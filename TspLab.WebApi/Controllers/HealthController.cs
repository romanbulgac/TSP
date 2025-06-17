using Microsoft.AspNetCore.Mvc;

namespace TspLab.WebApi.Controllers;

/// <summary>
/// Controller for health check operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Health")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get the health status of the API
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        try
        {
            return Ok(new 
            { 
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Problem("Health check failed");
        }
    }
}
