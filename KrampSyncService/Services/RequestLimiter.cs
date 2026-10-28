using KrampSyncService.DTOs;
using KrampSyncService.Settings;
using Serilog;
using System;
using TechagroSyncServices.Shared.Helpers;

namespace KrampSyncService.Services
{
    public enum KrampEndpoint
    {
        Prices,
        Atp
    }

    // Keeps track of the daily request limit shared by prices and ATP (auth is not counted).
    // Counters are persisted, so service restarts do not reset them.
    public class RequestLimiter
    {
        private readonly KrampApiSettings _settings;
        private readonly string _filePath;
        private readonly object _lock = new object();
        private RequestsState _state;

        public RequestLimiter(KrampApiSettings settings, string filePath)
        {
            _settings = settings;
            _filePath = filePath;
            _state = FileUtils.ReadJson<RequestsState>(filePath);
        }

        public int GetRemaining()
        {
            lock (_lock)
            {
                ResetIfNewDay();
                return Math.Max(0, _settings.DailyRequestsLimit - _state.PricesRequests - _state.AtpRequests);
            }
        }

        // Counts the request before it is sent, failed requests count towards the limit too
        public bool TryConsume(KrampEndpoint endpoint)
        {
            lock (_lock)
            {
                if (GetRemaining() <= 0)
                {
                    Log.Warning("Daily request limit reached ({Limit}).", _settings.DailyRequestsLimit);
                    return false;
                }

                if (endpoint == KrampEndpoint.Prices)
                    _state.PricesRequests++;
                else
                    _state.AtpRequests++;

                FileUtils.WriteJson(_filePath, _state);
                return true;
            }
        }

        public void LogUsage()
        {
            lock (_lock)
            {
                ResetIfNewDay();
                Log.Information("Kramp requests used today: Prices {Prices}, ATP {Atp} (daily limit {Limit}).",
                    _state.PricesRequests, _state.AtpRequests, _settings.DailyRequestsLimit);
            }
        }

        private void ResetIfNewDay()
        {
            if (_state.Date == DateTime.Today)
                return;

            _state = new RequestsState { Date = DateTime.Today };
            FileUtils.WriteJson(_filePath, _state);
        }
    }
}
