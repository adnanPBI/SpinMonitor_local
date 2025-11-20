using Microsoft.EntityFrameworkCore;
using Serilog;
using SpinMonitor.Api.Data;
using SpinMonitor.Api.Middleware;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() {
        Title = "SpinMonitor API",
        Version = "v1",
        Description = "REST API for SpinMonitor Desktop - Stream Detection Logging"
    });
});

// MySQL Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SpinMonitorDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "*" };

        if (allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SpinMonitor API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseSerilogRequestLogging();

app.UseResponseCompression();

// Custom API Key middleware (if enabled)
if (builder.Configuration.GetValue<bool>("Security:EnableApiKeyAuth"))
{
    app.UseMiddleware<ApiKeyAuthMiddleware>();
}

app.UseIpRateLimiting();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// Startup banner
var port = builder.Configuration.GetValue<string>("Urls") ?? "http://localhost:5000";
Log.Information("╔════════════════════════════════════════════════════════╗");
Log.Information("║       SpinMonitor Backend API (C# ASP.NET Core)       ║");
Log.Information("╚════════════════════════════════════════════════════════╝");
Log.Information("✓ Server running: {Port}", port);
Log.Information("✓ Environment: {Environment}", app.Environment.EnvironmentName);
Log.Information("✓ Database: {ConnectionString}", connectionString?.Split(";")[0]);
Log.Information("✓ API Key Auth: {Enabled}", builder.Configuration.GetValue<bool>("Security:EnableApiKeyAuth"));
Log.Information("📡 Swagger UI: {SwaggerUrl}", app.Environment.IsDevelopment() ? "http://localhost:5000" : "Disabled");
Log.Information("🚀 Ready to accept requests!");

app.Run();
