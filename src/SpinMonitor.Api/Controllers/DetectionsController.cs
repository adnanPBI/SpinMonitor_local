using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpinMonitor.Api.Data;
using SpinMonitor.Api.Models;
using SpinMonitor.Api.Models.DTOs;
using System.Globalization;

namespace SpinMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DetectionsController : ControllerBase
    {
        private readonly SpinMonitorDbContext _context;
        private readonly ILogger<DetectionsController> _logger;

        public DetectionsController(SpinMonitorDbContext context, ILogger<DetectionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Create a new detection record
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDetectionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse(
                        "Validation failed",
                        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                    ));
                }

                // Parse timestamp
                if (!DateTime.TryParse(request.Timestamp, out var timestamp))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("Invalid timestamp format"));
                }

                var detection = new Detection
                {
                    Timestamp = timestamp,
                    Stream = request.Stream,
                    StreamType = request.Stream_Type,
                    StreamNumber = request.Stream_Number,
                    Track = request.Track,
                    DurationSeconds = request.Duration_Seconds ?? 10,
                    Confidence = request.Confidence.HasValue ? (decimal)request.Confidence.Value : null,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Detections.Add(detection);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Detection created: ID={Id}, Track={Track}, Stream={Stream}",
                    detection.Id, detection.Track, detection.Stream);

                return CreatedAtAction(nameof(GetById), new { id = detection.Id },
                    ApiResponse<Detection>.SuccessResponse(detection, "Detection created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create detection");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to create detection", ex.Message));
            }
        }

        /// <summary>
        /// Create multiple detection records at once
        /// </summary>
        [HttpPost("batch")]
        public async Task<IActionResult> CreateBatch([FromBody] BatchDetectionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || request.Detections == null || request.Detections.Count == 0)
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("detections must be a non-empty array"));
                }

                var detections = new List<Detection>();

                foreach (var req in request.Detections)
                {
                    if (!DateTime.TryParse(req.Timestamp, out var timestamp))
                    {
                        return BadRequest(ApiResponse<object>.ErrorResponse($"Invalid timestamp: {req.Timestamp}"));
                    }

                    detections.Add(new Detection
                    {
                        Timestamp = timestamp,
                        Stream = req.Stream,
                        StreamType = req.Stream_Type,
                        StreamNumber = req.Stream_Number,
                        Track = req.Track,
                        DurationSeconds = req.Duration_Seconds ?? 10,
                        Confidence = req.Confidence.HasValue ? (decimal)req.Confidence.Value : null,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                _context.Detections.AddRange(detections);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Batch created: {Count} detections", detections.Count);

                return CreatedAtAction(nameof(GetAll), null,
                    ApiResponse<object>.SuccessResponse(new
                    {
                        count = detections.Count,
                        firstId = detections.First().Id
                    }, $"{detections.Count} detections created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create batch detections");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to create batch detections", ex.Message));
            }
        }

        /// <summary>
        /// Get all detections with pagination and filtering
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? stream = null,
            [FromQuery] string? track = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] double? min_confidence = null,
            [FromQuery] string sort = "desc")
        {
            try
            {
                limit = Math.Min(limit, 1000);
                var query = _context.Detections.AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(stream))
                    query = query.Where(d => d.Stream == stream);

                if (!string.IsNullOrEmpty(track))
                    query = query.Where(d => d.Track != null && d.Track.Contains(track));

                if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDate))
                    query = query.Where(d => d.Timestamp >= fromDate);

                if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDate))
                    query = query.Where(d => d.Timestamp <= toDate);

                if (min_confidence.HasValue)
                    query = query.Where(d => d.Confidence >= (decimal)min_confidence.Value);

                // Get total count
                var total = await query.CountAsync();

                // Apply sorting
                query = sort.ToLower() == "asc"
                    ? query.OrderBy(d => d.Timestamp)
                    : query.OrderByDescending(d => d.Timestamp);

                // Apply pagination
                var detections = await query
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .ToListAsync();

                var response = new PaginationResponse<Detection>
                {
                    Data = detections,
                    Pagination = new PaginationInfo
                    {
                        Page = page,
                        Limit = limit,
                        Total = total,
                        TotalPages = (int)Math.Ceiling(total / (double)limit),
                        HasMore = (page * limit) < total
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch detections");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to fetch detections", ex.Message));
            }
        }

        /// <summary>
        /// Get recent detections (last N seconds) for "Live Now" panel
        /// </summary>
        [HttpGet("live")]
        public async Task<IActionResult> GetLive([FromQuery] int seconds = 120)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-seconds);

                var detections = await _context.Detections
                    .Where(d => d.Timestamp >= cutoff)
                    .OrderByDescending(d => d.Timestamp)
                    .ToListAsync();

                var response = new
                {
                    success = true,
                    data = detections,
                    count = detections.Count,
                    timeWindow = $"{seconds} seconds"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch live detections");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to fetch live detections", ex.Message));
            }
        }

        /// <summary>
        /// Get detection statistics
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats([FromQuery] string period = "day")
        {
            try
            {
                DateTime? cutoff = period.ToLower() switch
                {
                    "hour" => DateTime.UtcNow.AddHours(-1),
                    "day" => DateTime.UtcNow.AddDays(-1),
                    "week" => DateTime.UtcNow.AddDays(-7),
                    "month" => DateTime.UtcNow.AddMonths(-1),
                    "year" => DateTime.UtcNow.AddYears(-1),
                    "all" => null,
                    _ => DateTime.UtcNow.AddDays(-1)
                };

                var query = cutoff.HasValue
                    ? _context.Detections.Where(d => d.Timestamp >= cutoff.Value)
                    : _context.Detections;

                var overall = await query
                    .GroupBy(d => 1)
                    .Select(g => new
                    {
                        total_detections = g.Count(),
                        unique_streams = g.Select(d => d.Stream).Distinct().Count(),
                        unique_tracks = g.Select(d => d.Track).Distinct().Count(),
                        avg_confidence = g.Average(d => d.Confidence),
                        first_detection = g.Min(d => d.Timestamp),
                        last_detection = g.Max(d => d.Timestamp)
                    })
                    .FirstOrDefaultAsync();

                var topStreams = await query
                    .GroupBy(d => d.Stream)
                    .Select(g => new
                    {
                        stream = g.Key,
                        detection_count = g.Count()
                    })
                    .OrderByDescending(x => x.detection_count)
                    .Take(10)
                    .ToListAsync();

                var topTracks = await query
                    .Where(d => d.Track != null)
                    .GroupBy(d => d.Track)
                    .Select(g => new
                    {
                        track = g.Key,
                        play_count = g.Count(),
                        avg_confidence = g.Average(d => d.Confidence)
                    })
                    .OrderByDescending(x => x.play_count)
                    .Take(10)
                    .ToListAsync();

                var response = new
                {
                    success = true,
                    period,
                    stats = overall,
                    topStreams,
                    topTracks
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch statistics");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to fetch statistics", ex.Message));
            }
        }

        /// <summary>
        /// Get a specific detection by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var detection = await _context.Detections.FindAsync(id);

                if (detection == null)
                {
                    return NotFound(ApiResponse<object>.ErrorResponse("Detection not found"));
                }

                return Ok(ApiResponse<Detection>.SuccessResponse(detection));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch detection");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to fetch detection", ex.Message));
            }
        }

        /// <summary>
        /// Delete a specific detection by ID
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var detection = await _context.Detections.FindAsync(id);

                if (detection == null)
                {
                    return NotFound(ApiResponse<object>.ErrorResponse("Detection not found"));
                }

                _context.Detections.Remove(detection);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Detection deleted: ID={Id}", id);

                return Ok(ApiResponse<object>.SuccessResponse(null, "Detection deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete detection");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Failed to delete detection", ex.Message));
            }
        }
    }
}
