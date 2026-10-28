using Serilog;
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Logging;

namespace TechagroSyncServices.Shared.Services
{
    public abstract class SyncServiceBase : ServiceBase
    {
        private readonly TimeSpan _interval;
        private readonly object _timerLock = new object();

        private Timer _timer;
        private bool _stopping;

        protected SyncServiceBase()
        {
            // Serilog configuration and initialization
            LogConfig.Configure(CommonSettingsLoader.GetLogsExpirationDays());

            _interval = CommonSettingsLoader.GetFetchInterval();
        }

        protected override void OnStart(string[] args)
        {
            _stopping = false;
            _timer = new Timer(
                async _ => await TimerTickAsync(),
                null,
                TimeSpan.Zero,
                Timeout.InfiniteTimeSpan
            );

            Log.Information("Service started. First run immediately. Interval: {Interval}", _interval);
        }

        protected override void OnStop()
        {
            lock (_timerLock)
            {
                _stopping = true;
                _timer?.Dispose();
                _timer = null;
            }

            Log.Information("Service stopped.");
            Log.CloseAndFlush();
        }

        protected abstract Task RunSyncAsync();

        // Hook executed after every run, even when the run failed
        protected virtual void OnSyncFinished() { }

        private async Task TimerTickAsync()
        {
            try
            {
                await RunSyncAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during API synchronization.");
            }
            finally
            {
                OnSyncFinished();
                ScheduleNextRun();
            }
        }

        private void ScheduleNextRun()
        {
            lock (_timerLock)
            {
                if (_stopping || _timer == null)
                    return;

                _timer.Change(_interval, Timeout.InfiniteTimeSpan);
            }

            Log.Information("All processes completed. Next run scheduled at: {NextRun}", DateTime.Now.Add(_interval));
        }
    }
}
