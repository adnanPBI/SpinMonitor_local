# SpinMonitor Backend API

Express.js REST API backend for SpinMonitor Desktop. Provides endpoints for logging and querying stream detection events to a MySQL database.

## Features

- 🎯 RESTful API for stream detection logging
- 🔒 Optional API key authentication
- 📊 Live detections endpoint for real-time monitoring
- 📈 Statistics and analytics endpoints
- 🚀 High-performance connection pooling
- 🛡️ Security middleware (Helmet, CORS, Rate Limiting)
- ⚡ Compression and optimized responses
- 💾 MySQL database integration

## Prerequisites

- **Node.js**: v18.0.0 or higher
- **MySQL**: v5.7+ or v8.0+
- **NPM**: v9.0.0 or higher

## Quick Start

### 1. Install Dependencies

```bash
cd backend
npm install
```

### 2. Configure Environment

Create a `.env` file based on `.env.example`:

```bash
cp .env.example .env
```

Edit `.env` with your configuration:

```env
# Server Configuration
PORT=3000
NODE_ENV=development

# MySQL Database Configuration
DB_HOST=localhost
DB_PORT=3306
DB_NAME=spinmonitor
DB_USER=root
DB_PASSWORD=your_password

# API Configuration (optional)
ENABLE_API_KEY_AUTH=false
API_KEY=your-secret-api-key-here

# CORS Configuration
CORS_ORIGIN=*
```

### 3. Setup MySQL Database

Ensure your MySQL database is created and the `detections` table exists. See the main project's `MYSQL_SETUP.md` for the schema.

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

### 4. Start the Server

Development mode (with auto-reload):
```bash
npm run dev
```

Production mode:
```bash
npm start
```

The server will start on `http://localhost:3000` (or the port specified in `.env`).

## API Endpoints

### Health Check

**GET** `/api/health`

Check if the API and database are healthy.

**Response:**
```json
{
  "success": true,
  "status": "healthy",
  "timestamp": "2025-01-20T12:30:00.000Z",
  "uptime": 123.45,
  "database": "connected"
}
```

### Create Detection

**POST** `/api/detections`

Create a new detection record.

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

Create multiple detection records at once.

**Request Body:**
```json
{
  "detections": [
    {
      "timestamp": "2025-01-20T12:30:45",
      "stream": "Stream 1",
      "track": "Track 1",
      ...
    },
    {
      "timestamp": "2025-01-20T12:31:00",
      "stream": "Stream 2",
      "track": "Track 2",
      ...
    }
  ]
}
```

### Get All Detections

**GET** `/api/detections`

Get paginated detections with optional filtering.

**Query Parameters:**
- `page` - Page number (default: 1)
- `limit` - Results per page (default: 50, max: 1000)
- `stream` - Filter by exact stream name
- `track` - Search in track name (partial match)
- `from` - Start date (ISO 8601 format)
- `to` - End date (ISO 8601 format)
- `min_confidence` - Minimum confidence score
- `sort` - Sort order: `desc` or `asc` (default: desc)

**Example:**
```bash
GET /api/detections?page=1&limit=20&stream=Radio%20Stream%201&min_confidence=0.8
```

**Response:**
```json
{
  "success": true,
  "data": [...],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 1500,
    "totalPages": 75,
    "hasMore": true
  }
}
```

### Get Live Detections

**GET** `/api/detections/live`

Get recent detections (default: last 2 minutes) - perfect for the "Live Now" panel.

**Query Parameters:**
- `seconds` - Time window in seconds (default: 120)

**Example:**
```bash
GET /api/detections/live?seconds=120
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 5678,
      "timestamp": "2025-01-20T12:35:10",
      "stream": "Radio Stream 1",
      "track": "Latest Song",
      "confidence": 0.92,
      ...
    }
  ],
  "count": 15,
  "timeWindow": "120 seconds"
}
```

### Get Statistics

**GET** `/api/detections/stats`

Get detection statistics and analytics.

