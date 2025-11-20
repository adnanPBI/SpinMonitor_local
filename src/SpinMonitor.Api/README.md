# SpinMonitor API (C# ASP.NET Core)

ASP.NET Core Web API backend for SpinMonitor Desktop. Provides REST endpoints for logging and querying stream detection events to a MySQL database.

## Features

- 🎯 **RESTful API** - Standard HTTP/JSON interface
- 🔒 **Security** - Optional API key authentication, CORS, rate limiting
- 📊 **Live Detections** - Real-time endpoint for "Live Now" panel
- 📈 **Statistics** - Analytics and insights endpoints
- ⚡ **High Performance** - Entity Framework Core with connection pooling
- 🛡️ **Production Ready** - Serilog logging, error handling, Swagger docs
- 💾 **MySQL Integration** - Full Entity Framework Core support
- 📖 **Auto Documentation** - Swagger/OpenAPI built-in

## Prerequisites

- **.NET 9.0 SDK** or higher
- **MySQL** 5.7+ or 8.0+
- **Visual Studio 2022** / **VS Code** / **Rider** (optional)

## Quick Start

### 1. Configure Database Connection

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=spinmonitor;User=root;Password=your_password;"
  }
}
```

### 2. Ensure MySQL Schema Exists

The `detections` table must exist. See main project's `MYSQL_SETUP.md`:

```sql
CREATE DATABASE IF NOT EXISTS spinmonitor;

CREATE TABLE IF NOT EXISTS detections (
  id INT AUTO_INCREMENT PRIMARY KEY,
  timestamp DATETIME NOT NULL,
  stream VARCHAR(255) NOT NULL,
  stream_type VARCHAR(50),
  stream_number VARCHAR(50),
  track VARCHAR(500),
  duration_seconds INT,
  confidence DECIMAL(5,3),
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

  INDEX idx_timestamp (timestamp),
  INDEX idx_stream (stream),
  INDEX idx_track (track(255)),
  INDEX idx_created_at (created_at),
  INDEX idx_confidence (confidence)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### 3. Run the API

**Option A: Command Line**
```bash
cd src/SpinMonitor.Api
dotnet restore
dotnet run
```

**Option B: Visual Studio**
1. Open `SpinMonitor.sln`
2. Set `SpinMonitor.Api` as startup project
3. Press F5 to run

**Option C: VS Code**
```bash
cd src/SpinMonitor.Api
dotnet watch run
```

The API will start at **http://localhost:5000**

### 4. Access Swagger UI

Open your browser: **http://localhost:5000**

Interactive API documentation and testing interface.

## Project Structure

```
src/SpinMonitor.Api/
├── Controllers/
│   ├── DetectionsController.cs    # Detection CRUD endpoints
│   └── HealthController.cs        # Health check endpoints
├── Data/
│   └── SpinMonitorDbContext.cs    # Entity Framework DbContext
├── Models/
│   ├── Detection.cs               # Detection entity model
│   └── DTOs/                      # Data transfer objects
│       ├── CreateDetectionRequest.cs
│       └── ApiResponse.cs
├── Middleware/
│   └── ApiKeyAuthMiddleware.cs    # API key authentication
├── Program.cs                     # Application entry point
├── appsettings.json              # Configuration
└── SpinMonitor.Api.csproj        # Project file
```

## API Endpoints

### Health Check

**GET** `/api/health`

Check if API and database are healthy.

**Response:**
```json
{
  "success": true,
  "status": "healthy",
  "timestamp": "2025-01-20T12:30:00Z",
  "uptime": 123.45,
  "database": "connected"
}
```

**GET** `/api/health/db`

Detailed database health with statistics.

### Create Detection

**POST** `/api/detections`

**Request Body:**
```json
{
  "timestamp": "2025-01-20T12:30:45",
  "stream": "Radio Stream 1",
  "stream_type": "HTTP-MP3",
  "stream_number": "12345",
  "track": "Artist - Song Title",
  "duration_seconds": 10,
  "confidence": 0.95
}
```

**Response:**
```json
{
  "success": true,
  "message": "Detection created successfully",
  "data": {
    "id": 1234,
    "timestamp": "2025-01-20T12:30:45",
    "stream": "Radio Stream 1",
    "track": "Artist - Song Title",
    ...
  }
}
```

### Batch Create Detections

**POST** `/api/detections/batch`

**Request Body:**
```json
{
  "detections": [
    {
      "timestamp": "2025-01-20T12:30:45",
      "stream": "Stream 1",
      "track": "Track 1"
    },
    {
      "timestamp": "2025-01-20T12:31:00",
      "stream": "Stream 2",
      "track": "Track 2"
    }
  ]
}
```

### Query Detections

**GET** `/api/detections`

**Query Parameters:**
- `page` - Page number (default: 1)
- `limit` - Results per page (default: 50, max: 1000)
- `stream` - Filter by exact stream name
- `track` - Search in track name (partial match)
- `from` - Start date (ISO 8601)
- `to` - End date (ISO 8601)
- `min_confidence` - Minimum confidence score
- `sort` - Sort order: `desc` or `asc` (default: desc)

**Example:**
```
GET /api/detections?page=1&limit=20&stream=Radio%20Stream%201&min_confidence=0.8
```

### Get Live Detections

**GET** `/api/detections/live?seconds=120`

Get recent detections (default: last 2 minutes).

Perfect for the "Live Now" panel.

### Get Statistics

**GET** `/api/detections/stats?period=day`

**Period Options:** `hour`, `day`, `week`, `month`, `year`, `all`

**Response:**
```json
{
  "success": true,
  "period": "day",
  "stats": {
    "total_detections": 1234,
    "unique_streams": 15,
    "unique_tracks": 456,
    "avg_confidence": 0.85,
    ...
  },
  "topStreams": [...],
  "topTracks": [...]
}
```

### Get Single Detection

**GET** `/api/detections/{id}`

### Delete Detection

**DELETE** `/api/detections/{id}`

## Configuration

### appsettings.json

```json
{
  "Urls": "http://localhost:5000",

  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=spinmonitor;User=root;Password=;"
  },

  "Security": {
    "EnableApiKeyAuth": false,
    "ApiKey": "your-secret-api-key-here"
  },

  "Cors": {
    "AllowedOrigins": ["*"]
  },

  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      }
    ]
  }
}
```

### Security Settings

**Enable API Key Authentication:**

```json
{
  "Security": {
    "EnableApiKeyAuth": true,
    "ApiKey": "your-very-secret-key-here"
  }
}
```

Clients must include `X-API-Key` header:

```
X-API-Key: your-very-secret-key-here
```

**Configure CORS:**

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:5000", "https://yourdomain.com"]
  }
}
```

Use `["*"]` to allow all origins (development only).

**Rate Limiting:**

Default: 100 requests per minute per IP.

```json
{
  "IpRateLimiting": {
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      }
    ]
  }
}
```

## Integration with SpinMonitor Desktop

### Update Desktop Configuration

Edit `src/SpinMonitor.Desktop/appsettings.json`:

```json
{
  "BackendApi": {
    "Enabled": true,
    "BaseUrl": "http://localhost:5000",
    "ApiKey": null,
    "CheckHealthOnStartup": true
  }
}
```

### Dual Logging

You can enable both MySQL direct and API logging:

```json
{
  "MySQL": {
    "Enabled": true,
    ...
  },
  "BackendApi": {
    "Enabled": true,
    ...
  }
}
```

This provides redundancy during migration.

## Production Deployment

### 1. Publish the Application

```bash
cd src/SpinMonitor.Api
dotnet publish -c Release -o ./publish
```

### 2. Run as a Service (Linux)

Create `/etc/systemd/system/spinmonitor-api.service`:

```ini
[Unit]
Description=SpinMonitor API
After=network.target

