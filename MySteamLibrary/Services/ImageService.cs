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
        /// Asynchronously downloads image data from a URL and writes it to the local cache folder.
        /// </summary>
        /// <param name="appId">The Steam Application ID used to name the file.</param>
        /// <param name="imageUrl">The remote URL of the image.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DownloadAndSaveImageAsync(int appId, string imageUrl)
        {
            try
            {
                // Fetch image as byte array to handle binary data correctly
                byte[] data = await _httpClient.GetByteArrayAsync(imageUrl);

                // Write directly to disk using the AppID as the filename
                await File.WriteAllBytesAsync(GetLocalImagePath(appId), data);
            }
            catch (Exception ex)
            {
                // Log failure to the Output window for debugging
                System.Diagnostics.Debug.WriteLine($"Download failed for {appId}: {ex.Message}");
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