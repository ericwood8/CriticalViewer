using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CriticalViewer.Api.Tests;

// A starter integration test wired against WebApplicationFactory<Program>.
// Replace/extend with real coverage as each feature lands - the brief
// requires PRs to fail if lint or the test suite has issues, so keep this
// project green and growing rather than skipped.
public class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
