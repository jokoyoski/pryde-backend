using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
}