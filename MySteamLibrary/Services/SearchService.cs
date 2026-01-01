using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MySteamLibrary.Models;

namespace MySteamLibrary.Services
{
    /// <summary>
    /// Service dedicated to high-performance library filtering.
    /// Uses background tasks to ensure the UI remains responsive during search.
    /// </summary>
    public class SearchService
    {
        /// <summary>
        /// Filters a cached list of games based on a search query.
        /// Performs the search on a background thread to prevent UI lag.
        /// </summary>
        /// <param name="allGames">The full snapshot of the library.</param>
        /// <param name="query">The search text entered by the user.</param>
        /// <returns>A HashSet of matching games for O(1) lookup speed during UI filtering.</returns>
        public async Task<HashSet<SteamGame>> FilterGamesAsync(List<SteamGame> allGames, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            return await Task.Run(() =>
            {
                // Normalize query once
                string cleanQuery = query.Trim().ToLower();

                // Find matches using Case-Insensitive Ordinal comparison (fastest in .NET)
                var matches = allGames.Where(g =>
                    g.Name.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase));

                return new HashSet<SteamGame>(matches);
            });
        }
    }
}
