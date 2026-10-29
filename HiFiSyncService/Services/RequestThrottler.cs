using System;
using System.Threading;
using System.Threading.Tasks;

namespace HiFiSyncService.Services
{
    // Sends requests one by one and spaces them to stay under the per-minute limit.
    // HIFI API bans API keys that keep sending requests while being rate limited.
    public class RequestThrottler
    {
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly TimeSpan _minInterval;
        private readonly object _lock = new object();
        private DateTime _nextRequestAt = DateTime.MinValue;

        public RequestThrottler(int requestsPerMinute)
        {
            _minInterval = TimeSpan.FromMilliseconds(60000.0 / Math.Max(1, requestsPerMinute));
        }

        public async Task<T> RunAsync<T>(Func<Task<T>> request)
        {
            await _gate.WaitAsync();
            try
            {
                var delay = GetDelay();
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay);

                return await request();
            }
            finally
            {
                Postpone(_minInterval);
                _gate.Release();
            }
        }

        // Moves the next request back, e.g. after 429 Retry-After or when the RPM budget is used up
        public void Postpone(TimeSpan delay)
        {
            lock (_lock)
            {
                var next = DateTime.UtcNow + delay;
                if (next > _nextRequestAt)
                    _nextRequestAt = next;
            }
        }

        private TimeSpan GetDelay()
        {
            lock (_lock)
            {
                return _nextRequestAt - DateTime.UtcNow;
            }
        }
    }
}
