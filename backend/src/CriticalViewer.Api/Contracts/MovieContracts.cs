using CriticalViewer.Core.Entities;

namespace CriticalViewer.Api.Contracts;

public record MovieListItem(
    Guid Id,
    string Title,
    string Genre,
    string Director,
    int ReleaseYear,
    string? PosterUrl,
    string? Tagline,
    string Summary)
{
    public static MovieListItem FromEntity(Movie movie) => new(
        movie.Id,
        movie.Title,
        movie.Genre,
        movie.Director,
        movie.ReleaseYear,
        movie.PosterUrl,
        movie.Tagline,
        movie.Summary);
}

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record MoviePageCountResponse(int TotalMovies, int PageSize, int TotalPages);

public record CreateMovieRequest(
    string Title,
    string Genre,
    string Director,
    int ReleaseYear,
    string? PosterUrl,
    string? Tagline,
    string Summary);