**Query Parameters:**
- `period` - Time period: `hour`, `day`, `week`, `month`, `year`, `all` (default: day)

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
    "first_detection": "2025-01-20T00:00:00",
    "last_detection": "2025-01-20T12:35:00"
  },
  "topStreams": [
    {"stream": "Radio 1", "detection_count": 234},
    ...
  ],
  "topTracks": [
    {"track": "Popular Song", "play_count": 45, "avg_confidence": 0.91},
    ...
  ]
}
```

### Get Single Detection

**GET** `/api/detections/:id`

Get a specific detection by ID.

### Delete Detection

**DELETE** `/api/detections/:id`

Delete a specific detection by ID.

## Integration with SpinMonitor Desktop

### C# Configuration

Update your `appsettings.json` to enable the Backend API:

```json
{
  "BackendApi": {
    "Enabled": true,
    "BaseUrl": "http://localhost:3000",
    "ApiKey": null,
    "CheckHealthOnStartup": true
  }
}
```

### Configuration Options

- **`Enabled`**: Set to `true` to send detections to the backend API
- **`BaseUrl`**: URL of your backend server
- **`ApiKey`**: Optional API key (must match the backend's `API_KEY` env var if `ENABLE_API_KEY_AUTH=true`)
- **`CheckHealthOnStartup`**: Verify backend connectivity when the app starts

### Dual Logging

You can enable both MySQL and Backend API simultaneously:

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

This provides redundancy and allows you to migrate gradually.

## Security

### API Key Authentication

Enable API key authentication in `.env`:

```env
ENABLE_API_KEY_AUTH=true
API_KEY=your-very-secret-key-here
```

Clients must include the API key in the `X-API-Key` header:

```
X-API-Key: your-very-secret-key-here
```

### Rate Limiting

Default rate limits:
- 100 requests per minute per IP
- Configurable via `RATE_LIMIT_WINDOW_MS` and `RATE_LIMIT_MAX_REQUESTS`

### CORS

Configure allowed origins in `.env`:

```env
CORS_ORIGIN=http://localhost:5000
```

Use `*` to allow all origins (development only).

## Production Deployment

### Using PM2

Install PM2:
```bash
npm install -g pm2
```

Start the server:
```bash
pm2 start server.js --name spinmonitor-api
pm2 save
pm2 startup
```

### Environment Variables

Set `NODE_ENV=production` for production deployments:

```env
NODE_ENV=production
```

This disables verbose error messages in API responses.

### Nginx Reverse Proxy

Example Nginx configuration:

```nginx
server {
    listen 80;
    server_name api.spinmonitor.example.com;

    location / {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

## Monitoring & Logs

### Health Checks

Monitor the `/api/health` endpoint:

```bash
curl http://localhost:3000/api/health
```

### Application Logs

Logs are written to console. In production, redirect to a file:

```bash
node server.js >> /var/log/spinmonitor-api.log 2>&1
```

Or use PM2 for log management:

```bash
pm2 logs spinmonitor-api
```

## Development

### Run with Nodemon

Nodemon automatically restarts the server when files change:

```bash
npm run dev
```

### Project Structure

```
backend/
├── server.js              # Main application entry point
├── config/
│   └── database.js        # MySQL connection pool
├── routes/
│   ├── health.js          # Health check endpoints
│   └── detections.js      # Detection CRUD endpoints
├── package.json           # Dependencies
├── .env                   # Environment configuration
├── .env.example           # Environment template
├── .gitignore            # Git ignore rules
└── README.md             # This file
```

## Troubleshooting

### Database Connection Errors

If you see `ECONNREFUSED` or connection errors:

1. Verify MySQL is running: `systemctl status mysql`
2. Check database credentials in `.env`
3. Ensure the database exists: `CREATE DATABASE spinmonitor;`
4. Verify the `detections` table schema

### Port Already in Use

If port 3000 is already in use, change it in `.env`:

```env
PORT=3001
```

### API Key Authentication Failing

If using API key auth, ensure:
1. `ENABLE_API_KEY_AUTH=true` in `.env`
2. Client sends `X-API-Key` header with matching value
3. C# app has `ApiKey` set in `appsettings.json`

## License

MIT

## Support

For issues and questions, please refer to the main SpinMonitor Desktop repository.
