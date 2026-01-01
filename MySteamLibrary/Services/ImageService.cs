using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MySteamLibrary.Services
{
    /// <summary>
    /// Service responsible for managing the local image cache.
    /// Handles directory creation, image availability checks, downloads, and cache cleanup.
    /// </summary>
    public class ImageService
    {
        /// <summary> The absolute path to the folder where images are stored. </summary>
        private readonly string _cacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImageCache");

        /// <summary> Reusable HttpClient for downloading image data. </summary>
        private readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Initializes a new instance of the ImageService and ensures the cache directory exists.
        /// </summary>
        public ImageService()
        {
            if (!Directory.Exists(_cacheFolder))
            {
                Directory.CreateDirectory(_cacheFolder);
            }
        }

        /// <summary>
        /// Constructs the local file path for a specific game based on its Steam AppID.
        /// </summary>
        /// <param name="appId">The unique Steam Application ID.</param>
        /// <returns>A full string path to the expected .jpg file.</returns>
        public string GetLocalImagePath(int appId)
        {
            return Path.Combine(_cacheFolder, $"{appId}.jpg");
        }

        /// <summary>
        /// Determines if a cover image for the given AppID is already stored on the disk.
        /// </summary>
        /// <param name="appId">The Steam Application ID to check.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        public bool DoesImageExistLocally(int appId)
        {
            return File.Exists(GetLocalImagePath(appId));
        }

        /// <summary>
        /// Asynchronously downloads image data. 
        /// Checks the HTTP response status to handle 404 errors gracefully without throwing exceptions.
        /// </summary>
        /// <param name="appId">The Steam AppID used for the filename.</param>
        /// <param name="imageUrl">The remote URL to download.</param>
        /// <returns>True if the download succeeded and was saved; otherwise, false.</returns>
        public async Task<bool> DownloadAndSaveImageAsync(int appId, string imageUrl)
        {
            try
            {
                // Use GetAsync to check the headers/status before downloading the whole body
                using (HttpResponseMessage response = await _httpClient.GetAsync(imageUrl))
                {
                    // If Steam returns 404 (Not Found) or 500 (Server Error), exit early
                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"Download skipped for {appId}: Server returned {response.StatusCode}");
                        return false;
                    }

                    // If success, read the actual bytes and save to disk
                    byte[] data = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(GetLocalImagePath(appId), data);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network error for {appId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes all .jpg files within the ImageCache folder.
        /// Part of the 'Full Reset' functionality to refresh the library view.
        /// </summary>
        /// <returns>The total number of files successfully deleted.</returns>
        public int ClearAllCachedImages()
        {
            if (!Directory.Exists(_cacheFolder)) return 0;

            int deletedCount = 0;
            // Only target JPG files to avoid deleting other potential cache files
            string[] files = Directory.GetFiles(_cacheFolder, "*.jpg");

            foreach (string file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch
                {
                    // Silently fail if a file is currently locked by the UI
                }
            }
            return deletedCount;
        }
    }
}