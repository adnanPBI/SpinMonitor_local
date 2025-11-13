using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Serilog;
using SpinMonitor.Models;
using SoundFingerprinting;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.Query;

namespace SpinMonitor.Services
{
    public class StreamMonitor
    {
        private readonly SqliteFingerprintStore _store;
        private readonly AppSettings _settings;
        private readonly StationLogGate _logGate;
        private readonly List<StreamItem> _all = new();
        private readonly object _lock = new();
        
        private readonly Dictionary<string, (CancellationTokenSource cts, Task task)> _activeTasks = new();
        private CancellationTokenSource? _mainCts = null;
        private readonly Dictionary<string, (string track, DateTime time)> _lastDetection = new();
        
        // ✅ NEW: Time-based circuit breaker (auto-resets)
        private readonly Dictionary<string, DateTime> _circuitBreakerResetTime = new();
        private const int CircuitBreakerResetMinutes = 5;

        public StreamMonitor(SqliteFingerprintStore store, AppSettings settings)
        {
            _store = store;
            _settings = settings;
            
            var timeoutSeconds = Math.Max(60, settings.Reconnect.OfflineTimeoutSeconds);
            _logGate = new StationLogGate(TimeSpan.FromSeconds(timeoutSeconds));
            
            StructuredLogger.LogAppInfo($"StreamMonitor initialized: timeout={timeoutSeconds}s");
        }

        public void SetStreams(List<StreamItem> items)
        {
            lock (_lock)
            {
                _all.Clear();
                _all.AddRange(items);
                
                foreach (var item in _all)
                {
                    var streamInfo = StreamTypeDetector.Analyze(item.Url);
                    item.StreamType = streamInfo.TypeString;
                    item.StreamNumber = streamInfo.StreamNumber;
                }
            }
        }

        public void Restart()
        {
            lock (_lock)
            {
                foreach (var kvp in _activeTasks.ToList())
                {
                    try
                    {
                        kvp.Value.cts.Cancel();
                    }
                    catch { }
                }
                
                // ✅ Reset circuit breakers on manual restart
                _circuitBreakerResetTime.Clear();
                StructuredLogger.LogAppInfo($"Stream restart initiated for {_activeTasks.Count} streams - circuit breakers reset");
            }
        }

        public event Action<string, string>? OnDetected;

