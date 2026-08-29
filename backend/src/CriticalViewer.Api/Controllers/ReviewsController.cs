using CriticalViewer.Api.Contracts;
using CriticalViewer.Core.Entities;
using CriticalViewer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CriticalViewer.Api.Controllers;

// Feature: Movie Detail View.
// Reviews are always scoped to a movie, hence the nested route.
[ApiController]
[Route("api/movies/{movieId:guid}/reviews")]
public class ReviewsController(AppDbContext db, UserManager<ApplicationUser> userManager) : ControllerBase
{
    private const int PageSize = 10;

    // GET /api/movies/{movieId}/reviews?page=
    // Public - anonymous visitors can read reviews without logging in.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ReviewListItem>>> GetReviews(Guid movieId, [FromQuery] int page = 1)
    {
        if (page < 1)
        {
            return BadRequest("page must be 1 or greater.");
        }

        var movieExists = await db.Movies.AsNoTracking().AnyAsync(m => m.Id == movieId);
        if (!movieExists)
        {
            return NotFound();
        }

        var query = db.Reviews.AsNoTracking().Where(r => r.MovieId == movieId);

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)PageSize);

        if (totalCount > 0 && page > totalPages)
        {
            return BadRequest($"page {page} exceeds the {totalPages} pages available for this movie's reviews.");
        }

        // Explicit LEFT JOIN (GroupJoin + SelectMany + DefaultIfEmpty): a
        // plain `r.User!.UserName!` projection through the required
        // navigation compiles to an INNER JOIN, which silently drops any
        // review whose UserId doesn't resolve to an AspNetUsers row (e.g.
        // this schema's own seed data, which points at a separate demo
        // reviewers table - see CriticalViewerDB.sql) - that desynced
        // totalCount from items.Count instead of just showing a fallback.
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .GroupJoin(db.Users, r => r.UserId, u => u.Id, (r, users) => new { r, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new ReviewListItem(
                x.r.Id,
                u != null ? u.UserName! : "Unknown",
                x.r.Rating,
                x.r.Body,
                x.r.CreatedAt))
            .ToListAsync();

        return Ok(new PagedResult<ReviewListItem>(items, page, PageSize, totalCount, totalPages));
    }

    // POST /api/movies/{movieId}/reviews
    // Registered users only - the "Leave a Review" button prompts login
    // first for anonymous visitors, so this action never needs to.
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ReviewListItem>> CreateReview(Guid movieId, CreateReviewRequest request)
    {
        if (request.Rating is < 1 or > 5)
        {
            return BadRequest("rating must be between 1 and 5.");
        }

        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 2000)
        {
            return BadRequest("body is required and must be 2000 characters or fewer.");
        }

        var movieExists = await db.Movies.AsNoTracking().AnyAsync(m => m.Id == movieId);
        if (!movieExists)
        {
            return NotFound();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var alreadyReviewed = await db.Reviews.AsNoTracking()
            .AnyAsync(r => r.MovieId == movieId && r.UserId == user.Id);
        if (alreadyReviewed)
        {
            return Conflict("You have already reviewed this movie.");
        }

        var review = new Review
        {
            MovieId = movieId,
            UserId = user.Id,
            Rating = request.Rating,
            Body = request.Body,
            CreatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Translates a UQ_Reviews_Movie_User violation from a race
            // between the check above and this insert into a 409, matching
            // the pre-check path instead of surfacing a raw SQL exception.
            // (A catch filter can't itself await, so the re-check happens
            // in the catch body; any other cause is rethrown as-is.)
            var isDuplicate = await db.Reviews.AsNoTracking()
                .AnyAsync(r => r.MovieId == movieId && r.UserId == user.Id);
            if (!isDuplicate)
            {
                throw;
            }

            return Conflict("You have already reviewed this movie.");
        }

        var result = new ReviewListItem(review.Id, user.UserName!, review.Rating, review.Body, review.CreatedAt);

        return Created($"/api/movies/{movieId}/reviews", result);
    }
}
