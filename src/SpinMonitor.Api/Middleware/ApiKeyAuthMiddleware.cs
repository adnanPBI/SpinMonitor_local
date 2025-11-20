namespace SpinMonitor.Api.Middleware
{
    public class ApiKeyAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiKeyAuthMiddleware> _logger;

        public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip authentication for health endpoint and Swagger
            var path = context.Request.Path.Value?.ToLower() ?? "";
            if (path.StartsWith("/api/health") || path.StartsWith("/swagger") || path == "/")
            {
                await _next(context);
                return;
            }

            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            var expectedApiKey = _configuration["Security:ApiKey"];

            if (string.IsNullOrEmpty(apiKey) || apiKey != expectedApiKey)
            {
                _logger.LogWarning("Unauthorized API request from {IP}", context.Connection.RemoteIpAddress);

                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    error = "Unauthorized - Invalid or missing API key"
                });
                return;
            }

            await _next(context);
        }
    }
}
