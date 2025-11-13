using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SoundFingerprinting;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.Data;

namespace SpinMonitor.Services
{
    public class FingerprintIndexer
    {
        private readonly SqliteFingerprintStore _store;
        private readonly AppSettings _settings;

        public event Action? IndexingCycleCompleted;

        public FingerprintIndexer(SqliteFingerprintStore store, AppSettings settings)
        {
            _store = store;
            _settings = settings;

            var ffDir = Path.Combine(AppContext.BaseDirectory, "FFmpeg", "bin", "x64");
            if (Directory.Exists(ffDir))
            {
                var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!path.Split(Path.PathSeparator).Any(p =>
                        string.Equals(p.TrimEnd('\\'), ffDir, StringComparison.OrdinalIgnoreCase)))
                {
                    Environment.SetEnvironmentVariable("PATH", ffDir + Path.PathSeparator + path);
                }
            }
        }

        public async Task RunOneCycleAsync(Action<string>? progress = null)
        {
            await IndexOnce(progress, CancellationToken.None);
        }

        public async Task RunPeriodicIndexing(CancellationToken ct, Action<string>? progress = null)
{
    Directory.CreateDirectory(_settings.LibraryFolder);
    
    StructuredLogger.LogAppInfo($"Starting periodic indexing. Library folder: {_settings.LibraryFolder}");
    progress?.Invoke($"Starting periodic indexing. Library: {_settings.LibraryFolder}");
    
    // ✅ CRITICAL: Delay first indexing cycle to let streams stabilize
    try
    {
        progress?.Invoke("Delaying first indexing cycle by 60 seconds...");
        await Task.Delay(TimeSpan.FromSeconds(60), ct);
    }
    catch (OperationCanceledException)
    {
        return;
    }
    
    while (!ct.IsCancellationRequested)
    {
        try 
        { 
            await IndexOnce(progress, ct); 
        }
        catch (Exception ex) 
        { 
            StructuredLogger.LogAppError(ex, "Indexing cycle failed");
            Log.Error(ex, "Indexing error"); 
        }
        
        var delayMinutes = Math.Max(1, _settings.RefreshMinutes);
        StructuredLogger.LogAppInfo($"Next indexing cycle in {delayMinutes} minutes");
        await Task.Delay(TimeSpan.FromMinutes(delayMinutes), ct);
    }
}

