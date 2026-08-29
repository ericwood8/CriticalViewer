using CriticalViewer.Api.Contracts;
using CriticalViewer.Api.Controllers;
using CriticalViewer.Api.Tests.TestSupport;
using CriticalViewer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriticalViewer.Api.Tests;

// Pure unit tests of the page-count ceiling-division math, calling the
// controller action directly against a fake IMovieCountProvider. This is
// deliberately not an HTTP/WebApplicationFactory test - the real provider
// (SqlMovieCountProvider) runs raw sys.partitions SQL that only SQL Server
// can execute, so its query itself is out of scope for automated tests here;
// this covers the controller logic that turns a count into totalPages.
public class MoviesPageCountTests
{
    private static AppDbContext CreateUnusedDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    [InlineData(250, 3)]
    [InlineData(1234, 13)]
    public async Task GetPageCount_ComputesCeilingDivisionAgainstFakeProvider(int totalMovies, int expectedPages)
    {
        using var db = CreateUnusedDbContext();
        var controller = new MoviesController(db, new FakeMovieCountProvider(totalMovies));

        var result = await controller.GetPageCount();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<MoviePageCountResponse>(ok.Value);
        Assert.Equal(totalMovies, body.TotalMovies);
        Assert.Equal(100, body.PageSize);
        Assert.Equal(expectedPages, body.TotalPages);
    }
}
