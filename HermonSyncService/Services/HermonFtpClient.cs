using CsvHelper;
using FluentFTP;
using HermonSyncService.DTOs;
using HermonSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace HermonSyncService.Services
{
    public class HermonFtpClient : IDisposable
    {
        private readonly HermonFtpSettings _settings;
        private readonly FtpClient _client;
        private bool _connected = false;

        public HermonFtpClient(HermonFtpSettings settings)
        {
            _settings = settings;
            _client = CreateFtpClient();
        }

        private FtpClient CreateFtpClient()
        {
            var client = new FtpClient(_settings.BaseUrl, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            client.Config.EncryptionMode = FtpEncryptionMode.None;
            client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
            client.Config.ConnectTimeout = 10000;
            client.Config.ReadTimeout = 10000;
            client.Config.DataConnectionConnectTimeout = 10000;
            client.Config.DataConnectionReadTimeout = 10000;
            client.Config.RetryAttempts = 2;
            client.Config.LocalFileBufferSize = 64 * 2048;
            client.Config.DownloadDataType = FtpDataType.Binary;
            client.Config.DownloadRateLimit = 0;

            return client;
        }

        private void EnsureConnected()
        {
            if (!_connected || !_client.IsConnected)
            {
                Log.Information("Connecting to FTP server {Host}:{Port}", _settings.BaseUrl, _settings.Port);
                _client.Connect();
                _connected = true;
            }
        }

        // -----------------------------
        // Download a CSV file from FTP
        // -----------------------------
        public List<T> DownloadCsv<T>(string fileName, Encoding encoding = null)
        {
            Log.Information("Downloading CSV from FTP: {File}", fileName);

            EnsureConnected();

            Stream stream = null;
            StreamReader reader = null;
            CsvReader csv = null;

            try
            {
                var fileEncoding = encoding ?? Encoding.GetEncoding("windows-1250");
                stream = _client.OpenRead(fileName);
                reader = new StreamReader(stream, encoding ?? fileEncoding);

                var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ";",
                    Quote = '\0',
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    Mode = CsvMode.NoEscape,
                    BadDataFound = args =>
                    {
                        Log.Warning("Bad data found at field {Row}: {RawRecord}", args.Field, args.RawRecord);
                    },
                    IgnoreBlankLines = true,
                    Encoding = encoding ?? fileEncoding
                };

                csv = new CsvReader(reader, config);
                var records = csv.GetRecords<T>().ToList();

                Log.Information("Downloaded and parsed {Count} records from {File}", records.Count, fileName);
                return records;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error downloading CSV from FTP: {File}", fileName);
                return new List<T>();
            }
            finally
            {
                csv?.Dispose();
                reader?.Dispose();
                stream?.Dispose();
            }
        }

        // --------------------------------------
        // List files in a given FTP folder
        // --------------------------------------
        public List<string> ListFiles(string folderPath)
        {
            EnsureConnected();

            Log.Information("Listing files from FTP folder: {Folder}", folderPath);

            try
            {
                var files = _client.GetListing(folderPath)
                    .Where(f => f.Type == FtpObjectType.File)
                    .Select(f => f.Name)
                    .ToList();

                Log.Information("Found {Count} files in {Folder}", files.Count, folderPath);
                return files;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error listing FTP folder: {Folder}", folderPath);
                return new List<string>();
            }
        }

        // --------------------------------------
        // Download image files from FTP folder
        // --------------------------------------
        public List<FtpImage> DownloadImages(string folderPath)
        {
            EnsureConnected();

            var fileNames = ListFiles(folderPath);
            var imageFiles = fileNames.Where(f =>
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            Log.Information("Downloading {Count} images from {Folder}", imageFiles.Count, folderPath);

            var images = new List<FtpImage>();
            int count = 0;

            string localDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImagesCache");
            Directory.CreateDirectory(localDir);

            foreach (var file in imageFiles)
            {
                try
                {
                    string localPath = Path.Combine(localDir, file);

                    using (var ftpStream = _client.OpenRead(folderPath + "/" + file))
                    using (var fs = File.Create(localPath))
                    {
                        ftpStream.CopyTo(fs);
                    }

                    images.Add(new FtpImage
                    {
                        FileName = file,
                        FilePath = localPath,
                        Data = null
                    });

                    count++;
                    if (count % 50 == 0)
                        Log.Information("Downloaded {Count}/{Total} images", count, imageFiles.Count);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to download image {File}", file);
                }
            }

            return images;
        }

        public void Dispose()
        {
            try
            {
                if (_client.IsConnected)
                {
                    Log.Information("Disconnecting from FTP server...");
                    _client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during FTP disconnect");
            }
            finally
            {
                _client.Dispose();
            }
        }
    }
}