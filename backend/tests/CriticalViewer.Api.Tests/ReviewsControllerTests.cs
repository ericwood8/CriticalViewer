using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriticalViewer.Api.Contracts;
using CriticalViewer.Api.Tests.TestSupport;
using CriticalViewer.Core.Entities;
using Xunit;

namespace CriticalViewer.Api.Tests;

public class ReviewsControllerTests : IAsyncLifetime
{
    private readonly CriticalViewerWebApplicationFactory _factory = new();
    private HttpClient _client = null!;
    private Movie _reviewedMovie = null!;
    private List<Review> _seededReviews = null!;
    private Movie _freshMovie = null!;

    public async Task InitializeAsync()
    {
        await _factory.SeedAsync(async db =>
        {
            (_reviewedMovie, _seededReviews) = await TestSeedData.SeedMovieWithReviewsAsync(db, 15);

            _freshMovie = new Movie
            {
                Id = Guid.NewGuid(),
                Title = "Freshly Seeded Movie",
                Genre = "Drama",
                Director = "New Director",
                ReleaseYear = 2010,
                Summary = "Has no reviews yet."
            };
            db.Movies.Add(_freshMovie);
            await db.SaveChangesAsync();
        });

        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "SuperSecret123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        return auth!.Token;
    }

    // ---- GET reviews ----

    [Fact]
    public async Task GetReviews_OrdersNewestFirstAndPaginates()
    {
        var page1Response = await _client.GetAsync($"/api/movies/{_reviewedMovie.Id}/reviews?page=1");
        var page2Response = await _client.GetAsync($"/api/movies/{_reviewedMovie.Id}/reviews?page=2");

        var page1 = await page1Response.Content.ReadFromJsonAsync<PagedResult<ReviewListItem>>(TestJson.Options);
        var page2 = await page2Response.Content.ReadFromJsonAsync<PagedResult<ReviewListItem>>(TestJson.Options);

        Assert.Equal(15, page1!.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(5, page2!.Items.Count);

        // Seed index 14 is the most recently created review (see
        // TestSeedData.SeedMovieWithReviewsAsync) - it must lead page 1.
        var mostRecent = _seededReviews[14];
        Assert.Equal(mostRecent.Id, page1.Items.First().Id);

        var allOrdered = page1.Items.Concat(page2.Items).Select(r => r.CreatedAt).ToList();
        var sortedDesc = allOrdered.OrderByDescending(d => d).ToList();
        Assert.Equal(sortedDesc, allOrdered);
    }

    [Fact]
    public async Task GetReviews_ReviewerHasNoMatchingUser_StillReturnedWithFallbackUsername()
    {
        // Regression test: dbo.Reviews rows whose UserId doesn't resolve to
        // an AspNetUsers row (exactly what CriticalViewerDB.sql's own seed
        // data looks like) must still appear in the page, not be silently
        // dropped by an inadvertent INNER JOIN - see
        // TestSeedData.SeedMovieWithOrphanedReviewAsync.
        Movie orphanMovie = null!;
        Review orphanReview = null!;
        await _factory.SeedAsync(async db =>
        {
            (orphanMovie, orphanReview) = await TestSeedData.SeedMovieWithOrphanedReviewAsync(db);
        });

        var response = await _client.GetAsync($"/api/movies/{orphanMovie.Id}/reviews");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<ReviewListItem>>(TestJson.Options);

        Assert.Equal(1, body!.TotalCount);
        Assert.Single(body.Items);
        Assert.Equal(orphanReview.Id, body.Items[0].Id);
        Assert.Equal("Unknown", body.Items[0].Username);
    }

    [Fact]
    public async Task GetReviews_UnknownMovie_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/movies/{Guid.NewGuid()}/reviews");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetReviews_NoReviewsYet_ReturnsEmptyPage()
    {
        var response = await _client.GetAsync($"/api/movies/{_freshMovie.Id}/reviews");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<ReviewListItem>>(TestJson.Options);

        Assert.Equal(0, body!.TotalCount);
        Assert.Empty(body.Items);
    }

    // ---- POST reviews ----

    [Fact]
    public async Task CreateReview_Success_Returns201WithLocationAndBody()
    {
        var token = await RegisterAndGetTokenAsync("reviewer-success@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            $"/api/movies/{_freshMovie.Id}/reviews", new CreateReviewRequest(5, "Loved it, would watch again."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains($"/api/movies/{_freshMovie.Id}/reviews", response.Headers.Location!.ToString());

        var body = await response.Content.ReadFromJsonAsync<ReviewListItem>(TestJson.Options);
        Assert.NotNull(body);
        Assert.Equal(5, body!.Rating);
        Assert.Equal("Loved it, would watch again.", body.Body);
        Assert.Equal("reviewer-success@example.com", body.Username);
    }

    [Fact]
    public async Task CreateReview_DuplicateForSameUserAndMovie_ReturnsConflict()
    {
        var token = await RegisterAndGetTokenAsync("reviewer-dup@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var first = await _client.PostAsJsonAsync(
            $"/api/movies/{_freshMovie.Id}/reviews", new CreateReviewRequest(4, "First review."));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync(
            $"/api/movies/{_freshMovie.Id}/reviews", new CreateReviewRequest(3, "Trying again."));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task CreateReview_InvalidRating_ReturnsBadRequest(int invalidRating)
    {
        var token = await RegisterAndGetTokenAsync($"reviewer-rating-{invalidRating}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            $"/api/movies/{_freshMovie.Id}/reviews", new CreateReviewRequest(invalidRating, "Some body text."));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReview_EmptyBody_ReturnsBadRequest()
    {
        var token = await RegisterAndGetTokenAsync("reviewer-emptybody@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            $"/api/movies/{_freshMovie.Id}/reviews", new CreateReviewRequest(3, ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReview_UnknownMovie_ReturnsNotFound()
    {
        var token = await RegisterAndGetTokenAsync("reviewer-nomovie@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            $"/api/movies/{Guid.NewGuid()}/reviews", new CreateReviewRequest(3, "Some body text."));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateReview_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/movies/{_freshMovie.Id}/reviews", new CreateReviewRequest(3, "Some body text."));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
