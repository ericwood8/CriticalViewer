using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriticalViewer.Api.Contracts;
using CriticalViewer.Api.Tests.TestSupport;
using Xunit;

namespace CriticalViewer.Api.Tests;

// HTTP-level integration tests for GET /api/movies, /api/movies/{id} and
// /api/movies/page-count, run against the real app with AppDbContext
// swapped to EF Core's InMemory provider (see CriticalViewerWebApplicationFactory).
// Each test gets its own factory/database for isolation.
public class MoviesControllerTests : IAsyncLifetime
{
    private readonly CriticalViewerWebApplicationFactory _factory = new();
    private HttpClient _client = null!;
    private SeededMovies _movies = null!;

    public async Task InitializeAsync()
    {
        await _factory.SeedAsync(async db => _movies = await TestSeedData.SeedMoviesAsync(db));
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetMovies_TitlePrefixSearch_MatchesOnlyPrefixedTitles()
    {
        var response = await _client.GetAsync(
            $"/api/movies?title=Alpha&year={TestSeedData.PrefixTestYear}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<MovieListItem>>(TestJson.Options);

        Assert.NotNull(body);
        Assert.Equal(2, body!.TotalCount);
        Assert.Equal(["Alpha Movie One", "Alpha Movie Two"], body.Items.Select(m => m.Title).OrderBy(t => t));
    }

    [Fact]
    public async Task GetMovies_GenreFilter_ExactMatchOnly()
    {
        var response = await _client.GetAsync(
            $"/api/movies?genre={TestSeedData.PrefixTestGenre}&year={TestSeedData.PrefixTestYear}");

        var body = await response.Content.ReadFromJsonAsync<PagedResult<MovieListItem>>(TestJson.Options);

        Assert.Equal(4, body!.TotalCount);
        Assert.All(body.Items, m => Assert.Equal(TestSeedData.PrefixTestGenre, m.Genre));
    }

    [Fact]
    public async Task GetMovies_DirectorFilter_ExactMatchOnly()
    {
        var response = await _client.GetAsync(
            $"/api/movies?director={Uri.EscapeDataString("Solo Director")}&year={TestSeedData.PrefixTestYear}");

        var body = await response.Content.ReadFromJsonAsync<PagedResult<MovieListItem>>(TestJson.Options);

        Assert.Equal(1, body!.TotalCount);
        Assert.Equal("Gamma Movie", body.Items.Single().Title);
    }

    [Fact]
    public async Task GetMovies_CombinedFilters_Intersect()
    {
        var response = await _client.GetAsync(
            $"/api/movies?title=Alpha&genre={TestSeedData.PrefixTestGenre}&director={Uri.EscapeDataString("Prefix Director")}&year={TestSeedData.PrefixTestYear}");

        var body = await response.Content.ReadFromJsonAsync<PagedResult<MovieListItem>>(TestJson.Options);

        Assert.Equal(2, body!.TotalCount);
    }

    [Fact]
    public async Task GetMovies_NoYearParam_ReturnsMoviesAcrossAllYears()
    {
        var response = await _client.GetAsync("/api/movies");

        var body = await response.Content.ReadFromJsonAsync<PagedResult<MovieListItem>>(TestJson.Options);

        // No year filter applied when the param is omitted - the whole
        // seeded catalog (8 named movies spanning several different years,
        // plus 120 bulk movies) is in scope, not just the current year's.
        Assert.Equal(128, body!.TotalCount);
        Assert.Equal(2, body.TotalPages);
    }

    [Fact]
    public async Task GetMovies_YearFilterOnly_MatchesExactYear()
    {
        var response = await _client.GetAsync($"/api/movies?year={TestSeedData.CurrentYear}");

        var body = await response.Content.ReadFromJsonAsync<PagedResult<MovieListItem>>(TestJson.Options);

        Assert.Equal(2, body!.TotalCount);
        Assert.All(body.Items, m => Assert.Equal(TestSeedData.CurrentYear, m.ReleaseYear));
    }

    [Fact]
    public async Task GetMovies_OffsetPagination_SplitsAcrossPages()
    {
        var page1Response = await _client.GetAsync(
            $"/api/movies?genre={TestSeedData.BulkGenre}&year={TestSeedData.BulkMovieYear}&page=1");
        var page2Response = await _client.GetAsync(
            $"/api/movies?genre={TestSeedData.BulkGenre}&year={TestSeedData.BulkMovieYear}&page=2");

        var page1 = await page1Response.Content.ReadFromJsonAsync<PagedResult<MovieListItem>>(TestJson.Options);
        var page2 = await page2Response.Content.ReadFromJsonAsync<PagedResult<MovieListItem>>(TestJson.Options);

        Assert.Equal(120, page1!.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(100, page1.Items.Count);
        Assert.Equal(20, page2!.Items.Count);

        var allIds = page1.Items.Select(m => m.Id).Concat(page2.Items.Select(m => m.Id)).Distinct();
        Assert.Equal(120, allIds.Count());
    }

    [Fact]
    public async Task GetMovies_PageBeyondRange_ReturnsBadRequest()
    {
        var response = await _client.GetAsync(
            $"/api/movies?genre={TestSeedData.BulkGenre}&year={TestSeedData.BulkMovieYear}&page=3");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMovies_PageZero_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/movies?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMovies_NoMatches_ReturnsEmptyPageOneOk()
    {
        var response = await _client.GetAsync(
            $"/api/movies?title=NoSuchMovieXYZ&year={TestSeedData.PrefixTestYear}&page=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<MovieListItem>>(TestJson.Options);

        Assert.Equal(0, body!.TotalCount);
        Assert.Equal(0, body.TotalPages);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task GetMovie_ById_ReturnsFullDetail()
    {
        var response = await _client.GetAsync($"/api/movies/{_movies.BigFish.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MovieListItem>(TestJson.Options);

        Assert.NotNull(body);
        Assert.Equal("Big Fish", body!.Title);
        Assert.Equal("Fantasy", body.Genre);
        Assert.Equal("Tim Burton", body.Director);
        Assert.Equal(2003, body.ReleaseYear);
    }

    [Fact]
    public async Task GetMovie_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/movies/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPageCount_ReturnsWholeCatalogCountFromProvider()
    {
        _factory.MovieCountProvider.Count = 1234;

        var response = await _client.GetAsync("/api/movies/page-count");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MoviePageCountResponse>(TestJson.Options);

        Assert.NotNull(body);
        Assert.Equal(1234, body!.TotalMovies);
        Assert.Equal(100, body.PageSize);
        Assert.Equal(13, body.TotalPages);
    }

    // ---- POST /api/movies ----

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "SuperSecret123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        return auth!.Token;
    }

    [Fact]
    public async Task CreateMovie_Success_Returns201WithLocationAndBody()
    {
        var token = await RegisterAndGetTokenAsync("movie-creator@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateMovieRequest(
            "Newly Added Movie", "Drama", "New Director", 2027, "/posters/new.jpg", "A tagline.", "A summary.");
        var response = await _client.PostAsJsonAsync("/api/movies", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/movies/", response.Headers.Location!.ToString());

        var body = await response.Content.ReadFromJsonAsync<MovieListItem>(TestJson.Options);
        Assert.NotNull(body);
        Assert.Equal("Newly Added Movie", body!.Title);
        Assert.Equal(2027, body.ReleaseYear);

        // The Location header must actually resolve.
        var getResponse = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task CreateMovie_MissingTitle_ReturnsBadRequest()
    {
        var token = await RegisterAndGetTokenAsync("movie-creator-badtitle@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateMovieRequest("", "Drama", "New Director", 2027, null, null, "A summary.");
        var response = await _client.PostAsJsonAsync("/api/movies", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1800)]
    [InlineData(2200)]
    public async Task CreateMovie_ReleaseYearOutOfRange_ReturnsBadRequest(int invalidYear)
    {
        var token = await RegisterAndGetTokenAsync($"movie-creator-year-{invalidYear}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateMovieRequest("Some Title", "Drama", "New Director", invalidYear, null, null, "A summary.");
        var response = await _client.PostAsJsonAsync("/api/movies", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateMovie_WithoutAuth_ReturnsUnauthorized()
    {
        var request = new CreateMovieRequest("Some Title", "Drama", "New Director", 2027, null, null, "A summary.");
        var response = await _client.PostAsJsonAsync("/api/movies", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
