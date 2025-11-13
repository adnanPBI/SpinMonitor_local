# SpinMonitor Desktop

Audio fingerprinting monitor for radio streams - Windows 10/11 Desktop Edition

## 🎯 Overview

SpinMonitor Desktop is a standalone Windows application that monitors multiple radio streams simultaneously, identifies songs using audio fingerprinting technology (similar to Shazam), and logs detections to MySQL database and CSV files.

## ✨ Features

- **Real-time Audio Fingerprinting** - Identifies songs playing on radio streams
- **Multi-Stream Monitoring** - Monitor multiple streams concurrently
- **MySQL Database Logging** - Direct logging to MySQL database
- **Daily CSV Export** - Creates daily CSV files with all detections
- **Live Detection View** - Real-time display of currently playing tracks
- **Fingerprint Management** - View and manage your audio fingerprint library
- **Performance Optimized** - Efficient resource usage with connection throttling
- **Auto-Recovery** - Automatic reconnection with circuit breaker protection

## 📋 Requirements

### System Requirements
- **OS:** Windows 10 (version 1809 or later) or Windows 11
- **RAM:** 4GB minimum, 8GB recommended for 50+ streams
- **CPU:** 4+ cores recommended
- **Disk Space:** 2GB for application + storage for fingerprint database
- **.NET:** .NET 9.0 Runtime (included in self-contained build)

### External Dependencies
- **FFmpeg** - Required for audio decoding (download script included)
- **MySQL Server** - For database logging (optional, can use CSV only)

## 🚀 Quick Start

### 1. Download and Extract

Download the latest release and extract to your desired location:
```
C:\SpinMonitor\
```

### 2. Install FFmpeg

Run the included PowerShell script:
```powershell
.\get-ffmpeg.ps1
```

Or download manually from: https://github.com/BtbN/FFmpeg-Builds/releases

Extract `ffmpeg.exe` to:
```
C:\SpinMonitor\FFmpeg\bin\x64\ffmpeg.exe
```

### 3. Configure MySQL (Optional)

If you want to use MySQL logging:

1. Create database:
```sql
CREATE DATABASE spinmonitor;
USE spinmonitor;

CREATE TABLE detections (
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
  INDEX idx_track (track(255))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

2. Edit `appsettings.json`:
```json
{
  "MySQL": {
    "Enabled": true,
    "Hostname": "localhost",
    "Database": "spinmonitor",
    "Username": "root",
    "Password": "your_password",
    "Port": 3306
  }
}
```

Or configure through: **File → Settings → MySQL Database Settings**

### 4. Prepare Audio Library

1. Create a folder for your MP3 library:
```
C:\SpinMonitor\library\
```

2. Copy your MP3 files to this folder

3. Configure in **File → Settings**:
   - Set Library Folder path
   - Adjust refresh interval (default: 5 minutes)

### 5. Configure Streams

Edit `streams.json` to add your radio streams:

```json
[
  {
    "Name": "My Radio Station",
    "Url": "https://stream.myradio.com/live",
    "IsEnabled": true
  }
]
```

### 6. Run the Application

1. Double-click `SpinMonitor.Desktop.exe`
2. Click **Start Monitoring**
3. View detections in the **Live Now** panel
4. Check logs in the **Real-time log** panel

## 📂 Directory Structure

```
C:\SpinMonitor\
├── SpinMonitor.Desktop.exe     # Main application
├── appsettings.json             # Configuration file
├── streams.json                 # Radio stream list
├── get-ffmpeg.ps1               # FFmpeg download script
├── FFmpeg\
│   └── bin\x64\ffmpeg.exe      # Audio decoder
├── library\                     # Your MP3 files for fingerprinting
├── data\
│   └── fingerprints.db         # SQLite fingerprint database
└── logs\
    ├── app-YYYYMMDD.log        # Application logs
    ├── detections-YYYYMMDD.csv # Daily detection CSV
    ├── streams-YYYYMMDD.log    # Stream connection logs
    ├── errors-YYYYMMDD.log     # Error logs
    └── performance-YYYYMMDD.log # Performance metrics
