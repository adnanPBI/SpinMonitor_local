using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace SpinMonitor.Services
{
    /// <summary>
    /// Centralized structured logging service with stream-specific metadata
    /// </summary>
    public static class StructuredLogger
    {
        private static ILogger? _appLogger;
        private static ILogger? _streamLogger;
        private static ILogger? _detectionLogger;
        private static ILogger? _errorLogger;
        private static ILogger? _performanceLogger;

        public static void Initialize(string baseDirectory)
        {
            var logsDir = Path.Combine(baseDirectory, "logs");
            Directory.CreateDirectory(logsDir);

            _appLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(logsDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: 50_000_000,
                    retainedFileCountLimit: 30,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(5))
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _streamLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logsDir, "streams-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{StreamName}] [{StreamType}] {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: 100_000_000,
                    retainedFileCountLimit: 14,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(3))
                .CreateLogger();

            _detectionLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(logsDir, "detections-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {StreamName} | {TrackTitle} | Confidence: {Confidence:0.000} | Starts@: {StartsAt:0.0}s{NewLine}",
                    fileSizeLimitBytes: 50_000_000,
                    retainedFileCountLimit: 60,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(2))
                .CreateLogger();

            _errorLogger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.File(
                    Path.Combine(logsDir, "errors-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{StreamName}] {Message:lj}{NewLine}{Exception}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 50_000_000,
                    retainedFileCountLimit: 90,
                    buffered: false)
                .CreateLogger();

            _performanceLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logsDir, "performance-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {StreamName} | BW: {Bandwidth:0.000} MB/s | CPU: {CpuPercent:0.0}%{NewLine}",
                    fileSizeLimitBytes: 30_000_000,
                    retainedFileCountLimit: 7,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(10))
                .CreateLogger();

            Log.Logger = _appLogger;
        }

        public static void LogAppInfo(string message) 
            => _appLogger?.Information(message);

        public static void LogAppWarning(string message) 
            => _appLogger?.Warning(message);

        public static void LogAppError(Exception ex, string message) 
            => _appLogger?.Error(ex, message);

        public static void LogStreamEvent(string streamName, string streamType, string message, LogEventLevel level = LogEventLevel.Information)
        {
            _streamLogger?.Write(level, "{StreamName} {StreamType} {Message}", streamName, streamType, message);
        }

        public static void LogStreamConnecting(string streamName, string streamType, string url)
        {
            _streamLogger?.Information("CONNECTING to {Url}", url);
            _streamLogger?.Write(LogEventLevel.Information, 
                "[{StreamName}] [{StreamType}] Attempting connection to {Url}", 
                streamName, streamType, url);
        }

        public static void LogStreamOnline(string streamName, string streamType, double bandwidthMBps)
        {
            _streamLogger?.Write(LogEventLevel.Information,
                "[{StreamName}] [{StreamType}] ONLINE | Bandwidth: {Bandwidth:0.000} MB/s",
                streamName, streamType, bandwidthMBps);
        }

        public static void LogStreamOffline(string streamName, string streamType, string reason)
        {
            _streamLogger?.Write(LogEventLevel.Warning,
                "[{StreamName}] [{StreamType}] OFFLINE | Reason: {Reason}",
                streamName, streamType, reason);
        }

        public static void LogStreamReconnecting(string streamName, string streamType, int attemptNumber)
        {
            _streamLogger?.Write(LogEventLevel.Warning,
                "[{StreamName}] [{StreamType}] RECONNECTING (attempt #{Attempt})",
                streamName, streamType, attemptNumber);
        }

        public static void LogDetection(string streamName, string trackTitle, double confidence, double startsAt, DateTime timestamp)
        {
            _detectionLogger?.Information(
                "DETECTED: {TrackTitle} on {StreamName} | Confidence: {Confidence:0.000} | Starts@: {StartsAt:0.0}s | Time: {Timestamp:yyyy-MM-dd HH:mm:ss}",
                trackTitle, streamName, confidence, startsAt, timestamp);
        }

        public static void LogError(string streamName, string streamType, Exception ex, string context)
        {
            _errorLogger?.Error(ex,
                "[{StreamName}] [{StreamType}] ERROR in {Context}: {Message}",
                streamName, streamType, context, ex.Message);
        }

        public static void LogErrorWithDetails(string streamName, string streamType, string errorType, string message, string? stackTrace = null)
        {
            _errorLogger?.Error(
                "[{StreamName}] [{StreamType}] {ErrorType}: {Message}{StackTrace}",
                streamName, streamType, errorType, message, 
                stackTrace != null ? Environment.NewLine + stackTrace : "");
        }

        public static void LogPerformance(string streamName, double bandwidthMBps, double cpuPercent)
        {
            _performanceLogger?.Debug(
                "{StreamName} {Bandwidth} {CpuPercent}",
                streamName, bandwidthMBps, cpuPercent);
        }

        public static void LogIndexingPerformance(int filesProcessed, int filesSkipped, int filesFailed, TimeSpan duration)
        {
            _performanceLogger?.Information(
                "INDEXING COMPLETE | Processed: {Processed} | Skipped: {Skipped} | Failed: {Failed} | Duration: {Duration:0.0}s",
                filesProcessed, filesSkipped, filesFailed, duration.TotalSeconds);
        }

        public static void Shutdown()
        {
            Log.CloseAndFlush();
            (_appLogger as IDisposable)?.Dispose();
            (_streamLogger as IDisposable)?.Dispose();
            (_detectionLogger as IDisposable)?.Dispose();
            (_errorLogger as IDisposable)?.Dispose();
            (_performanceLogger as IDisposable)?.Dispose();
        }
    }
}