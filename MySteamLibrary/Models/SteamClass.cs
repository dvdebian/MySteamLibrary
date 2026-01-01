using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MySteamLibrary.Models
{
    /// <summary>
    /// Represents a single Steam game within the library.
    /// Implements INotifyPropertyChanged to ensure the WPF UI updates automatically 
    /// when descriptions or images are loaded asynchronously.
    /// </summary>
    public class SteamGame : INotifyPropertyChanged
    {
        // --- Core Data Properties ---

        /// <summary> The official title of the game. </summary>
        public string Name { get; set; }

        /// <summary> The unique numerical identifier assigned by Steam. </summary>
        public int AppId { get; set; }

        /// <summary> A formatted string representing total play duration (e.g., "10.5 hours"). </summary>
        public string Playtime { get; set; }

        /// <summary> 
        /// URL for the small application icon. 
        /// Used as a backup in the "Waterfall" logic if larger images are missing. 
        /// </summary>
        public string IconUrl { get; set; }

        /// <summary> The primary remote URL for the high-quality portrait artwork (600x900). </summary>
        public string ImageUrl { get; set; }

        // --- Observable Properties (UI Bound) ---

        private string _description;
        /// <summary> 
        /// The textual description of the game. 
        /// Fetched lazily when a game is selected; updates the UI via OnPropertyChanged.
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        private object _displayImage;
        /// <summary> 
        /// The actual source used by the WPF Image control. 
        /// This can be a BitmapImage (local cache) or a string (remote URL).
        /// [JsonIgnore] prevents this complex object from being saved into the JSON database.
        /// </summary>
        [JsonIgnore]
        public object DisplayImage
        {
            get => _displayImage;
            set
            {
                _displayImage = value;
                OnPropertyChanged(nameof(DisplayImage));
            }
        }

        // --- Notification Logic ---

        /// <summary> Event raised when a property value changes. </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Helper method to notify the WPF Binding system that a property has changed.
        /// </summary>
        /// <param name="name">The name of the property that changed.</param>
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}