```

## ⚙️ Configuration

### Detection Settings

Adjust in **File → Settings → Audio Detection Parameters**:

- **Minimum Confidence** (0.0-1.0): Higher = fewer false positives (default: 0.2)
- **Query Window** (seconds): Audio chunk size for analysis (default: 10)
- **Query Hop** (seconds): Overlap between chunks (default: 5)

### Stream Reconnection

Configure automatic reconnection behavior:

- **Reconnect Delay** (seconds): Wait time before reconnection (default: 60)
- **Offline Timeout** (seconds): Max time before circuit breaker (default: 300)

### MySQL Settings

Enable/disable database logging and configure connection:

- **Enabled**: Toggle MySQL logging
- **Hostname**: MySQL server address
- **Port**: MySQL port (default: 3306)
- **Database**: Database name
- **Username**: MySQL username
- **Password**: MySQL password

## 📊 Using the Application

### Main Window Layout

```
┌─────────────────────────────────────────────────────────────┐
│ File Menu                                                    │
├──────────────────┬──────────────────────────────────────────┤
│ Radio Streams    │ Live Now                                 │
│                  │  ┌────────────────────────────────────┐  │
│ Name | Status    │  │ Track Name    | Stream | Time     │  │
│ ──────────────── │  └────────────────────────────────────┘  │
│ Radio1 | Online  │                                           │
│ Radio2 | Online  │  Real-time Log                           │
│ Radio3 | Offline │  ┌────────────────────────────────────┐  │
│                  │  │ [10:30:15] Monitoring: Radio1...   │  │
│                  │  │ [10:30:16] DETECTED: Song X...     │  │
│                  │  └────────────────────────────────────┘  │
└──────────────────┴──────────────────────────────────────────┘
│ Status: Running                  CPU: 25%  NET: 5.2 MB/s   │
└─────────────────────────────────────────────────────────────┘
```

### Menu Options

- **File → Settings**: Configure all application settings
- **File → Manage Fingerprints**: View fingerprint library and creation dates
- **File → DB Table Viewer**: Browse SQLite database tables
- **File → Exit**: Close application

### Buttons

- **Start Monitoring**: Begin stream monitoring
- **Stop Monitoring**: Stop all streams
- **Reset**: Reload streams and clear counters

## 🔍 Monitoring Detections

### Live Now Panel

Shows currently detected tracks:
- **Track Name**: Identified song/audio
- **Stream**: Which radio stream detected it
- **Time**: When it was detected

Tracks auto-expire after 2 minutes.

### Real-time Log

Shows all application events:
- Stream connection status
- Detection events with confidence scores
- Errors and warnings
- Performance metrics

### CSV Export

Daily files created in `logs/detections-YYYYMMDD.csv`:

```csv
timestamp,stream,stream_type,stream_number,track,duration_seconds,confidence
2025-11-13T10:30:15,Radio1,HTTP-MP3,1,Song Name,10,0.850
```

### MySQL Database

Detections logged to `detections` table in real-time when enabled.

## 🛠️ Troubleshooting

### FFmpeg Not Found

**Error:** "FFmpeg not found. Place ffmpeg.exe at FFmpeg/bin/x64."

**Solution:**
1. Download FFmpeg from: https://github.com/BtbN/FFmpeg-Builds/releases
2. Extract `ffmpeg.exe` to `FFmpeg\bin\x64\`
3. Or run `.\get-ffmpeg.ps1`

### MySQL Connection Failed

**Error:** "Failed to write to MySQL database"

**Solution:**
1. Verify MySQL is running: `mysql -u root -p`
2. Check hostname/port in Settings
3. Verify credentials
4. Test connection: `mysql -h localhost -u root -p -D spinmonitor`
5. Check firewall allows MySQL port (3306)

### No Detections

**Problem:** Streams running but no songs detected

**Solution:**
1. Verify MP3 files in library folder
2. Wait for indexing to complete (check Real-time log)
3. Lower confidence threshold in Settings (e.g., 0.15)
4. Increase query window to 15 seconds
5. Ensure MP3 quality is good (not heavily compressed)

### High CPU Usage

**Problem:** CPU at 80-100%

**Solution:**
1. Reduce number of enabled streams
2. Increase query hop interval (more overlap = less CPU)
3. Ensure only one instance running
4. Check for stuck FFmpeg processes in Task Manager

### Streams Stay Offline

**Problem:** Streams show "Offline" status

**Solution:**
1. Check stream URL in browser
2. Verify internet connection
3. Some streams block automated access - try different User-Agent
4. Check logs/streams-YYYYMMDD.log for errors
5. Increase offline timeout in Settings

## 📈 Performance Tips

### For Many Streams (50+)

1. Increase RAM allocation (8GB+ recommended)
2. Use SSD for database and logs
3. Disable MySQL logging for better performance
4. Increase query hop interval to 7-10 seconds
5. Use connection throttling (already built-in)

### For Low-Spec Systems

1. Monitor fewer streams (10-20)
2. Increase query window to 15 seconds
3. Disable real-time log display (minimize window)
4. Use CSV-only logging (disable MySQL)

## 🔐 Security Best Practices

1. **Never share appsettings.json** - Contains MySQL password
2. **Use strong MySQL passwords**
3. **Run as standard user** (not Administrator)
4. **Keep Windows updated**
5. **Use antivirus software**
6. **Back up fingerprint database regularly**

## 🆘 Getting Help

### Log Files

Check these files for debugging:
- `logs/app-YYYYMMDD.log` - General application events
- `logs/errors-YYYYMMDD.log` - Error details
- `logs/streams-YYYYMMDD.log` - Stream connection issues

### Common Issues

See the **Troubleshooting** section above.

### Support

For issues or questions:
1. Check the logs folder
2. Review the troubleshooting section
3. Open an issue on GitHub (if available)

## 📝 License

Licensed under Apache 2.0 License.

## 🙏 Acknowledgments

- **SoundFingerprinting** - Audio fingerprinting library
- **FFmpeg** - Audio encoding/decoding
- **Serilog** - Logging framework
- **MySqlConnector** - MySQL database driver

---

**Version:** 1.0.0
**Last Updated:** 2025-11-13
**Platform:** Windows 10/11 (x64)
**Framework:** .NET 9.0
