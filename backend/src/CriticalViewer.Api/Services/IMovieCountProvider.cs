namespace CriticalViewer.Api.Services;

// Abstraction over "how many movies are in the catalog" so the page-count
// math (ceiling division) can be unit-tested against a fake without a
// live SQL Server connection - see SqlMovieCountProvider for the real
// row-count-metadata query this wraps.
public interface IMovieCountProvider
{
    Task<int> GetTotalMovieCountAsync();
}
