using CriticalViewer.Api.Services;
using CriticalViewer.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CriticalViewer.Api.Tests.TestSupport;

// Boots the real app (Program.cs) for HTTP-level integration tests, with
// two swaps so it never needs a live SQL Server (CI runs on ubuntu-latest
// with none available - see .github/workflows/backend.yml):
//   1. AppDbContext's SQL Server registration -> EF Core's InMemory provider.
//   2. SqlMovieCountProvider (raw sys.partitions SQL) -> a settable fake.
// Jwt:SigningKey has no value in appsettings*.json (by design - it's meant
// to come from user-secrets/env vars in real environments), and Program.cs
// reads it into a local variable before WebApplicationFactory's
// ConfigureWebHost hooks run, so ConfigureAppConfiguration would be too
// late. Setting the environment variable in a static constructor guarantees
// it's visible when WebApplication.CreateBuilder(args) first reads config,
// which happens well before any of that.
public class CriticalViewerWebApplicationFactory : WebApplicationFactory<Program>
{
    static CriticalViewerWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey", "test-only-signing-key-not-for-any-real-environment-32bytes+");
    }

    private readonly string _dbName = $"CriticalViewerTests-{Guid.NewGuid()}";

    public FakeMovieCountProvider MovieCountProvider { get; } = new(0);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            // EF Core (8+) registers each AddDbContext(...) configure action as an
            // additive IDbContextOptionsConfiguration<AppDbContext> rather than
            // replacing a prior one - Program.cs's UseSqlServer(...) registration
            // has to be removed explicitly, or it gets applied together with
            // UseInMemoryDatabase below and EF rejects the resulting options for
            // carrying two database providers at once.
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));

            services.RemoveAll<IMovieCountProvider>();
            services.AddSingleton<IMovieCountProvider>(MovieCountProvider);
        });
    }

    // Seeds through the same DI-built AppDbContext the running app uses to
    // serve requests (Services triggers host build on first access), so
    // seeded data is guaranteed visible to subsequent HTTP calls against
    // this factory's CreateClient().
    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await seed(db);
        await db.SaveChangesAsync();
    }
}
