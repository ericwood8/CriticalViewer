using CriticalViewer.Core.Entities;
using CriticalViewer.Infrastructure.Data;

namespace CriticalViewer.Api.Tests.TestSupport;

public static class TestSeedData
{
    // Offsets from "now" rather than fixed years, so seeded data never
    // accidentally collides with whatever the real current year is when
    // these tests run (GET /api/movies defaults its year filter to the
    // current UTC year - see MoviesController.GetMovies).
    public static readonly int CurrentYear = DateTime.UtcNow.Year;
    public const string PrefixTestGenre = "Test";
    public static readonly int PrefixTestYear = CurrentYear - 51;

    public const string BulkGenre = "Bulk";
    public const int BulkMovieCount = 120;
    public static readonly int BulkMovieYear = CurrentYear - 6;

    public static async Task<SeededMovies> SeedMoviesAsync(AppDbContext db)
    {
        var greatEscape = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "The Great Escape",
            Genre = "Drama",
            Director = "John Sturges",
            ReleaseYear = 1963,
            Summary = "Allied POWs plan a daring escape from a German camp."
        };
        var bigFish = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Big Fish",
            Genre = "Fantasy",
            Director = "Tim Burton",
            ReleaseYear = 2003,
            Summary = "A son unpacks his father's tall tales."
        };

        var currentYearA = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Current Year Movie A",
            Genre = "Action",
            Director = "Dir A",
            ReleaseYear = CurrentYear,
            Summary = "Explosions this year."
        };
        var currentYearB = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Current Year Movie B",
            Genre = "Action",
            Director = "Dir B",
            ReleaseYear = CurrentYear,
            Summary = "More explosions this year."
        };

        var alphaOne = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Alpha Movie One",
            Genre = PrefixTestGenre,
            Director = "Prefix Director",
            ReleaseYear = PrefixTestYear,
            Summary = "First alpha movie."
        };
        var alphaTwo = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Alpha Movie Two",
            Genre = PrefixTestGenre,
            Director = "Prefix Director",
            ReleaseYear = PrefixTestYear,
            Summary = "Second alpha movie."
        };
        var beta = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Beta Movie",
            Genre = PrefixTestGenre,
            Director = "Prefix Director",
            ReleaseYear = PrefixTestYear,
            Summary = "Not an alpha movie."
        };
        var gamma = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Gamma Movie",
            Genre = PrefixTestGenre,
            Director = "Solo Director",
            ReleaseYear = PrefixTestYear,
            Summary = "A movie with its own director."
        };

        db.Movies.AddRange(greatEscape, bigFish, currentYearA, currentYearB, alphaOne, alphaTwo, beta, gamma);

        var bulkMovies = new List<Movie>();
        for (var i = 0; i < BulkMovieCount; i++)
        {
            bulkMovies.Add(new Movie
            {
                Id = Guid.NewGuid(),
                Title = $"Bulk Movie {i:D3}",
                Genre = BulkGenre,
                Director = "Bulk Director",
                ReleaseYear = BulkMovieYear,
                Summary = "Filler movie for pagination tests."
            });
        }

        db.Movies.AddRange(bulkMovies);

        await db.SaveChangesAsync();

        return new SeededMovies(greatEscape, bigFish, currentYearA, currentYearB, alphaOne, alphaTwo, beta, gamma, bulkMovies);
    }

    // Seeds a movie plus `count` reviews from distinct users, CreatedAt
    // staggered one minute apart so DESC ordering is unambiguous.
    public static async Task<(Movie Movie, List<Review> Reviews)> SeedMovieWithReviewsAsync(AppDbContext db, int count)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Movie With Reviews",
            Genre = "Drama",
            Director = "Some Director",
            ReleaseYear = 1999,
            Summary = "Has reviews for pagination tests."
        };
        db.Movies.Add(movie);

        var reviews = new List<Review>();
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"reviewer{i:D3}",
                NormalizedUserName = $"REVIEWER{i:D3}",
                Email = $"reviewer{i:D3}@example.com",
                NormalizedEmail = $"REVIEWER{i:D3}@EXAMPLE.COM",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            db.Users.Add(user);

            var review = new Review
            {
                Id = Guid.NewGuid(),
                MovieId = movie.Id,
                UserId = user.Id,
                Rating = (i % 5) + 1,
                Body = $"Review body number {i}.",
                // Oldest first in insertion order; index 0 is the OLDEST
                // (created furthest in the past) so index (count-1) is the
                // most recent - DESC ordering should surface it first.
                CreatedAt = now.AddMinutes(-(count - i))
            };
            db.Reviews.Add(review);
            reviews.Add(review);
        }

        await db.SaveChangesAsync();

        return (movie, reviews);
    }

    // A review whose UserId doesn't match any ApplicationUser row - this is
    // exactly what CriticalViewerDB.sql's own seed data looks like (it
    // points reviewer IDs at a separate demo dbo.Reviewers table, not
    // AspNetUsers). Regression coverage for a bug where such a review was
    // silently dropped from the paged list (INNER JOIN via `r.User.UserName`)
    // even though it was correctly counted in totalCount.
    public static async Task<(Movie Movie, Review OrphanedReview)> SeedMovieWithOrphanedReviewAsync(AppDbContext db)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Movie With Orphaned Review",
            Genre = "Drama",
            Director = "Some Director",
            ReleaseYear = 1999,
            Summary = "Has a review whose author isn't a real AspNetUsers row."
        };
        db.Movies.Add(movie);

        var review = new Review
        {
            Id = Guid.NewGuid(),
            MovieId = movie.Id,
            UserId = Guid.NewGuid(), // no matching ApplicationUser
            Rating = 4,
            Body = "Orphaned review body.",
            CreatedAt = DateTime.UtcNow
        };
        db.Reviews.Add(review);

        await db.SaveChangesAsync();

        return (movie, review);
    }
}

public record SeededMovies(
    Movie GreatEscape,
    Movie BigFish,
    Movie CurrentYearA,
    Movie CurrentYearB,
    Movie AlphaOne,
    Movie AlphaTwo,
    Movie Beta,
    Movie Gamma,
    IReadOnlyList<Movie> BulkMovies);
