using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using MySteamLibrary.Models;
using System.IO;

namespace MySteamLibrary.Services
{
    /// <summary>
    /// Service responsible for communicating with Steam APIs (Web API and Store API)
    /// and managing the local persistence of game metadata.
    /// </summary>
    public class SteamService
    {
        /// <summary> Static HttpClient instance to be reused across the application lifecycle. </summary>
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary> The filename used for storing cached game metadata. </summary>
        private readonly string _cachePath = "games_cache.json";

        /// <summary>
        /// Fetches the list of owned games for a specific user from the Steam Web API.
        /// Constructs fallback URLs for icons and handles JSON parsing.
        /// </summary>
        /// <param name="apiKey">The user's unique Steam Web API Key.</param>
        /// <param name="steamId">The 64-bit Steam ID of the user.</param>
        /// <returns>A list of SteamGame objects populated with basic info and image URLs.</returns>
        public async Task<List<SteamGame>> GetGamesFromApiAsync(string apiKey, string steamId)
        {
            string url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={apiKey}&steamid={steamId}&include_appinfo=true&format=json";
            var gamesList = new List<SteamGame>();

            try
            {
                string response = await _httpClient.GetStringAsync(url);
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    // Check if 'response' or 'games' property exists to avoid null errors
                    if (!doc.RootElement.TryGetProperty("response", out JsonElement resp) ||
                        !resp.TryGetProperty("games", out JsonElement gamesArray))
                        return gamesList;

                    foreach (var el in gamesArray.EnumerateArray())
                    {
                        int id = el.GetProperty("appid").GetInt32();
                        int minutes = el.TryGetProperty("playtime_forever", out var pt) ? pt.GetInt32() : 0;

                        // Extract icon hash provided by Steam to build a direct image link
                        string iconHash = el.TryGetProperty("img_icon_url", out var iconProp) ? iconProp.GetString() : "";

                        // Construct the full URL for the small application icon
                        string iconUrl = string.IsNullOrEmpty(iconHash)
                            ? ""
                            : $"https://media.steampowered.com/steamcommunity/public/images/apps/{id}/{iconHash}.jpg";

                        gamesList.Add(new SteamGame
                        {
                            AppId = id,
                            Name = el.GetProperty("name").GetString(),
                            // Format playtime into a readable string
                            Playtime = minutes == 0 ? "Not played" : $"{Math.Round(minutes / 60.0, 1)} hours",
                            // Primary high-quality portrait target
                            ImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{id}/library_600x900.jpg",
                            IconUrl = iconUrl,
                            DisplayImage = null
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API Fetch failed: {ex.Message}");
            }

            return gamesList;
        }

        /// <summary>
        /// Fetches detailed game information (specifically short_description) from the Steam Store API.
        /// Includes HTML decoding and basic tag removal for clean UI display.
        /// </summary>
        /// <param name="appId">The Steam Application ID.</param>
        /// <returns>A cleaned string containing the game description.</returns>
        public async Task<string> FetchGameDescriptionAsync(int appId)
        {
            try
            {
                string url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
                string json = await _httpClient.GetStringAsync(url);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement.GetProperty(appId.ToString());
                    if (root.GetProperty("success").GetBoolean())
                    {
                        var data = root.GetProperty("data");
                        string rawDesc = data.GetProperty("short_description").GetString();

                        // Clean up the description: Decode HTML entities and remove common Steam tags
                        return System.Net.WebUtility.HtmlDecode(rawDesc)
                                .Replace("<b>", "").Replace("</b>", "").Replace("<br>", "\n");
                    }
                }
            }
            catch { }
            return "Details currently unavailable.";
        }

        /// <summary>
        /// Serializes the provided game collection into a JSON file for offline access.
        /// </summary>
        /// <param name="games">The collection of games to save.</param>
        public void SaveGamesToDisk(IEnumerable<SteamGame> games)
        {
            try
            {
                string json = JsonSerializer.Serialize(games);
                File.WriteAllText(_cachePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save games: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads and deserializes the game collection from the local JSON file.
        /// </summary>
        /// <returns>A list of cached SteamGame objects or an empty list if file doesn't exist.</returns>
        public List<SteamGame> LoadGamesFromDisk()
        {
            if (!File.Exists(_cachePath)) return new List<SteamGame>();

            try
            {
                string json = File.ReadAllText(_cachePath);
                return JsonSerializer.Deserialize<List<SteamGame>>(json) ?? new List<SteamGame>();
            }
            catch
            {
                return new List<SteamGame>();
            }
        }

        /// <summary>
        /// Deletes the local JSON metadata file. Part of the 'Full Reset' functionality.
        /// </summary>
        public void DeleteGamesCache()
        {
            if (File.Exists(_cachePath))
            {
                File.Delete(_cachePath);
            }
        }
    }
}