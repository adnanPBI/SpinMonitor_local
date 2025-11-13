using System;
using System.Collections.Concurrent;

namespace SpinMonitor.Services
{
    public sealed class StationLogGate
    {
        private readonly TimeSpan _downCooldown;
        private readonly ConcurrentDictionary<string, (string lastStatus, DateTime lastDownUtc)> _state = new();

        public StationLogGate(TimeSpan? downCooldown = null)
            => _downCooldown = downCooldown ?? TimeSpan.FromMinutes(5);

        public bool AllowDetection(string key) => true;

        public bool AllowStatus(string key, string status)
        {
            var cur = _state.AddOrUpdate(key,
                _ => (status, DateTime.MinValue),
                (_, prev) => (status, prev.lastDownUtc));
            return cur.lastStatus != status;
        }

        public bool AllowDownHeartbeat(string key)
        {
            var now = DateTime.UtcNow;
            var cur = _state.AddOrUpdate(
                key,
                _ => ("down", now),
                (_, prev) =>
                {
                    if (now - prev.lastDownUtc >= _downCooldown)
                        return (prev.lastStatus, now);
                    return prev;
                });
            return now - cur.lastDownUtc < TimeSpan.FromSeconds(2);
        }
    }
}