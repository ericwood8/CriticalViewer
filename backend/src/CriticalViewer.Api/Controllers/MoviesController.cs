using CriticalViewer.Api.Contracts;
using CriticalViewer.Api.Services;
using CriticalViewer.Core.Entities;
using CriticalViewer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CriticalViewer.Api.Controllers;

// Feature: Movie List / Search, Movie Detail View.
// Read endpoints are public; creating a movie requires being logged in
// (same trust level as leaving a review - no separate admin role exists
// in this app). Auth is per-action, not class-level [AllowAnonymous],
// since that would silently override any [Authorize] placed on an action.
[ApiController]
[Route("api/movies")]
public class MoviesController(AppDbContext db, IMovieCountProvider movieCountProvider) : ControllerBase
{
    private const int PageSize = 100;
    private const int EarliestReleaseYear = 1888; // Roundhay Garden Scene - the earliest surviving film
    private const int FurthestFutureReleaseYear = 5; // years beyond "now", for scheduled/announced releases

    // GET /api/movies?title=&genre=&director=&year=&page=
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<MovieListItem>>> GetMovies(
        [FromQuery] string? title,
        [FromQuery] string? genre,
        [FromQuery] string? director,
        [FromQuery] int? year,
        [FromQuery] int page = 1)
    {
        if (page < 1)
        {
            return BadRequest("page must be 1 or greater.");
        }

        var query = db.Movies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(m => EF.Functions.Like(m.Title, title + "%"));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            query = query.Where(m => m.Genre == genre);
        }

        if (!string.IsNullOrWhiteSpace(director))
        {
            query = query.Where(m => m.Director == director);
        }

        // Year is a plain optional filter, same as title/genre/director - an
        // omitted year means "any year", not "current year". The brief's
        // "defaults to a filter of the current year" is the search form's
        // *initial* state (see HomePage.tsx), not something the API should
        // silently re-impose whenever the param is blank - otherwise a
        // visitor can never actually search across all years.
        if (year.HasValue)
        {
            query = query.Where(m => m.ReleaseYear == year.Value);
        }

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)PageSize);

        if (totalCount > 0 && page > totalPages)
        {
            return BadRequest($"page {page} exceeds the {totalPages} pages available for this search.");
        }

        var items = await query
            .OrderBy(m => m.Title)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(m => MovieListItem.FromEntity(m))
            .ToListAsync();

        return Ok(new PagedResult<MovieListItem>(items, page, PageSize, totalCount, totalPages));
    }

    // GET /api/movies/page-count
    // Whole-catalog page count (unfiltered), computed without a table scan
    // via IMovieCountProvider. Routed ahead of {id:guid} via the id route's
    // guid constraint so this literal segment never gets parsed as a Guid.
    [HttpGet("page-count")]
    [AllowAnonymous]
    public async Task<ActionResult<MoviePageCountResponse>> GetPageCount()
    {
        var totalMovies = await movieCountProvider.GetTotalMovieCountAsync();
        var totalPages = totalMovies == 0 ? 0 : (int)Math.Ceiling(totalMovies / (double)PageSize);

        return Ok(new MoviePageCountResponse(totalMovies, PageSize, totalPages));
    }

    // GET /api/movies/{id}
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<MovieListItem>> GetMovie(Guid id)
    {
        var movie = await db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (movie is null)
        {
            return NotFound();
        }

        return Ok(MovieListItem.FromEntity(movie));
    }

    // POST /api/movies
    // Any logged-in user can add a movie - same trust level as leaving a
    // review. Not gated behind an admin role; there isn't one in this app.
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<MovieListItem>> CreateMovie(CreateMovieRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 300)
        {
            return BadRequest("title is required and must be 300 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Genre) || request.Genre.Length > 100)
        {
            return BadRequest("genre is required and must be 100 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Director) || request.Director.Length > 200)
        {
            return BadRequest("director is required and must be 200 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Summary) || request.Summary.Length > 2000)
        {
            return BadRequest("summary is required and must be 2000 characters or fewer.");
        }

        if (request.PosterUrl?.Length > 500)
        {
            return BadRequest("posterUrl must be 500 characters or fewer.");
        }

        if (request.Tagline?.Length > 300)
        {
            return BadRequest("tagline must be 300 characters or fewer.");
        }

        var maxYear = DateTime.UtcNow.Year + FurthestFutureReleaseYear;
        if (request.ReleaseYear < EarliestReleaseYear || request.ReleaseYear > maxYear)
        {
            return BadRequest($"releaseYear must be between {EarliestReleaseYear} and {maxYear}.");
        }

        var movie = new Movie
        {
            Title = request.Title,
            Genre = request.Genre,
            Director = request.Director,
            ReleaseYear = request.ReleaseYear,
            PosterUrl = request.PosterUrl,
            Tagline = request.Tagline,
            Summary = request.Summary
        };

        db.Movies.Add(movie);
        await db.SaveChangesAsync();

        var result = MovieListItem.FromEntity(movie);
        return CreatedAtAction(nameof(GetMovie), new { id = movie.Id }, result);
    }
}
