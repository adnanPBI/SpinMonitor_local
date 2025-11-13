using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SpinMonitor.Services;
using Serilog;

namespace SpinMonitor
{
    public partial class MainWindow : Window
    {
        private readonly Services.AppSettings _settings;
        private readonly Services.SqliteFingerprintStore _sqlite;
        private readonly Services.FingerprintIndexer _indexer;
        private readonly Services.StreamMonitor _monitor;

        private SystemMonitor? _sysMon;
        private readonly DispatcherTimer _netTimer;
        private System.Threading.Timer? _feedbackCleanupTimer; // ✅ Changed to background timer
        private CancellationTokenSource? _cts;
        private bool _pendingUpdate = false;

        // ✅ NEW: Buffered logging to prevent UI thread blocking
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly DispatcherTimer _logFlushTimer;
        private readonly StringBuilder _logBuffer = new();

        public ObservableCollection<Models.StreamItem> Streams { get; } = new();

        public sealed class FeedbackRow
        {
            public string FileName { get; set; } = "";
            public string StreamName { get; set; } = "";
            public DateTime LastSeen { get; set; } = DateTime.Now;
        }
        public ObservableCollection<FeedbackRow> Feedback { get; } = new();

        private const int UiMetricsPeriodMs = 1000; // ✅ Reduced from 500ms
        private const int FeedbackExpirySeconds = 120;
        private const int LogFlushIntervalMs = 250; // ✅ Batch logs 4x per second

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            var baseDir = AppContext.BaseDirectory;

            StructuredLogger.Initialize(baseDir);
            StructuredLogger.LogAppInfo("=== SpinMonitor Application Starting ===");
            StructuredLogger.LogAppInfo($"Base Directory: {baseDir}");

            var cfgPath = Path.Combine(baseDir, "appsettings.json");
            _settings = Services.AppSettings.Load(cfgPath);
            
            StructuredLogger.LogAppInfo($"Configuration loaded: LibraryFolder={_settings.LibraryFolder}, RefreshMinutes={_settings.RefreshMinutes}");

            _sqlite = new Services.SqliteFingerprintStore(
                Path.Combine(baseDir, _settings.Persistence.SqlitePath));
            _indexer = new Services.FingerprintIndexer(_sqlite, _settings);
            _monitor = new Services.StreamMonitor(_sqlite, _settings);

            _indexer.IndexingCycleCompleted += () =>
            {
                // ✅ Non-blocking UI update
                Dispatcher.BeginInvoke(() =>
                {
                    // ✅ Reset detection counts and clear log on indexing cycle
                    foreach (var stream in Streams)
                    {
                        stream.Detections = 0;
                    }
                    LogBox.Clear();
                    _logQueue.Clear();
                    _logBuffer.Clear();

                    AppendLog("MP3 indexing finished – reloading streams.");
                    StructuredLogger.LogAppInfo("Indexing cycle completed - restarting stream monitor");
                    _monitor.Restart();
                });
            };

            // ✅ FIXED: Non-blocking feedback updates
            _monitor.OnDetected += (fileName, streamName) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    var existing = Feedback.FirstOrDefault(r =>
                        r.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existing == null)
                    {
                        Feedback.Add(new FeedbackRow 
                        { 
                            FileName = fileName, 
                            StreamName = streamName, 
                            LastSeen = DateTime.Now 
                        });
                        StructuredLogger.LogAppInfo($"New detection: {fileName} on {streamName}");
                    }
                    else
                    {
                        existing.StreamName = streamName;
                    }

