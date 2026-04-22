using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BasmaApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public HealthController(IHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var hasDatabaseConnection = !string.IsNullOrWhiteSpace(_configuration.GetConnectionString("DefaultConnection"));
        return Ok(new
        {
            status = "ok",
            environment = _environment.EnvironmentName,
            databaseConfigured = hasDatabaseConnection,
            timestampUtc = DateTime.UtcNow
        });
    }
}