using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CriticalViewer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    // Used by the ECS/App Runner health check and by GitHub Actions smoke
    // tests after a deploy - keep this fast and dependency-free.
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "healthy", timeUtc = DateTimeOffset.UtcNow });
}
