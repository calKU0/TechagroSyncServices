using HiFiSyncService.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TechagroSyncServices.Tests.Unit
{
    [TestClass]
    public class RequestThrottlerTests
    {
        [TestMethod]
        public async Task Requests_AreSpacedByPerMinuteLimit()
        {
            // 600 requests per minute = one request every 100 ms
            var throttler = new RequestThrottler(600);
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < 4; i++)
                await throttler.RunAsync(() => Task.FromResult(i));

            Assert.IsGreaterThanOrEqualTo(290L, stopwatch.ElapsedMilliseconds);
        }

        [TestMethod]
        public async Task Postpone_DelaysNextRequest()
        {
            var throttler = new RequestThrottler(6000);
            throttler.Postpone(TimeSpan.FromMilliseconds(300));
            var stopwatch = Stopwatch.StartNew();

            await throttler.RunAsync(() => Task.FromResult(0));

            Assert.IsGreaterThanOrEqualTo(250L, stopwatch.ElapsedMilliseconds);
        }
    }
}
