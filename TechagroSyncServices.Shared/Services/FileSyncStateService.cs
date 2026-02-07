using System;
using System.IO;
using System.Text.Json;
using TechagroApiSync.Shared.DTOs;

namespace TechagroApiSync.Shared.Services
{
    public class FileSyncStateService : ISyncStateService
    {
        private readonly string _filePath = Path.Combine(
            AppContext.BaseDirectory,
            "sync-state.json");

        public int GetLastProductsCount()
        {
            if (!File.Exists(_filePath))
                return 0;

            var json = File.ReadAllText(_filePath);
            var state = JsonSerializer.Deserialize<SyncStateDto>(json);
            return state.LastProductsCount;
        }

        public void SetLastProductsCount(int count)
        {
            var state = new SyncStateDto
            {
                LastProductsCount = count
            };

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_filePath, json);
        }
    }
}
