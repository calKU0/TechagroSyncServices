using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;
using static System.Net.Mime.MediaTypeNames;

namespace TechagroSyncServices.Shared.Helpers
{
    public static class HtmlHelper
    {
        public static string BuildNewProductsEmailHtml(IReadOnlyList<ProductDto> products, string serviceName)
        {
            string GetImageSrc(ImageDto img)
            {
                if (!string.IsNullOrEmpty(img.Url))
                    return img.Url;

                return null;
            }

            string productBlocks = string.Join("", products.Select(product =>
            {
                var imagesHtml = string.Join("",
                    product.Images
                        .Select(img =>
                        {
                            var src = GetImageSrc(img);
                            if (string.IsNullOrEmpty(src))
                                return "";

                            return $@"
                                <img src='{src}'
                                 alt='Zdjęcie'
                                 style='max-width:110px; max-height:110px; margin:4px; border-radius:6px; border:1px solid #ddd;' />";
                        })
                );

                var tradingCodeRow = string.IsNullOrWhiteSpace(product.TradingCode)
                    ? ""
                    : $@"
                        <tr>
                            <td style='padding:6px; font-weight:bold;'>Nr. Handlowy:</td>
                            <td style='padding:6px;'>{product.TradingCode}</td>
                        </tr>";

                var nameRow = string.IsNullOrWhiteSpace(product.Name) || product.IntegrationCompany == IntegrationCompany.INTERCARS
                    ? ""
                    : $@"
                        <tr>
                            <td style='padding:6px; font-weight:bold;'>Nazwa:</td>
                            <td style='padding:6px;'>{product.Name}</td>
                        </tr>";

                return $@"
                    <div style='border:1px solid #e1e1e1; padding:15px; border-radius:8px; margin-bottom:20px; background-color:#fafafa;'>
                        <h3 style='margin:0; color:#D70000; font-size:18px;'>Nowy produkt: {product.Code}</h3>
                        <p style='margin:6px 0 12px 0; color:#555;'>{product.Brand}</p>

                        <table style='width:100%; border-collapse:collapse; font-size:14px;'>
                            {nameRow}
                            {tradingCodeRow}
                            <tr>
                                <td style='padding:6px; font-weight:bold;'>Dostępność:</td>
                                <td style='padding:6px;'>{product.Quantity} szt.</td>
                            </tr>
                            <tr style='background-color:#f2f2f2;'>
                                <td style='padding:6px; font-weight:bold;'>Cena hurtowa:</td>
                                <td style='padding:6px;'>{product.NetBuyPrice:0.00} zł</td>
                            </tr>
                        </table>

                        <h4 style='margin-top:15px; color:#333;'>Opis:</h4>
                        <p style='color:#555; line-height:1.5;'>{product.Description}</p>

                        <div style='margin-top:10px;'>{imagesHtml}</div>
                    </div>";
            }));

            return $@"
                <html>
                <head><meta charset='UTF-8' /></head>
                <body style='font-family:Arial, sans-serif; background-color:#f7f7f7; padding:20px;'>
                    <div style='max-width:800px; margin:auto; background:white; padding:25px; border-radius:10px;
                                box-shadow:0 2px 10px rgba(0,0,0,0.1);'>

                        <h2 style='color:#D70000; margin-bottom:10px;'>
                            Wykryto nowe produkty w ofercie {serviceName}
                        </h2>

                        <p style='font-size:15px; color:#555;'>
                            Poniżej znajduje się lista wszystkich nowych pozycji wykrytych podczas ostatniej synchronizacji.
                        </p>

                        {productBlocks}

                        <hr style='margin-top:30px; border:none; border-top:1px solid #eee;' />

                        <p style='font-size:12px; color:#aaa;'>
                            Automatyczna wiadomość wygenerowana przez system synchronizacji {serviceName}.
                        </p>
                    </div>
                </body>
                </html>";
        }
    }
}