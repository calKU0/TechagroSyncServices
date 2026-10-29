using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Helpers;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Services;

namespace TechagroSyncServices.Shared.Helpers
{
    // Reports products added by supplier: e-mail with new products, CSV file and snapshot of the whole offer.
    // Errors here never stop synchronization of prices and stocks. When e-mail fails the snapshot is not saved,
    // so the same products are reported again in next run.
    public class NewProductsNotifier
    {
        private const string ExportFolder = "Export";
        private const string NewProductsFolder = "Nowe";
        private const string SnapshotFileName = "products.json";
        private const string NewProductsFileNameFormat = "nowe-produkty-{0}.csv"; // {0} = date
        private const int ProductsPerEmail = 100;

        private readonly IEmailService _emailService;
        private readonly string _supplierName;
        private readonly string _emailSupplierName;
        private readonly int _maxNewProducts;

        // maxNewProducts - more new products than that means broken snapshot rather than real news, so nothing is sent
        public NewProductsNotifier(IEmailService emailService, string supplierName, string emailSupplierName = null, int maxNewProducts = int.MaxValue)
        {
            _emailService = emailService;
            _supplierName = supplierName;
            _emailSupplierName = emailSupplierName ?? supplierName;
            _maxNewProducts = maxNewProducts;
        }

        private static string ExportPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExportFolder);

        // Compares whole offer with previous snapshot. Returns false when report failed and should be repeated.
        // isFetchComplete - offer fetched only partly is never compared nor saved, otherwise the missing products
        // would be reported as new on the next day
        public async Task<bool> DetectAndNotifyAsync(IReadOnlyList<ProductDto> products, bool isFetchComplete)
        {
            if (!isFetchComplete)
            {
                Log.Warning("Offer was not fetched completely. New products will be checked in next run.");
                return false;
            }

            try
            {
                var snapshotPath = Path.Combine(ExportPath, SnapshotFileName);
                var newProducts = await SnapshotChangeDetector.DetectNewAsync(snapshotPath, products, p => p.Code);

                if (!await NotifyAsync(newProducts))
                    return false;

                Log.Information("Exporting product data to json file...");
                await SnapshotChangeDetector.SaveSnapshotAsync(snapshotPath, products);
                Log.Information("JSON file created at {Path}", snapshotPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to detect new products. They will be detected again in next run.");
                return false;
            }
        }

        // Reports products already known to be new. Returns false when report failed and should be repeated.
        public async Task<bool> NotifyAsync(IReadOnlyList<ProductDto> newProducts)
        {
            if (!newProducts.Any())
            {
                Log.Information("No new products detected.");
                return true;
            }

            try
            {
                var to = CommonSettingsLoader.GetEmailsToNotify();
                if (newProducts.Count > _maxNewProducts)
                {
                    // So many new products usually mean a broken snapshot, so they are not e-mailed - the CSV file still lists them
                    Log.Warning("Detected {Count} new products, more than the limit of {Limit}. No email is sent, check the CSV file.", newProducts.Count, _maxNewProducts);
                }
                else if (string.IsNullOrWhiteSpace(to))
                {
                    Log.Information("Detected {Count} new products. EmailsToNotify is empty, so no email is sent.", newProducts.Count);
                }
                else
                {
                    await BatchEmailNotifier.SendAsync(
                        newProducts,
                        ProductsPerEmail,
                        batch => $"Nowe produkty {_supplierName} ({newProducts.Count})",
                        batch => HtmlHelper.BuildNewProductsEmailHtml(batch, _emailSupplierName),
                        recipients: to,
                        from: $"{_supplierName} Sync Service",
                        emailService: _emailService);

                    Log.Information("Detected {Count} new products. Notification email sent to: {Recipients}", newProducts.Count, to);
                }

                var newProductsFolder = Path.Combine(ExportPath, NewProductsFolder);
                var newProductsFileName = string.Format(NewProductsFileNameFormat, DateTime.Today.ToString("dd-MM-yyyy"));
                var exportedFileName = await SnapshotChangeDetector.SaveProductsSnapshotCsvAsync(newProductsFolder, newProductsFileName, newProducts);
                Log.Information("CSV snapshot of new products saved at {Path}", exportedFileName);

                SnapshotChangeDetector.CleanOldSnapshots(newProductsFolder, CommonSettingsLoader.GetFilesExpirationDays());
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send notification about {Count} new products. It will be sent again in next run.", newProducts.Count);
                return false;
            }
        }
    }
}
