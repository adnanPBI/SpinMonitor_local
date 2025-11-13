# SpinMonitor Desktop - Quick Start Guide

## 🚀 5-Minute Setup

Get SpinMonitor running in 5 minutes!

### Step 1: Download & Extract (1 minute)

1. Download latest release: `SpinMonitor-Desktop-v1.0.0.zip`
2. Extract to: `C:\SpinMonitor\`

### Step 2: Install FFmpeg (2 minutes)

**Option A: Automatic (Recommended)**
```powershell
cd C:\SpinMonitor
.\get-ffmpeg.ps1
```

**Option B: Manual**
1. Download: https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip
2. Extract `ffmpeg.exe` to: `C:\SpinMonitor\FFmpeg\bin\x64\`

### Step 3: Add Music Library (1 minute)

1. Create folder: `C:\SpinMonitor\library\`
2. Copy your MP3 files to this folder
3. At least 50+ MP3s recommended for good detection

### Step 4: Configure Streams (Optional, 1 minute)

Edit `streams.json`:

```json
[
  {
    "Name": "Your Favorite Radio",
    "Url": "https://stream.example.com/radio.mp3",
    "IsEnabled": true
  }
]
```

**Popular stream formats supported:**
- HTTP-MP3: `http://stream.example.com/radio.mp3`
- HLS: `https://stream.example.com/playlist.m3u8`
- Icecast: `http://icecast.example.com:8000/radio`

### Step 5: Run! (30 seconds)

1. Double-click `SpinMonitor.Desktop.exe`
2. Click **Start Monitoring**
3. Watch detections appear in **Live Now** panel!

---

## 🗄️ MySQL Setup (Optional)

### Quick MySQL Setup (5 minutes)

**1. Install MySQL** (if not already installed)
- Download: https://dev.mysql.com/downloads/mysql/
- Or use XAMPP: https://www.apachefriends.org/

**2. Create Database**

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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

**3. Configure SpinMonitor**

In SpinMonitor:
1. Go to **File → Settings**
2. Scroll to **MySQL Database Settings**
3. Check **Enable MySQL logging**
4. Enter connection details:
   - Hostname: `localhost`
   - Port: `3306`
   - Database: `spinmonitor`
   - Username: `root`
   - Password: *your password*
5. Click **OK**

**4. Verify**

After a detection occurs:
```sql
SELECT * FROM spinmonitor.detections ORDER BY created_at DESC LIMIT 10;
```

---

## 📊 First Time Usage

### What You'll See

```
┌─────────────────────────────────────────────┐
│ SpinMonitor - Audio Stream Monitor         │
├──────────────┬──────────────────────────────┤
│ Radio Streams│ Live Now                     │
│              │ ┌─────────────────────────┐  │
│ Radio 1      │ │ Song X | Radio 1 |10:30│  │
│ ✓ Online     │ │ Song Y | Radio 2 |10:31│  │
│ Detect: 5    │ └─────────────────────────┘  │
│              │                               │
│ Radio 2      │ Real-time Log                │
│ ✓ Online     │ ┌─────────────────────────┐  │
│ Detect: 3    │ │[10:30] DETECTED: Song X │  │
│              │ │[10:31] DETECTED: Song Y │  │
│              │ └─────────────────────────┘  │
└──────────────┴──────────────────────────────┘
```

### Understanding the Interface

**Radio Streams (Left Panel)**
- Shows all configured streams
- **Status:** Online/Offline/Connecting
- **Detect:** Number of songs detected
- **MB/s:** Bandwidth usage

**Live Now (Top Right)**
- Currently playing detected songs
- Auto-expires after 2 minutes
- Shows stream source and detection time

**Real-time Log (Bottom Right)**
- All application events
- Detection notifications
- Error messages
- Connection status

---

## 🎯 Common Tasks

### Adding a New Stream

1. Open `streams.json` in Notepad
2. Add new entry:
   ```json
   {
     "Name": "New Radio Station",
     "Url": "https://stream.newradio.com/live.mp3",
     "IsEnabled": true
   }
   ```
