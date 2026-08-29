using CriticalViewer.Api.Services;

namespace CriticalViewer.Api.Tests.TestSupport;

// Settable stand-in for SqlMovieCountProvider's sys.partitions query, which
// can't run against EF Core's InMemory provider. Lets tests exercise the
// page-count math (ceiling division) without a live SQL Server connection.
public class FakeMovieCountProvider(int count) : IMovieCountProvider
{
    public int Count { get; set; } = count;

    public Task<int> GetTotalMovieCountAsync() => Task.FromResult(Count);
}
