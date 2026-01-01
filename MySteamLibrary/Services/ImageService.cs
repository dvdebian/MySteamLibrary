using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MySteamLibrary.Services
{
    public class ImageService
    {
        private readonly string _cacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImageCache");
        private readonly HttpClient _httpClient = new HttpClient();

        public ImageService()
        {
            if (!Directory.Exists(_cacheFolder)) Directory.CreateDirectory(_cacheFolder);
        }

        // Gets the local file path for a game cover
        public string GetLocalImagePath(int appId)
        {
            return Path.Combine(_cacheFolder, $"{appId}.jpg");
        }

        // Checks if the image exists locally
        public bool DoesImageExistLocally(int appId)
        {
            return File.Exists(GetLocalImagePath(appId));
        }

        // Downloads the image and saves it to disk
        public async Task DownloadAndSaveImageAsync(int appId, string imageUrl)
        {
            try
            {
                byte[] data = await _httpClient.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(GetLocalImagePath(appId), data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Download failed for {appId}: {ex.Message}");
            }
        }

        // Logic moved from ClearCache_Click
        public int ClearAllCachedImages()
        {
            if (!Directory.Exists(_cacheFolder)) return 0;

            int deletedCount = 0;
            string[] files = Directory.GetFiles(_cacheFolder, "*.jpg");
            foreach (string file in files)
            {
                try { File.Delete(file); deletedCount++; } catch { }
            }
            return deletedCount;
        }
    }
}