3. Save file
4. Click **Reset** button in SpinMonitor (auto-reloads within 5 minutes)

### Viewing Detections

**CSV Files:**
- Location: `C:\SpinMonitor\logs\detections-20251113.csv`
- Opens in Excel
- Daily file created automatically

**MySQL Database:**
```sql
SELECT
  timestamp,
  stream,
  track,
  ROUND(confidence, 2) as conf
FROM spinmonitor.detections
ORDER BY timestamp DESC
LIMIT 20;
```

### Adjusting Detection Sensitivity

1. Go to **File → Settings**
2. Under **Audio Detection Parameters**:
   - **Lower confidence** (e.g., 0.15) = more detections, more false positives
   - **Higher confidence** (e.g., 0.30) = fewer detections, higher accuracy
3. Default: 0.20 (recommended)

### Managing Library

**Add More Songs:**
1. Copy MP3 files to `library\` folder
2. Wait for automatic re-indexing (default: every 5 minutes)
3. Or click **Reset** to force immediate re-index

**View Fingerprints:**
1. Go to **File → Manage Fingerprints**
2. Shows all indexed tracks and creation dates

---

## 🔧 Troubleshooting

### No Detections?

**Checklist:**
- [ ] MP3 files in `library\` folder?
- [ ] At least 50+ MP3 files?
- [ ] Good quality MP3s (not heavily compressed)?
- [ ] Streams actually playing music?
- [ ] Wait 5-10 minutes for indexing to complete

**Try:**
- Lower confidence threshold to 0.15
- Increase query window to 15 seconds
- Check logs/app-*.log for errors

### FFmpeg Error?

**Error:** "FFmpeg not found"

**Fix:**
1. Run `.\get-ffmpeg.ps1`
2. Or download manually and extract to `FFmpeg\bin\x64\`
3. Verify: `FFmpeg\bin\x64\ffmpeg.exe -version`

### Streams Offline?

**Checklist:**
- [ ] Internet connection working?
- [ ] Stream URL correct?
- [ ] Stream not blocked by firewall?

**Try:**
- Open stream URL in browser (should prompt to download/play)
- Check `logs\streams-*.log` for error details
- Try different stream URL

### High CPU Usage?

**Normal:** 20-40% with 10-20 streams

**High (80%+):**
- Too many streams enabled (reduce to 10-20)
- Increase query hop interval (Settings → 7 seconds)
- Close other CPU-intensive applications

---

## 📈 Performance Tips

### For Best Results:

**Library:**
- 100-500 MP3s is ideal
- Use high-quality MP3s (192+ kbps)
- Avoid heavily compressed files
- Remove duplicate tracks

**Streams:**
- Start with 5-10 streams
- Add more gradually
- Monitor CPU usage (<50% is good)

**Detection:**
- Confidence 0.20 = balanced
- Confidence 0.15 = more detections
- Confidence 0.25 = fewer, more accurate

**System:**
- 8GB+ RAM recommended for 20+ streams
- SSD improves database performance
- Close unnecessary applications

---

## 🆘 Need Help?

### Log Files Location

```
C:\SpinMonitor\logs\
├── app-20251113.log          # Application events
├── detections-20251113.csv   # All detections (Excel)
├── streams-20251113.log      # Stream connections
├── errors-20251113.log       # Errors only
└── performance-20251113.log  # CPU/memory metrics
```

### Check Logs First!

Most issues are explained in the log files.

### Still Stuck?

1. Read full **README.md** for detailed guide
2. Check **MYSQL_SETUP.md** for database issues
3. Review **BUILD.md** if building from source

---

## 🎉 You're Ready!

**Next Steps:**
1. Start monitoring
2. Wait for first detection (can take 5-15 minutes)
3. Check `detections-*.csv` file
4. Enjoy automated stream monitoring!

**Pro Tips:**
- Let it run 24/7 for best results
- Check detections daily
- Back up fingerprint database weekly
- Update stream list monthly

---

**Version:** 1.0.0
**Platform:** Windows 10/11
**Support:** See README.md for detailed documentation
