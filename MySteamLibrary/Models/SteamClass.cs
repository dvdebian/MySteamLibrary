using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MySteamLibrary.Models
{
    // Class representing a single Steam game and its properties
    public class SteamGame : INotifyPropertyChanged
    {
        // --- Variables / Properties ---

        // The name of the game
        public string Name { get; set; }

        // The unique Steam Application ID
        public int AppId { get; set; }

        // Formatted string showing how long the game has been played
        public string Playtime { get; set; }

        // URL for the small square icon
        public string IconUrl { get; set; }

        // URL for the large library artwork
        public string ImageUrl { get; set; }

        // Backing field for the game description
        private string _description;

        // The textual description of the game with change notification
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        // Backing field for the actual image displayed in the UI
        private object _displayImage;

        // The image object (Bitmap or URL) used by the UI; ignored during JSON serialization
        [JsonIgnore]
        public object DisplayImage
        {
            get => _displayImage;
            set { _displayImage = value; OnPropertyChanged(nameof(DisplayImage)); }
        }

        // --- Notification Logic ---

        // Event triggered when a property value changes to update the UI
        public event PropertyChangedEventHandler PropertyChanged;

        // Helper method to raise the PropertyChanged event
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
