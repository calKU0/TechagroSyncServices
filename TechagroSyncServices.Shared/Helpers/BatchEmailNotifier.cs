using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Services;

namespace TechagroApiSync.Shared.Helpers
{
    public static class BatchEmailNotifier
    {
        public static async Task SendAsync<T>(IReadOnlyList<T> items, int batchSize, Func<IReadOnlyList<T>, string> subjectFactory, Func<IReadOnlyList<T>, string> bodyFactory, string recipients, string from, IEmailService emailService)
        {
            if (!items.Any())
                return;

            int totalBatches = (int)Math.Ceiling(items.Count / (double)batchSize);

            for (int i = 0; i < totalBatches; i++)
            {
                var batch = items
                    .Skip(i * batchSize)
                    .Take(batchSize)
                    .ToList();

                await emailService.SendEmailAsync(recipients, from, subjectFactory(batch), bodyFactory(batch));

                Log.Information("Sent email batch {Batch}/{Total} ({Count} items)", i + 1, totalBatches, batch.Count);
            }
        }
    }
}