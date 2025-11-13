# MySQL Database Setup for SpinMonitor Desktop

Complete guide for setting up MySQL database for SpinMonitor detections logging.

## 📋 Table of Contents

1. [Installation](#installation)
2. [Database Creation](#database-creation)
3. [User Setup](#user-setup)
4. [Table Schema](#table-schema)
5. [Configuration](#configuration)
6. [Testing](#testing)
7. [Troubleshooting](#troubleshooting)

---

## 🔧 Installation

### Option 1: MySQL Community Server (Recommended)

**Download:**
- Windows: https://dev.mysql.com/downloads/mysql/
- Choose "MySQL Installer for Windows"

**Installation Steps:**
1. Run installer
2. Choose "Developer Default" or "Server only"
3. Configure MySQL Server:
   - Port: 3306 (default)
   - Root password: *Choose a strong password*
4. Install MySQL Workbench (optional, for GUI management)

### Option 2: XAMPP (Easy Setup)

**Download:** https://www.apachefriends.org/download.html

**Installation:**
1. Install XAMPP
2. Start MySQL from XAMPP Control Panel
3. Default credentials:
   - Username: `root`
   - Password: *(empty)*
   - Port: `3306`

### Option 3: MariaDB (MySQL Compatible)

**Download:** https://mariadb.org/download/

MariaDB is fully compatible with SpinMonitor.

---

## 💾 Database Creation

### Method 1: Using MySQL Command Line

1. Open Command Prompt or PowerShell
2. Connect to MySQL:

```bash
mysql -u root -p
```

3. Enter your password

4. Create database:

```sql
CREATE DATABASE spinmonitor CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

5. Use the database:

```sql
USE spinmonitor;
```

### Method 2: Using MySQL Workbench

1. Open MySQL Workbench
2. Connect to local MySQL instance
3. Click **Create Schema** icon
4. Enter schema name: `spinmonitor`
5. Charset: `utf8mb4`
6. Collation: `utf8mb4_unicode_ci`
7. Click **Apply**

---

## 👤 User Setup

### Create Dedicated User (Recommended)

**For better security, create a dedicated user instead of using root:**

```sql
-- Create user
CREATE USER 'spinmonitor_user'@'localhost' IDENTIFIED BY 'YourStrongPassword123!';

-- Grant permissions
GRANT SELECT, INSERT, UPDATE, DELETE ON spinmonitor.* TO 'spinmonitor_user'@'localhost';

-- Apply changes
FLUSH PRIVILEGES;
```

### Allow Remote Access (If Needed)

If MySQL is on a different machine:

```sql
CREATE USER 'spinmonitor_user'@'%' IDENTIFIED BY 'YourStrongPassword123!';
GRANT SELECT, INSERT, UPDATE, DELETE ON spinmonitor.* TO 'spinmonitor_user'@'%';
FLUSH PRIVILEGES;
```

**Security Note:** Only allow remote access if absolutely necessary!

---

## 📊 Table Schema

### Create Detections Table

Run this SQL script to create the detections table:

```sql
USE spinmonitor;

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

  -- Indexes for faster queries
  INDEX idx_timestamp (timestamp),
  INDEX idx_stream (stream),
  INDEX idx_track (track(255)),
  INDEX idx_created_at (created_at),
  INDEX idx_confidence (confidence)

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### Verify Table Creation

```sql
DESCRIBE detections;
```

Expected output:
```
+------------------+--------------+------+-----+-------------------+
| Field            | Type         | Null | Key | Default           |
+------------------+--------------+------+-----+-------------------+
| id               | int          | NO   | PRI | NULL              |
| timestamp        | datetime     | NO   | MUL | NULL              |
| stream           | varchar(255) | NO   | MUL | NULL              |
| stream_type      | varchar(50)  | YES  |     | NULL              |
| stream_number    | varchar(50)  | YES  |     | NULL              |
| track            | varchar(500) | YES  | MUL | NULL              |
| duration_seconds | int          | YES  |     | NULL              |
| confidence       | decimal(5,3) | YES  | MUL | NULL              |
| created_at       | timestamp    | YES  | MUL | CURRENT_TIMESTAMP |
+------------------+--------------+------+-----+-------------------+
```

---

## ⚙️ Configuration

### Configure SpinMonitor

Edit `appsettings.json` in SpinMonitor installation folder:

```json
{
  "MySQL": {
    "Enabled": true,
    "Hostname": "localhost",
    "Database": "spinmonitor",
    "Username": "spinmonitor_user",
    "Password": "YourStrongPassword123!",
    "Port": 3306
  }
}
```

### Or Use Settings UI

1. Launch SpinMonitor
2. Go to **File → Settings**
3. Scroll to **MySQL Database Settings**
4. Check **Enable MySQL logging**
5. Fill in connection details:
   - **Hostname:** `localhost` (or IP address)
   - **Port:** `3306`
   - **Database:** `spinmonitor`
   - **Username:** `spinmonitor_user`
   - **Password:** Your password
6. Click **OK**

---

## 🧪 Testing

### Test Connection from Command Line

```bash
mysql -h localhost -P 3306 -u spinmonitor_user -p spinmonitor
```

Enter password when prompted. If successful, you'll see:
```
Welcome to the MySQL monitor.
mysql>
```

### Test from SpinMonitor

1. Configure MySQL settings (see above)
2. Start monitoring streams
3. Wait for a detection
4. Check MySQL for data:

```sql
SELECT * FROM spinmonitor.detections ORDER BY created_at DESC LIMIT 10;
```

### Manual Test Insert

Test table with manual insert:

```sql
INSERT INTO spinmonitor.detections
  (timestamp, stream, stream_type, stream_number, track, duration_seconds, confidence)
VALUES
  (NOW(), 'Test Stream', 'HTTP-MP3', '1', 'Test Track', 10, 0.850);

SELECT * FROM spinmonitor.detections;
```

---

## 🔍 Useful Queries

### View Recent Detections

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

### Count Detections by Stream

```sql
SELECT
  stream,
  COUNT(*) as total_detections,
  AVG(confidence) as avg_confidence
FROM spinmonitor.detections
GROUP BY stream
ORDER BY total_detections DESC;
```

### Today's Detections

```sql
SELECT *
FROM spinmonitor.detections
WHERE DATE(timestamp) = CURDATE()
ORDER BY timestamp DESC;
```

### Top Detected Tracks

```sql
SELECT
  track,
  COUNT(*) as times_detected,
  AVG(confidence) as avg_confidence,
  GROUP_CONCAT(DISTINCT stream) as streams
FROM spinmonitor.detections
GROUP BY track
ORDER BY times_detected DESC
LIMIT 20;
```

### Detections by Hour

```sql
SELECT
  DATE_FORMAT(timestamp, '%Y-%m-%d %H:00') as hour,
  COUNT(*) as detections
FROM spinmonitor.detections
GROUP BY hour
ORDER BY hour DESC
LIMIT 24;
```

---

## 🛠️ Troubleshooting

### Connection Refused

**Error:** "Failed to write to MySQL database"

**Solutions:**

1. **Check MySQL is running:**
   ```bash
   # Windows
   sc query MySQL80

   # Or check Task Manager for mysqld.exe
   ```

2. **Start MySQL service:**
   ```bash
   # Windows
   net start MySQL80

   # Or use Services.msc GUI
   ```

3. **Check port 3306 is open:**
   ```bash
   netstat -an | findstr :3306
   ```

4. **Test connection:**
   ```bash
   telnet localhost 3306
   ```

---

### Access Denied

**Error:** "Access denied for user 'spinmonitor_user'@'localhost'"

**Solutions:**

1. **Verify credentials:**
   ```bash
   mysql -u spinmonitor_user -p
   ```

2. **Check user exists:**
   ```sql
   SELECT User, Host FROM mysql.user WHERE User='spinmonitor_user';
   ```

3. **Grant permissions again:**
   ```sql
   GRANT ALL PRIVILEGES ON spinmonitor.* TO 'spinmonitor_user'@'localhost';
   FLUSH PRIVILEGES;
   ```

---

### Table Doesn't Exist

**Error:** "Table 'spinmonitor.detections' doesn't exist"

**Solution:**

1. **Check database selected:**
   ```sql
   SHOW DATABASES;
   USE spinmonitor;
   SHOW TABLES;
   ```

2. **Create table** (see [Table Schema](#table-schema))

---

### Can't Connect from Remote Machine

**Error:** Connection timeout or refused from remote machine

**Solutions:**

1. **Check MySQL bind address:**

   Edit `my.ini` (Windows) or `my.cnf` (Linux):
   ```ini
   [mysqld]
   bind-address = 0.0.0.0
   ```

2. **Restart MySQL:**
   ```bash
   net stop MySQL80
   net start MySQL80
   ```

3. **Allow firewall:**
   ```bash
   # Windows Firewall
   netsh advfirewall firewall add rule name="MySQL" dir=in action=allow protocol=TCP localport=3306
   ```

4. **Grant remote access:** (see [User Setup](#user-setup))

---

### Slow Inserts

**Problem:** High latency when writing to MySQL

**Solutions:**

1. **Check indexes exist:**
   ```sql
   SHOW INDEX FROM detections;
   ```

2. **Optimize table:**
   ```sql
   OPTIMIZE TABLE detections;
   ```

3. **Increase connection pool** (in SpinMonitor code)

4. **Use SSD for MySQL data directory**

---

## 📊 Monitoring & Maintenance

### Check Database Size

```sql
SELECT
  table_schema AS 'Database',
  ROUND(SUM(data_length + index_length) / 1024 / 1024, 2) AS 'Size (MB)'
FROM information_schema.tables
WHERE table_schema = 'spinmonitor'
GROUP BY table_schema;
```

### Archive Old Data

Keep database size manageable by archiving old detections:

```sql
-- Archive detections older than 90 days
CREATE TABLE detections_archive LIKE detections;

INSERT INTO detections_archive
SELECT * FROM detections
WHERE timestamp < DATE_SUB(NOW(), INTERVAL 90 DAY);

DELETE FROM detections
WHERE timestamp < DATE_SUB(NOW(), INTERVAL 90 DAY);
```

### Backup Database

**Command line backup:**
```bash
mysqldump -u root -p spinmonitor > backup_spinmonitor.sql
```

**Restore from backup:**
```bash
mysql -u root -p spinmonitor < backup_spinmonitor.sql
```

### Automated Backup Script (PowerShell)

Save as `backup-mysql.ps1`:

```powershell
$date = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "C:\Backups\spinmonitor_$date.sql"

& "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe" `
  -u root -p`
  spinmonitor > $backupFile

Write-Host "Backup created: $backupFile"
```

---

## 🔐 Security Best Practices

1. **Use strong passwords** (12+ characters, mixed case, numbers, symbols)
2. **Don't use root user** for applications
3. **Limit user permissions** (only what's needed)
4. **Use localhost** connection when possible
5. **Enable SSL** for remote connections (if needed)
6. **Regular backups** (daily recommended)
7. **Keep MySQL updated** to latest stable version
8. **Monitor logs** for suspicious activity

---

## 📚 Additional Resources

- **MySQL Documentation:** https://dev.mysql.com/doc/
- **MySQL Workbench:** https://dev.mysql.com/downloads/workbench/
- **phpMyAdmin:** https://www.phpmyadmin.net/ (web-based management)
- **HeidiSQL:** https://www.heidisql.com/ (alternative GUI client)

---

**Last Updated:** 2025-11-13
**MySQL Version:** 8.0+
**Compatibility:** MariaDB 10.5+