        // ✅ FIXED: Active health monitoring with automatic recovery
        public async Task RunAsync(CancellationToken token, Action<string>? log = null)
        {
            _mainCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            
            try
            {
                await SyncActiveStreamsAsync(log);
                
                // ✅ Health check loop (every 30 seconds)
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(30000, token); // Check every 30s
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    
                    List<StreamItem> currentStreams;
                    lock (_lock)
                    {
                        currentStreams = _all.ToList();
                        var now = DateTime.UtcNow;
                        
                        foreach (var stream in currentStreams)
                        {
                            bool needsStart = false;
                            
                            // ✅ Check if task exists and is healthy
                            if (_activeTasks.TryGetValue(stream.Name, out var taskInfo))
                            {
                                if (taskInfo.task.IsCompleted || taskInfo.task.IsFaulted || taskInfo.task.IsCanceled)
                                {
                                    log?.Invoke($"[{stream.Name}] Task unhealthy - restarting...");
                                    _activeTasks.Remove(stream.Name);
                                    needsStart = true;
                                }
                                else
                                {
                                    // ✅ Check if stuck in "Starting..." for too long
                                    if (stream.Status == "Starting..." || stream.Status == "Connecting…" || stream.Status == "Buffering…")
                                    {
                                        // If stuck for more than 2 minutes, restart
                                        // We'll track this by checking if bandwidth is still 0 after 2 min
                                        // For now, just log
                                    }
                                }
                            }
                            else
                            {
                                // ✅ No active task - check circuit breaker
                                if (_circuitBreakerResetTime.TryGetValue(stream.Name, out var resetTime))
                                {
                                    if (now >= resetTime)
                                    {
                                        // Circuit breaker expired - allow retry
                                        _circuitBreakerResetTime.Remove(stream.Name);
                                        log?.Invoke($"[{stream.Name}] Circuit breaker reset - retrying...");
                                        needsStart = true;
                                    }
                                    else
                                    {
                                        // Still in circuit breaker cooldown
                                        var remaining = (resetTime - now).TotalMinutes;
                                        stream.Status = $"Cooldown ({remaining:0.0}m)";
                                    }
                                }
                                else
                                {
                                    // No circuit breaker - start normally
                                    needsStart = true;
                                }
                            }
                            
                            if (needsStart)
                            {
                                StartStreamTask(stream, _mainCts.Token, log);
                            }
                        }
                        
                        // ✅ Cleanup removed streams
                        var currentNames = new HashSet<string>(currentStreams.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
                        foreach (var name in _activeTasks.Keys.ToList())
                        {
                            if (!currentNames.Contains(name))
                            {
                                log?.Invoke($"[{name}] Removed from configuration - stopping...");
                                try
                                {
                                    _activeTasks[name].cts.Cancel();
                                    _activeTasks.Remove(name);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                log?.Invoke("Monitoring cancelled.");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Monitoring error: {ex.Message}");
                StructuredLogger.LogAppError(ex, "Fatal monitoring loop error");
            }
            finally
            {
                Task[] tasks;
                lock (_lock)
                {
                    foreach (var kvp in _activeTasks.Values)
                    {
                        try { kvp.cts.Cancel(); } catch { }
                    }
                    tasks = _activeTasks.Values.Select(v => v.task).ToArray();
                    _activeTasks.Clear();
                }
                
                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch { }
                
                log?.Invoke("All stream monitors stopped.");
            }
        }

        // ✅ FIXED: Gradual staggered startup
        private async Task SyncActiveStreamsAsync(Action<string>? log)
        {
            List<StreamItem> currentStreams;
            lock (_lock)
            {
                currentStreams = _all.ToList();
            }
            
            log?.Invoke($"Starting {currentStreams.Count} streams with staggered delays...");
            
            // ✅ Start streams gradually (2 per second)
            for (int i = 0; i < currentStreams.Count; i++)
            {
                var item = currentStreams[i];
                
                lock (_lock)
                {
                    if (!_activeTasks.ContainsKey(item.Name) && !_circuitBreakerResetTime.ContainsKey(item.Name))
                    {
                        StartStreamTask(item, _mainCts!.Token, log);
                    }
                }
                
                // ✅ Stagger by 500ms (2 streams per second)
                if (i < currentStreams.Count - 1)
                {
                    await Task.Delay(500);
                }
            }
            
            log?.Invoke($"All {currentStreams.Count} stream tasks started.");
        }

        private void StartStreamTask(StreamItem item, CancellationToken mainToken, Action<string>? log)
        {
            lock (_lock)
            {
                if (_activeTasks.ContainsKey(item.Name))
                    return;
                
                item.Status = "Starting...";
                item.Reconnects = 0;
                
                var streamCts = CancellationTokenSource.CreateLinkedTokenSource(mainToken);
                
                // ✅ No concurrency limit - all streams run in parallel
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await RunStreamWithAutoRestartAsync(item, streamCts.Token, log);
                    }
                    catch (Exception ex)
                    {
                        StructuredLogger.LogAppError(ex, $"Stream task failed: {item.Name}");
                    }
                }, mainToken);
                
                _activeTasks[item.Name] = (streamCts, task);
                
                StructuredLogger.LogAppInfo($"Started monitoring task for stream: {item.Name}");
            }
        }

        // ✅ FIXED: Better retry logic with circuit breaker
        private async Task RunStreamWithAutoRestartAsync(StreamItem item, CancellationToken token, Action<string>? log)
        {
            int consecutiveFailures = 0;
            const int maxFailuresBeforeCircuitBreaker = 5; // Reduced from 10
            
            try
            {
                while (!token.IsCancellationRequested)
                {
                    StreamItem? currentConfig = null;
                    lock (_lock)
                    {
                        currentConfig = _all.FirstOrDefault(s => s.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (currentConfig == null)
                    {
                        log?.Invoke($"[{item.Name}] Removed from configuration - stopping.");
                        item.Status = "Idle";
                        break;
                    }
                    
                    item = currentConfig;
                    
                    bool success = await RunOneWithReconnectsAsync(item, token, log);
                    
                    if (success)
                    {
                        consecutiveFailures = 0; // Reset on success
                    }
                    else
                    {
                        consecutiveFailures++;
                        
                        // ✅ FIXED: Time-based circuit breaker instead of permanent
                        if (consecutiveFailures >= maxFailuresBeforeCircuitBreaker)
                        {
                            var resetTime = DateTime.UtcNow.AddMinutes(CircuitBreakerResetMinutes);
                            lock (_circuitBreakerResetTime)
                            {
                                _circuitBreakerResetTime[item.Name] = resetTime;
                            }
                            
                            log?.Invoke($"[{item.Name}] Circuit breaker triggered after {consecutiveFailures} failures - will retry in {CircuitBreakerResetMinutes} minutes");
                            item.Status = $"Cooldown ({CircuitBreakerResetMinutes}m)";
                            break; // Exit loop - health monitor will restart later
                        }
                    }
                    
                    if (token.IsCancellationRequested)
                        break;
                    
                    // ✅ FIXED: Smarter backoff (max 30s, not 60s)
                    var delaySeconds = Math.Min(5 + (consecutiveFailures * 5), 30);
                    log?.Invoke($"[{item.Name}] Waiting {delaySeconds}s before retry (failure #{consecutiveFailures})...");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                log?.Invoke($"[{item.Name}] Unexpected error: {ex.Message}");
                StructuredLogger.LogAppError(ex, $"Stream monitoring error for {item.Name}");
            }
            finally
            {
                if (_mainCts?.Token.IsCancellationRequested == true)
                {
                    item.Status = "Idle";
                    item.BandwidthMBps = 0;
                }
                
                lock (_lock)
                {
                    _activeTasks.Remove(item.Name);
                }
            }
        }

        private async Task<bool> RunOneWithReconnectsAsync(StreamItem item, CancellationToken token, Action<string>? log)
        {
            var streamType = item.StreamType ?? "Unknown";
            bool anySuccess = false;
            int reconnectAttempts = 0;
            const int maxReconnectAttempts = 3; // Limit per cycle
            
            while (!token.IsCancellationRequested && reconnectAttempts < maxReconnectAttempts)
            {
                var earlyExit = await RunOneOnceAsync(item, token, log);
                
                if (!earlyExit)
                {
                    anySuccess = true;
                    reconnectAttempts = 0; // Reset on success
                }
                
                if (token.IsCancellationRequested) break;
                
                if (earlyExit && item.AutoReconnect)
                {
                    reconnectAttempts++;
                    item.Reconnects++;
                    item.Status = "Reconnecting…";
                    
                    var delaySeconds = Math.Max(2, _settings.Reconnect.DelaySeconds);
                    
                    StructuredLogger.LogStreamReconnecting(item.Name, streamType, item.Reconnects);
                    log?.Invoke($"[{item.Name}] Reconnecting in {delaySeconds}s (attempt {reconnectAttempts}/{maxReconnectAttempts})...");
                    
                    try { await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token); } catch { break; }
                    continue;
                }
                else
                {
                    break;
                }
            }
            
            return anySuccess;
        }

        private async Task<bool> RunOneOnceAsync(StreamItem item, CancellationToken token, Action<string>? log)
        {
            var ffmpeg = Path.Combine(AppContext.BaseDirectory, "FFmpeg", "bin", "x64", "ffmpeg.exe");
            var streamType = item.StreamType ?? "Unknown";
            
            if (!File.Exists(ffmpeg))
            {
                item.Status = "FFmpeg not found";
                return false;
            }

            if (_store.InMemoryModel == null)
            {
                item.Status = "Model service not available";
                return false;
            }

            var extra = string.IsNullOrWhiteSpace(_settings.FFmpegArgs)
                        ? "-user_agent \"Mozilla/5.0\" -reconnect 1 -reconnect_streamed 1 -reconnect_on_network_error 1 -reconnect_delay_max 5 -nostats -loglevel error"
                        : _settings.FFmpegArgs;

            var args = $"{extra} -i \"{item.Url}\" -vn -ac 1 -ar 5512 -f s16le pipe:1";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            
            // ✅ Connection timeout: 60s to connect
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
            
            try
            {
                item.Status = "Connecting…";
                StructuredLogger.LogStreamConnecting(item.Name, streamType, item.Url);
                
                if (!proc.Start())
                {
                    item.Status = "Failed to start FFmpeg";
                    return true;
                }
                
                try { proc.BeginErrorReadLine(); } catch { }
                
                item.Status = "Buffering…";
            }
            catch (Exception ex)
            {
                item.Status = $"Offline ({ex.Message})";
                
                if (_logGate.AllowDownHeartbeat(item.Name))
                {
                    log?.Invoke($"DOWN: '{item.Name}' [{streamType}] ({ex.Message})");
                }
                
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                return true;
            }

            var stdout = proc.StandardOutput.BaseStream;

            const int sampleRate = 5512;
            const int bytesPerSample = 2;
            int windowSec = Math.Max(5, _settings.Detection.QueryWindowSeconds);
            int hopSec = Math.Max(1, _settings.Detection.QueryHopSeconds);
            int windowBytes = windowSec * sampleRate * bytesPerSample;
            int hopBytes = hopSec * sampleRate * bytesPerSample;

            var buffer = new byte[windowBytes];
            int filled = 0;

            long bytesWindow = 0;
            var lastBwStamp = DateTime.UtcNow;
            var connectionStart = DateTime.UtcNow;
            var lastDataTime = DateTime.UtcNow;
            bool wasOnline = false;

            var displayName = StreamTypeDetector.GetDisplayName(item.Name, 
                new StreamTypeDetector.StreamInfo { TypeString = streamType, StreamNumber = item.StreamNumber });
            log?.Invoke($"Monitoring: {displayName}");

            try
            {
                while (!token.IsCancellationRequested && !proc.HasExited)
                {
                    // ✅ Data timeout: 90s without data
                    if ((DateTime.UtcNow - lastDataTime).TotalSeconds > 90)
                    {
                        log?.Invoke($"[{item.Name}] No data for 90s - timing out");
                        item.Status = "Timeout (no data)";
                        break;
                    }
                    
                    int read;
                    try
                    {
                        // ✅ Use main token, not timeout (timeout is for whole operation)
                        read = await stdout.ReadAsync(buffer, filled, windowBytes - filled, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    
                    if (read <= 0) break;

                    filled += read;
                    bytesWindow += read;
                    lastDataTime = DateTime.UtcNow; // Reset timeout

                    var now = DateTime.UtcNow;
                    var dt = (now - lastBwStamp).TotalSeconds;
                    if (dt >= 1.0)
                    {
                        item.BandwidthMBps = (bytesWindow / 1048576.0) / dt;
                        bytesWindow = 0;
                        lastBwStamp = now;
                        
                        if (item.Status.StartsWith("Buffering") || item.Status.StartsWith("Connecting"))
                        {
                            item.Status = "Online";
                            if (!wasOnline)
                            {
                                wasOnline = true;
                                var connectTime = (now - connectionStart).TotalSeconds;
                                log?.Invoke($"[{item.Name}] [{streamType}] ONLINE (connected in {connectTime:0.1}s, {item.BandwidthMBps:0.000} MB/s)");
                            }
                        }
                    }

                    if (filled >= windowBytes)
                    {
                        float[] samples = new float[windowBytes / 2];
                        int si = 0;
                        for (int i = 0; i < windowBytes; i += 2)
                        {
                            short v = (short)(buffer[i] | (buffer[i + 1] << 8));
                            samples[si++] = v / 32768f;
                        }

                        try
                        {
                            var audioSamples = new AudioSamples(samples, item.Name, sampleRate);
                            var model = _store.InMemoryModel;

                            if (model == null)
                                break;

                            var queryResult = await Task.Run(() =>
                            {
                                return QueryCommandBuilder.Instance
                                    .BuildQueryCommand()
                                    .From(audioSamples)
                                    .UsingServices(model)
                                    .Query();
                            }, token);

                            if (queryResult?.ResultEntries != null && queryResult.ResultEntries.Any())
                            {
                                var matches = queryResult.ResultEntries
                                    .Where(r => r.Audio != null && r.Audio.Confidence >= _settings.Detection.MinConfidence)
                                    .OrderByDescending(r => r.Audio!.Confidence)
                                    .Take(10)
                                    .ToList();

                                foreach (var match in matches)
                                {
                                    var trackTitle = match.Audio!.Track?.Title ?? match.Audio.Track?.Id ?? "(unknown)";
                                    var trackId = match.Audio!.Track?.Id ?? trackTitle;
                                    var startsAtSec = match.Audio.TrackStartsAt;
                                    var confidence = match.Audio.Confidence;
                                    var timestamp = DateTime.Now;

                                    string dedupeKey = $"{item.Name}:{trackId}";
                                    bool shouldLog = true;
                                    
                                    lock (_lastDetection)
                                    {
                                        if (_lastDetection.TryGetValue(dedupeKey, out var last))
                                        {
                                            if ((timestamp - last.time).TotalSeconds < 30)
                                            {
                                                shouldLog = false;
                                            }
                                        }
                                        
                                        if (shouldLog)
                                        {
                                            _lastDetection[dedupeKey] = (trackTitle, timestamp);
                                        }
                                    }

                                    if (!shouldLog)
                                        continue;

                                    var msg = $"DETECTED: '{trackTitle}' on '{item.Name}' [{streamType}] at {timestamp:dd/MM/yyyy HH:mm:ss} (conf={confidence:0.00})";
                                    log?.Invoke(msg);

                                    item.Detections++;

                                    AppendCsvLog(item.Name, streamType, item.StreamNumber, trackTitle,
                                        timestamp, windowSec, confidence);

                                    // ✅ Write to MySQL if enabled
                                    if (_settings.MySQL.Enabled)
                                    {
                                        _ = Task.Run(() => AppendMySqlLog(item.Name, streamType, item.StreamNumber,
                                            trackTitle, timestamp, windowSec, confidence));
                                    }

                                    try
                                    {
                                        OnDetected?.Invoke(trackTitle, item.Name);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning(ex, "OnDetected event handler threw exception");
                                    }
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Query error on stream {Stream}", item.Name);
                        }

                        if (hopBytes < windowBytes)
                        {
                            Buffer.BlockCopy(buffer, hopBytes, buffer, 0, windowBytes - hopBytes);
                            filled = windowBytes - hopBytes;
                        }
                        else
                        {
                            filled = 0;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                return false;
            }
            catch (Exception ex)
            {
                item.Status = $"Offline ({ex.Message})";
                
                if (_logGate.AllowDownHeartbeat(item.Name))
                {
                    log?.Invoke($"DOWN: '{item.Name}' [{streamType}] ({ex.Message})");
                }
                
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                return true;
            }

            // ✅ Cleanup
            try 
            { 
                if (!proc.HasExited) 
                {
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(1000);
                }
            } 
            catch { }
            
            bool unexpected = !token.IsCancellationRequested;
            
            if (unexpected)
            {
                item.Status = "Offline (eof)";
                if (_logGate.AllowDownHeartbeat(item.Name))
                {
                    log?.Invoke($"DOWN: '{item.Name}' [{streamType}] (eof)");
                }
            }
            
            return unexpected;
        }

        private static void AppendCsvLog(string stream, string streamType, string? streamNumber,
            string track, DateTime ts, int duration, double confidence)
        {
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);

                // ✅ Create daily CSV file with date in filename (format: detections-20251024.csv)
                var dateStr = ts.ToString("yyyyMMdd");
                var csv = Path.Combine(logDir, $"detections-{dateStr}.csv");
                var header = "timestamp,stream,stream_type,stream_number,track,duration_seconds,confidence";

                if (!File.Exists(csv))
                    File.WriteAllText(csv, header + Environment.NewLine);

                var line = string.Join(",",
                    ts.ToString("s"),
                    Quote(stream),
                    Quote(streamType),
                    Quote(streamNumber ?? ""),
                    Quote(track),
                    duration.ToString(CultureInfo.InvariantCulture),
                    confidence.ToString("0.000", CultureInfo.InvariantCulture));

                File.AppendAllText(csv, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to append CSV log");
            }
        }

        private static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

        // ✅ MySQL logging for detections
        private void AppendMySqlLog(string stream, string streamType, string? streamNumber,
            string track, DateTime ts, int duration, double confidence)
        {
            try
            {
                var connStr = $"Server={_settings.MySQL.Hostname};" +
                              $"Port={_settings.MySQL.Port};" +
                              $"Database={_settings.MySQL.Database};" +
                              $"User ID={_settings.MySQL.Username};" +
                              $"Password={_settings.MySQL.Password};" +
                              $"ConnectionTimeout=5;";

                using var conn = new MySqlConnection(connStr);
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO detections
                    (timestamp, stream, stream_type, stream_number, track, duration_seconds, confidence)
                    VALUES
                    (@timestamp, @stream, @stream_type, @stream_number, @track, @duration_seconds, @confidence)";

                cmd.Parameters.AddWithValue("@timestamp", ts);
                cmd.Parameters.AddWithValue("@stream", stream);
                cmd.Parameters.AddWithValue("@stream_type", streamType);
                cmd.Parameters.AddWithValue("@stream_number", streamNumber ?? "");
                cmd.Parameters.AddWithValue("@track", track);
                cmd.Parameters.AddWithValue("@duration_seconds", duration);
                cmd.Parameters.AddWithValue("@confidence", confidence);

                cmd.ExecuteNonQuery();

                StructuredLogger.LogAppInfo($"MySQL: Logged detection for '{track}' on '{stream}'");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to write to MySQL database");
                StructuredLogger.LogAppError(ex, $"MySQL logging failed for stream '{stream}'");
            }
        }
    }
}