[Service]
Type=notify
User=www-data
WorkingDirectory=/var/www/spinmonitor-api
ExecStart=/usr/bin/dotnet /var/www/spinmonitor-api/SpinMonitor.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=spinmonitor-api
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable spinmonitor-api
sudo systemctl start spinmonitor-api
sudo systemctl status spinmonitor-api
```

### 3. Nginx Reverse Proxy

```nginx
server {
    listen 80;
    server_name api.spinmonitor.example.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### 4. Windows Service

Use **NSSM** (Non-Sucking Service Manager):

```cmd
nssm install SpinMonitorAPI "C:\Program Files\dotnet\dotnet.exe"
nssm set SpinMonitorAPI AppDirectory "C:\SpinMonitor\Api"
nssm set SpinMonitorAPI AppParameters "SpinMonitor.Api.dll"
nssm set SpinMonitorAPI DisplayName "SpinMonitor API"
nssm set SpinMonitorAPI Description "SpinMonitor Backend API Service"
nssm start SpinMonitorAPI
```

## Development

### Run with Hot Reload

```bash
dotnet watch run
```

Files are recompiled automatically on save.

### Entity Framework Migrations

If you modify the `Detection` model:

```bash
# Create migration
dotnet ef migrations add MigrationName

# Apply migration
dotnet ef database update
```

### Logging

Logs are written to console via Serilog.

**Configure log levels** in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning"
      }
    }
  }
}
```

## Testing with curl

```bash
# Health check
curl http://localhost:5000/api/health

# Create detection
curl -X POST http://localhost:5000/api/detections \
  -H "Content-Type: application/json" \
  -d '{
    "timestamp": "2025-01-20T12:00:00",
    "stream": "Test Stream",
    "track": "Test Track",
    "confidence": 0.95
  }'

# Get live detections
curl http://localhost:5000/api/detections/live?seconds=120

# Get statistics
curl http://localhost:5000/api/detections/stats?period=day
```

## Troubleshooting

### Database Connection Errors

**Error:** `Unable to connect to any of the specified MySQL hosts`

**Solution:**
1. Verify MySQL is running
2. Check connection string in `appsettings.json`
3. Ensure database and table exist
4. Test connection: `mysql -h localhost -u root -p`

### Port Already in Use

**Error:** `Address already in use`

**Solution:** Change port in `appsettings.json`:

```json
{
  "Urls": "http://localhost:5001"
}
```

### API Key Authentication Failing

Ensure:
1. `EnableApiKeyAuth = true` in `appsettings.json`
2. Client sends `X-API-Key` header
3. Desktop app has matching `ApiKey` in config

## License

MIT

## Support

For issues and questions, see the main SpinMonitor Desktop repository.
