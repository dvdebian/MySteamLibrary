using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using MySteamLibrary.Models;
using System.IO;            // Fixes the 'File' error
using System.Text.Json;     // Fixes 'JsonSerializer' error

namespace MySteamLibrary.Services
{
    public class SteamService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        // Fetches the game list (Identical to your current logic)
        public async Task<List<SteamGame>> GetGamesFromApiAsync(string apiKey, string steamId)
        {
            string url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={apiKey}&steamid={steamId}&include_appinfo=true&format=json";
            var gamesList = new List<SteamGame>();

            string response = await _httpClient.GetStringAsync(url);
            using (JsonDocument doc = JsonDocument.Parse(response))
            {
                if (!doc.RootElement.TryGetProperty("response", out JsonElement resp) ||
                    !resp.TryGetProperty("games", out JsonElement gamesArray))
                    return gamesList;

                foreach (var el in gamesArray.EnumerateArray())
                {
                    int id = el.GetProperty("appid").GetInt32();
                    int minutes = el.TryGetProperty("playtime_forever", out var pt) ? pt.GetInt32() : 0;

                    gamesList.Add(new SteamGame
                    {
                        AppId = id,
                        Name = el.GetProperty("name").GetString(),
                        Playtime = minutes == 0 ? "Not played" : $"{Math.Round(minutes / 60.0, 1)} hours",
                        ImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{id}/library_600x900.jpg",
                        DisplayImage = null
                    });
                }
            }
            return gamesList;
        }

        // Fetches the description (Identical to your current logic)
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
                        return System.Net.WebUtility.HtmlDecode(rawDesc)
                                .Replace("<b>", "").Replace("</b>", "").Replace("<br>", "\n");
                    }
                }
            }
            catch { }
            return "Details currently unavailable.";
        }

        private readonly string _cachePath = "games_cache.json";

        // Logic moved from MainWindow
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

        // Logic moved from MainWindow
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
    }
}