        private async Task IndexOnce(Action<string>? progress, CancellationToken ct)
        {
            var startTime = DateTime.UtcNow;
            
            // ✅ FIXED: Batch cleanup with memory reload
            int removed = 0;
            var tracksToDelete = new List<(string trackId, string fileName)>();

            // First pass: identify tracks to delete
            foreach (var rec in _store.EnumerateTracks())
            {
                if (!string.IsNullOrEmpty(rec.FilePath) && !File.Exists(rec.FilePath))
                {
                    tracksToDelete.Add((rec.TrackId, Path.GetFileName(rec.FilePath)));
                }
            }

            // Second pass: batch delete from database
            foreach (var (trackId, fileName) in tracksToDelete)
            {
                _store.DeleteTrack(trackId);
                removed++;
                progress?.Invoke($"Deleted fingerprint (missing): {fileName}");
                StructuredLogger.LogAppInfo($"Deleted fingerprint for missing file: {fileName}");
            }

            // ✅ CRITICAL: Reload in-memory model only if tracks were deleted
            if (removed > 0)
            {
                progress?.Invoke($"Removed {removed} entries - reloading memory...");
                StructuredLogger.LogAppInfo($"Removed {removed} fingerprints, reloading in-memory model...");
                
                var reloadStart = DateTime.UtcNow;
                var reloadedCount = _store.ReloadInMemoryModel();
                var reloadTime = (DateTime.UtcNow - reloadStart).TotalSeconds;
                
                progress?.Invoke($"Reloaded {reloadedCount} tracks into memory ({reloadTime:0.1}s).");
                StructuredLogger.LogAppInfo($"In-memory model reloaded: {reloadedCount} tracks in {reloadTime:0.1}s");
            }
            
            var files = Directory.EnumerateFiles(_settings.LibraryFolder, "*.*", SearchOption.AllDirectories)
                                 .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                                          || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                                          || f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
                                 .ToArray();

            StructuredLogger.LogAppInfo($"Indexing cycle started. Found {files.Length} audio files");
            progress?.Invoke($"Scanning library: {files.Length} files found.");

            int processed = 0;
            int skipped = 0;
            int failed = 0;
            int zeroed = 0;

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;

                var fi = new FileInfo(file);
                var trackId = fi.FullName.ToLowerInvariant();

                if (_store.IsTrackUpToDate(trackId, fi.LastWriteTimeUtc))
                {
                    skipped++;
                    continue;
                }

                if (fi.Length == 0)
                {
                    progress?.Invoke($"Skipped (empty): {fi.Name}");
                    skipped++;
                    continue;
                }

                try
                {
                    var decodeStart = DateTime.UtcNow;
                    var samples = await DecodeFileToMonoPcmAsync(file, ct);
                    if (samples == null || samples.Length == 0)
                    {
                        progress?.Invoke($"Skipped (decode failed): {fi.Name}");
                        StructuredLogger.LogAppWarning($"Decode failed for file: {fi.FullName}");
                        failed++;
                        continue;
                    }

                    var decodeTime = (DateTime.UtcNow - decodeStart).TotalSeconds;
                    var fingerprintStart = DateTime.UtcNow;
                    const int sampleRate = 5512;
                    var audio = new AudioSamples(samples, fi.Name, sampleRate);
                    var av = await FingerprintCommandBuilder.Instance
                        .BuildFingerprintCommand()
                        .From(audio)
                        .Hash();

                    if (av == null || av.Audio == null || av.Audio.Count == 0)
                    {
                        progress?.Invoke($"Skipped (no hashes): {fi.Name}");
                        StructuredLogger.LogAppWarning($"No fingerprint hashes generated for file: {fi.FullName}");
                        failed++;
                        continue;
                    }

                    var fingerprintTime = (DateTime.UtcNow - fingerprintStart).TotalSeconds;
                    var track = new TrackInfo(trackId, Path.GetFileNameWithoutExtension(fi.Name), artist: "");
                    _store.UpsertTrackAndHashes(track, av, fi.FullName, fi.LastWriteTimeUtc);
                    
                    var msg = $"Indexed: {fi.Name} ({av.Audio?.Count ?? 0} hashes, decode:{decodeTime:0.1}s, fp:{fingerprintTime:0.1}s)";
                    progress?.Invoke(msg);
                    StructuredLogger.LogAppInfo($"Indexed: {fi.Name} | Hashes: {av.Audio?.Count ?? 0} | Decode: {decodeTime:0.1}s | Fingerprint: {fingerprintTime:0.1}s | Size: {fi.Length / 1024:0}KB");
                    processed++;

                    if (_settings.ZeroOutAfterFingerprint)
                    {
                        try
                        {
                            var ts = fi.LastWriteTimeUtc;
                            var originalSize = fi.Length;
                            
                            using (var fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Write, FileShare.Read))
                                fs.SetLength(0);
                            File.SetLastWriteTimeUtc(fi.FullName, ts);
                            
                            zeroed++;
                            progress?.Invoke($"  → Zeroed: {fi.Name} (saved {originalSize / 1024:0}KB)");
                            StructuredLogger.LogAppInfo($"Zeroed file: {fi.Name} | Saved: {originalSize / 1024:0}KB");
                        }
                        catch (Exception ex)
                        {
                            progress?.Invoke($"  ⚠ Warning: couldn't zero '{fi.Name}': {ex.Message}");
                            StructuredLogger.LogAppWarning($"Failed to zero file {fi.Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    StructuredLogger.LogAppError(ex, $"Failed to index file: {fi.FullName}");
                    Log.Error(ex, "Failed to index {File}", file);
                    progress?.Invoke($"Failed: {fi.Name} - {ex.Message}");
                    failed++;
                }
            }

            var duration = DateTime.UtcNow - startTime;
            var summary = $"Indexing complete: {processed} processed, {skipped} skipped, {failed} failed, {zeroed} zeroed in {duration.TotalSeconds:0.1}s";
            progress?.Invoke(summary);
            
            StructuredLogger.LogIndexingPerformance(processed, skipped, failed, duration);
            StructuredLogger.LogAppInfo(summary);

            IndexingCycleCompleted?.Invoke();
        }

        private async Task<float[]> DecodeFileToMonoPcmAsync(string file, CancellationToken ct)
        {
            var ffmpeg = Path.Combine(AppContext.BaseDirectory, "FFmpeg", "bin", "x64", "ffmpeg.exe");
            
            // ✅ Validate FFmpeg exists
            if (!File.Exists(ffmpeg))
            {
                var error = $"FFmpeg not found at: {ffmpeg}";
                StructuredLogger.LogAppError(new FileNotFoundException(error, ffmpeg), "FFmpeg not found");
                throw new FileNotFoundException(error, ffmpeg);
            }

            var args = $"-v error -nostdin -i \"{file}\" -f s16le -ac 1 -ar 5512 pipe:1";
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            
            try
            {
                // ✅ Better process start handling
                if (!proc.Start())
                {
                    var error = "FFmpeg failed to start (returned false)";
                    StructuredLogger.LogAppError(new InvalidOperationException(error), $"FFmpeg start failed for: {file}");
                    throw new InvalidOperationException(error);
                }

                using var ms = new MemoryStream();
                await proc.StandardOutput.BaseStream.CopyToAsync(ms, ct);

                if (!proc.WaitForExit(5000))
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    StructuredLogger.LogAppWarning($"FFmpeg decode timeout for file: {file}");
                    throw new TimeoutException("FFmpeg decode timeout");
                }

                var pcm = ms.ToArray();
                if (pcm.Length < 2)
                {
                    StructuredLogger.LogAppWarning($"FFmpeg produced no audio data for file: {file}");
                    return Array.Empty<float>();
                }

                int samples = pcm.Length / 2;
                var floats = new float[samples];
                int j = 0;
                for (int i = 0; i < pcm.Length; i += 2)
                {
                    short s = (short)(pcm[i] | (pcm[i + 1] << 8));
                    floats[j++] = s / 32768f;
                }
                return floats;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 0xc00012d)
            {
                // ✅ Handle missing DLL error
                var errorMsg = "FFmpeg missing required DLLs (error 0xc00012d). " +
                              "Install Visual C++ Redistributables (x64) from Microsoft, " +
                              "or download static FFmpeg build from https://www.gyan.dev/ffmpeg/builds/";
                
                StructuredLogger.LogAppError(new InvalidOperationException(errorMsg, ex), 
                    $"FFmpeg DLL dependency error for: {file}");
                
                Log.Error("═══════════════════════════════════════════════════════");
                Log.Error("FFmpeg Error 0xc00012d - MISSING DLL DEPENDENCIES");
                Log.Error("Solution 1: Install Visual C++ Redistributables (x64)");
                Log.Error("Download: https://aka.ms/vs/17/release/vc_redist.x64.exe");
                Log.Error("Solution 2: Use static FFmpeg build (no DLL dependencies)");
                Log.Error("Download: https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip");
                Log.Error("═══════════════════════════════════════════════════════");
                
                throw new InvalidOperationException(errorMsg, ex);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                var errorMsg = $"FFmpeg Win32 error: {ex.Message} (Code: 0x{ex.NativeErrorCode:X})";
                StructuredLogger.LogAppError(ex, $"FFmpeg Win32 error for: {file}");
                throw new InvalidOperationException(errorMsg, ex);
            }
            catch (Exception ex)
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                StructuredLogger.LogAppError(ex, $"FFmpeg decode failed for file: {file}");
                throw;
            }
        }
    }
}