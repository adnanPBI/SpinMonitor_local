using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpinMonitor.Api.Data;

namespace SpinMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly SpinMonitorDbContext _context;
        private readonly ILogger<HealthController> _logger;

        public HealthController(SpinMonitorDbContext context, ILogger<HealthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Basic health check endpoint
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                // Test database connection
                await _context.Database.ExecuteSqlRawAsync("SELECT 1");

                var response = new
                {
                    success = true,
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    uptime = (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
                    database = "connected"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");

                var response = new
                {
                    success = false,
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow,
                    database = "disconnected",
                    error = ex.Message
                };

                return StatusCode(503, response);
            }
        }

        /// <summary>
        /// Detailed database health check
        /// </summary>
        [HttpGet("db")]
        public async Task<IActionResult> GetDatabaseHealth()
        {
            try
            {
                var stats = await _context.Detections
                    .GroupBy(d => 1)
                    .Select(g => new
                    {
                        total_detections = g.Count(),
                        last_detection = g.Max(d => d.CreatedAt),
                        first_detection = g.Min(d => d.CreatedAt)
                    })
                    .FirstOrDefaultAsync();

                var response = new
                {
                    success = true,
                    database = new
                    {
                        status = "connected",
                        stats = stats ?? new
                        {
                            total_detections = 0,
                            last_detection = (DateTime?)null,
                            first_detection = (DateTime?)null
                        }
                    },
                    timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");

                var response = new
                {
                    success = false,
                    database = new
                    {
                        status = "error",
                        error = ex.Message
                    },
                    timestamp = DateTime.UtcNow
                };

                return StatusCode(503, response);
            }
        }
    }
}
