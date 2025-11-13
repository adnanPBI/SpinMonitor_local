using System;
using System.Diagnostics;

namespace SpinMonitor.Services
{
    /// <summary>
    /// Lightweight overall CPU% sampler for the current process.
    /// </summary>
    public sealed class SystemMonitor : IDisposable
    {
        private readonly System.Threading.Timer _timer;
        private TimeSpan _lastCpu;
        private DateTime _lastWall;
        private readonly Action<double> _cpuCallback;
        private readonly Process _proc = Process.GetCurrentProcess();

        public SystemMonitor(Action<double> cpuPercentCallback, TimeSpan? period = null)
        {
            _cpuCallback = cpuPercentCallback;
            _lastCpu = _proc.TotalProcessorTime;
            _lastWall = DateTime.UtcNow;

            _timer = new System.Threading.Timer(Tick,
                                                state: null,
                                                dueTime: TimeSpan.Zero,
                                                period: period ?? TimeSpan.FromSeconds(1));
        }

        private void Tick(object? _)
        {
            try
            {
                _proc.Refresh();
                var nowCpu  = _proc.TotalProcessorTime;
                var nowWall = DateTime.UtcNow;

                var cpuDeltaMs  = (nowCpu  - _lastCpu ).TotalMilliseconds;
                var wallDeltaMs = (nowWall - _lastWall).TotalMilliseconds;
                if (wallDeltaMs <= 1) return;

                var cores = Environment.ProcessorCount;
                var cpuPct = Math.Max(0, Math.Min(100, cpuDeltaMs / (wallDeltaMs * cores) * 100.0));

                _lastCpu  = nowCpu;
                _lastWall = nowWall;
                _cpuCallback(cpuPct);
            }
            catch
            {
                // swallow; the sampler must never crash the app
            }
        }

        public void Dispose() => _timer.Dispose();
    }
}