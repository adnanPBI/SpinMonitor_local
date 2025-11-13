using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SpinMonitor.Services
{
    /// <summary>
    /// ✅ CRITICAL FIX: Prevents thundering herd problem during mass reconnections.
    /// Limits concurrent connection attempts to avoid overwhelming network/CPU.
    /// </summary>
    public class ConnectionThrottler
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxConcurrent;
        private readonly Random _jitter = new Random();

        public ConnectionThrottler(int maxConcurrentConnections = 10)
        {
            _maxConcurrent = maxConcurrentConnections;
            _semaphore = new SemaphoreSlim(maxConcurrentConnections, maxConcurrentConnections);
        }

        /// <summary>
        /// Throttles connection attempts with semaphore + jitter.
        /// </summary>
        public async Task<T> ExecuteThrottledAsync<T>(Func<Task<T>> action, CancellationToken token)
        {
            // ✅ Add random jitter (0-2000ms) to prevent synchronized reconnections
            var jitterMs = _jitter.Next(0, 2000);
            await Task.Delay(jitterMs, token);

            await _semaphore.WaitAsync(token);
            try
            {
                return await action();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ExecuteThrottledAsync(Func<Task> action, CancellationToken token)
        {
            var jitterMs = _jitter.Next(0, 2000);
            await Task.Delay(jitterMs, token);

            await _semaphore.WaitAsync(token);
            try
            {
                await action();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}