                    var sorted = Feedback.OrderBy(r => r.FileName, StringComparer.OrdinalIgnoreCase).ToList();
                    Feedback.Clear();
                    foreach (var row in sorted) Feedback.Add(row);
                });
            };

            LoadStreamsFromJson();

            AppendLog("Loading fingerprint cache into memory...");
            StructuredLogger.LogAppInfo("Loading fingerprint database into memory...");
            
            var loadStart = DateTime.UtcNow;
            var loadedCount = _sqlite.LoadAllIntoInMemoryModel();
            var loadTime = (DateTime.UtcNow - loadStart).TotalSeconds;
            
            AppendLog($"Loaded {loadedCount} tracks into in-memory model ({loadTime:0.1}s).");
            StructuredLogger.LogAppInfo($"Fingerprint database loaded: {loadedCount} tracks in {loadTime:0.1}s");
            StatusText.Text = "Ready";

            ValidateFFmpeg(baseDir);

            // ✅ Non-blocking CPU monitor
            _sysMon = new SystemMonitor(pct =>
            {
                Dispatcher.BeginInvoke(() => CpuLabel.Text = $"CPU: {pct:0.#}%");
            }, period: TimeSpan.FromMilliseconds(UiMetricsPeriodMs));

            // ✅ NET timer
            _netTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(UiMetricsPeriodMs)
            };
            _netTimer.Tick += (_, __) =>
            {
                var sum = Streams.Sum(s => s.BandwidthMBps);
                NetLabel.Text = $"NET: {sum:0.000} MB/s";
            };
            _netTimer.Start();

            // ✅ CRITICAL: Move feedback cleanup to background thread
            _feedbackCleanupTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    var cutoff = DateTime.Now.AddSeconds(-FeedbackExpirySeconds);
                    var toRemove = Feedback.Where(f => f.LastSeen < cutoff).ToList();
                    
                    if (toRemove.Count > 0)
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            foreach (var item in toRemove)
                            {
                                Feedback.Remove(item);
                            }
                            StructuredLogger.LogAppInfo($"Removed {toRemove.Count} expired feedback items");
                        });
                    }
                }
                catch (Exception ex)
                {
                    StructuredLogger.LogAppError(ex, "Feedback cleanup failed");
                }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // ✅ CRITICAL: Buffered log flusher
            _logFlushTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(LogFlushIntervalMs)
            };
            _logFlushTimer.Tick += (_, __) => FlushLogBuffer();
            _logFlushTimer.Start();

            StructuredLogger.LogAppInfo("Application initialization complete");
        }

        // ✅ NEW: Non-blocking buffered logging
        private void AppendLog(string message)
        {
            _logQueue.Enqueue($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        // ✅ NEW: Batch flush logs to UI (runs on UI thread)
        private void FlushLogBuffer()
        {
            if (_logQueue.IsEmpty) return;

            _logBuffer.Clear();
            int count = 0;
            const int maxBatch = 50; // Limit batch size

            while (count < maxBatch && _logQueue.TryDequeue(out var msg))
            {
                _logBuffer.AppendLine(msg);
                count++;
            }

            if (_logBuffer.Length > 0)
            {
                LogBox.AppendText(_logBuffer.ToString());
                LogBox.ScrollToEnd();
            }
        }

        private void ValidateFFmpeg(string baseDir)
        {
            var ffmpegPath = Path.Combine(baseDir, "FFmpeg", "bin", "x64", "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                AppendLog($"WARNING: FFmpeg not found at {ffmpegPath}");
                StructuredLogger.LogAppWarning($"FFmpeg not found at {ffmpegPath}");
                return;
            }

            try
            {
                var testProc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                if (testProc.Start())
                {
                    testProc.WaitForExit(2000);
                    if (testProc.ExitCode == 0)
                    {
                        AppendLog("FFmpeg validated successfully.");
                        StructuredLogger.LogAppInfo("FFmpeg validation passed");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"FFmpeg validation error: {ex.Message}");
                StructuredLogger.LogAppError(ex, "FFmpeg validation failed");
            }
        }

        private void LoadStreamsFromJson()
        {
            var streamsPath = Path.Combine(AppContext.BaseDirectory, "streams.json");
            try
            {
                if (!File.Exists(streamsPath))
                {
                    var sample = new[]
                    {
                        new Models.StreamItem { Name="Sample Radio 1", Url="https://example1", IsEnabled=true },
                        new Models.StreamItem { Name="Sample Radio 2", Url="https://example2", IsEnabled=false }
                    };
                    File.WriteAllText(streamsPath, JsonSerializer.Serialize(sample,
                        new JsonSerializerOptions { WriteIndented = true }));
                    
                    StructuredLogger.LogAppInfo($"Created sample streams.json at {streamsPath}");
                }

                var json = File.ReadAllText(streamsPath);
                var loaded = JsonSerializer.Deserialize<Models.StreamItem[]>(json,
                               new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                             ?? Array.Empty<Models.StreamItem>();

                Streams.Clear();
                foreach (var s in loaded) Streams.Add(s);
                
                StructuredLogger.LogAppInfo($"Loaded {loaded.Length} streams from configuration");
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to load streams.json: {ex.Message}");
                StructuredLogger.LogAppError(ex, "Failed to load streams.json");
                Streams.Clear();
            }
        }

        private async Task AutoReloadStreamsAsync(CancellationToken token)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "streams.json");
            DateTime last = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _settings.RefreshMinutes)), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    if (!File.Exists(path)) continue;
                    var now = File.GetLastWriteTimeUtc(path);
                    if (now <= last) continue;
                    last = now;

                    // ✅ Reset detection counts and clear log on stream reload
                    foreach (var stream in Streams)
                    {
                        stream.Detections = 0;
                    }
                    LogBox.Clear();
                    _logQueue.Clear();
                    _logBuffer.Clear();

                    LoadStreamsFromJson();
                    _monitor.SetStreams(Streams.Where(s => s.IsEnabled).ToList());

                    AppendLog("streams.json changed – restarting monitors...");
                    StructuredLogger.LogAppInfo("Detected streams.json change - reloading configuration");
                    _monitor.Restart();
                }
                catch (Exception ex)
                {
                    AppendLog($"Auto-reload streams error: {ex.Message}");
                    StructuredLogger.LogAppError(ex, "Auto-reload streams failed");
                }
            }
        }

        private async void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                AppendLog("Manual update requested - reloading library and streams...");
                StructuredLogger.LogAppInfo("Manual update triggered while monitoring");

                try
                {
                    // ✅ Reset detection counts and clear log
                    foreach (var stream in Streams)
                    {
                        stream.Detections = 0;
                    }
                    LogBox.Clear();
                    _logQueue.Clear();
                    _logBuffer.Clear();

                    await _indexer.RunOneCycleAsync(progress: msg => AppendLog(msg));

                    LoadStreamsFromJson();
                    _monitor.SetStreams(Streams.Where(s => s.IsEnabled).ToList());
                    _monitor.Restart();

                    AppendLog("Update complete - monitoring resumed with updated data.");
                    StructuredLogger.LogAppInfo("Manual update completed - monitoring restarted");
                }
                catch (Exception ex)
                {
                    AppendLog($"Update error: {ex.Message}");
                    StructuredLogger.LogAppError(ex, "Manual update failed");
                }
            }
            else
            {
                _pendingUpdate = true;
                AppendLog("Update scheduled - library and streams will update when monitoring starts.");
                StructuredLogger.LogAppInfo("Manual update scheduled for next monitoring session");
            }
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            ResetBtn.IsEnabled = true;
            StatusText.Text = "Running";
            _cts = new CancellationTokenSource();

            if (_pendingUpdate)
            {
                _pendingUpdate = false;
                AppendLog("Executing scheduled update before starting...");
                
                try
                {
                    await _indexer.RunOneCycleAsync(progress: msg => AppendLog(msg));
                    LoadStreamsFromJson();
                }
                catch (Exception ex)
                {
                    AppendLog($"Update error: {ex.Message}");
                    StructuredLogger.LogAppError(ex, "Scheduled update failed");
                }
            }

            _monitor.SetStreams(Streams.Where(s => s.IsEnabled).ToList());

            var ff = Path.Combine(AppContext.BaseDirectory, "FFmpeg", "bin", "x64", "ffmpeg.exe");
            if (!File.Exists(ff))
            {
                AppendLog("ERROR: FFmpeg not found. Place ffmpeg.exe at FFmpeg/bin/x64.");
                StructuredLogger.LogAppError(new FileNotFoundException("FFmpeg not found", ff), "FFmpeg missing");
                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                ResetBtn.IsEnabled = true;
                StatusText.Text = "Error";
                return;
            }

            var enabledCount = Streams.Count(s => s.IsEnabled);
            StructuredLogger.LogAppInfo($"=== Starting monitoring session with {enabledCount} enabled streams ===");

            var indexTask = _indexer.RunPeriodicIndexing(_cts.Token, progress: msg => AppendLog(msg));
            var monitorTask = _monitor.RunAsync(_cts.Token, log: msg => AppendLog(msg));

            _ = AutoReloadStreamsAsync(_cts.Token);

            try
            {
                await Task.WhenAll(indexTask, monitorTask);
            }
            catch (OperationCanceledException)
            {
                AppendLog("Stopped.");
                StructuredLogger.LogAppInfo("Monitoring session stopped by user");
            }
            catch (Exception ex)
            {
                AppendLog($"Run loop ended with error: {ex.Message}");
                StructuredLogger.LogAppError(ex, "Monitoring session ended with error");
            }
            finally
            {
                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                ResetBtn.IsEnabled = true;
                StatusText.Text = "Stopped";
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e) => Stop();

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Stop();
            StructuredLogger.LogAppInfo("=== Application shutting down ===");
            StructuredLogger.Shutdown();
            System.Windows.Application.Current.Shutdown();
        }

        private void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
            ResetBtn.IsEnabled = true;
            StatusText.Text = "Stopped";
            
            StructuredLogger.LogAppInfo("Monitoring stopped");
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new SettingsWindow(_settings) { Owner = this };
            wnd.ShowDialog();
            _settings.Save(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            StructuredLogger.LogAppInfo("Settings updated");
        }

        private void OpenFingerprintManager_Click(object sender, RoutedEventArgs e)
        {
            var svc = new SpinMonitor.Services.FeedbackService();
            var wnd = new SpinMonitor.Views.FeedbackWindow(svc) { Owner = this };
            wnd.ShowDialog();
        }

        private void OpenDbViewer_Click(object sender, RoutedEventArgs e)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, _settings.Persistence.SqlitePath);
            if (!File.Exists(dbPath))
            {
                System.Windows.MessageBox.Show(this,
                    $"Database not found:\n{dbPath}",
                    "DB Viewer",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var wnd = new SpinMonitor.Views.DbViewerWindow(dbPath) { Owner = this };
            wnd.ShowDialog();
        }

        private void OpenReadme_Click(object sender, RoutedEventArgs e)
        {
            var root = Directory.GetParent(AppContext.BaseDirectory);
            var readme = (root != null) ? Path.Combine(root.FullName, "README.md") : null;
            if (readme != null && File.Exists(readme))
            {
                Process.Start(new ProcessStartInfo { FileName = readme, UseShellExecute = true });
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            _sysMon?.Dispose();
            _netTimer?.Stop();
            _logFlushTimer?.Stop();
            _feedbackCleanupTimer?.Dispose();
            Stop();
            
            StructuredLogger.LogAppInfo("=== Application closed ===");
            StructuredLogger.Shutdown();
        }
    }
}