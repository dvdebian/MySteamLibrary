using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace MySteamLibrary
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

    // Main logic for the application window
    public partial class MainWindow : Window
    {
        // --- Variables & State ---

        // The collection of games bound to the UI
        public ObservableCollection<SteamGame> Games { get; set; } = new ObservableCollection<SteamGame>();

        // A static list used for fast search operations without filtering the main collection directly
        private List<SteamGame> _allGamesCache;

        // File path where the game list is saved locally
        private string cachePath = "games_cache.json";

        // Stores the time of the last scroll to prevent scroll-wheel spamming
        private DateTime _lastScrollTime = DateTime.MinValue;

        // Timer used to delay search execution until the user stops typing
        private System.Windows.Threading.DispatcherTimer _searchTimer;

        // Dependency property used to bridge WPF animations with ScrollViewer offsets
        public static readonly DependencyProperty ScrollHelperProperty =
            DependencyProperty.Register("ScrollHelper", typeof(double), typeof(MainWindow),
                new PropertyMetadata(0.0, OnScrollHelperChanged));

        // --- Initialization & Setup ---

        // Main constructor: sets up the UI, data binding, and initial data loading
        public MainWindow()
        {
            InitializeComponent();
            GamesListView.ItemsSource = Games;
            ICollectionView view = CollectionViewSource.GetDefaultView(Games);
            view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            LoadGamesFromDisk();

            foreach (var game in Games)
            {
                _ = LoadImageWithCache(game);
            }
            InitializeSearchCache();
        }

        // Copies the current games list to the search cache for high-speed filtering
        private void InitializeSearchCache()
        {
            if (Games != null && Games.Count > 0)
            {
                _allGamesCache = Games.ToList();
            }
        }

        // --- UI Event Handlers (Selection & Scrolling) ---

        // Called when the user selects a game; centers the item and fetches its description
        private void GamesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedGame = GamesListView.SelectedItem as SteamGame;
            if (selectedGame == null) return;

            if (CoverModeBtn.IsChecked == true)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    CenterSelectedItem();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }

            if (string.IsNullOrEmpty(selectedGame.Description))
            {
                _ = FetchGameDescription(selectedGame);
            }
        }

        // Logic to smoothly animate the selected game into the horizontal center of the screen
        private void CenterSelectedItem()
        {
            var scrollViewer = GetScrollViewer(GamesListView);
            var item = GamesListView.SelectedItem;
            if (scrollViewer == null || item == null) return;

            var itemsPresenter = FindVisualChild<ItemsPresenter>(GamesListView);
            var stackPanel = VisualTreeHelper.GetChild(itemsPresenter, 0) as StackPanel;

            var container = GamesListView.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (container != null && stackPanel != null)
            {
                double sideMargin = (scrollViewer.ViewportWidth / 2) - (container.ActualWidth / 2);
                stackPanel.Margin = new Thickness(sideMargin, 0, sideMargin, 0);
                stackPanel.UpdateLayout();

                var transform = container.TransformToAncestor((Visual)scrollViewer.Content);
                Point relativePos = transform.Transform(new Point(0, 0));
                double targetOffset = relativePos.X - (scrollViewer.ViewportWidth / 2) + (container.ActualWidth / 2);

                DoubleAnimation smoothScroll = new DoubleAnimation
                {
                    From = scrollViewer.HorizontalOffset,
                    To = targetOffset,
                    Duration = TimeSpan.FromMilliseconds(450),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                this.BeginAnimation(ScrollHelperProperty, smoothScroll);
            }
        }

        // Maps mouse wheel movement to selecting the next/previous game in Cover Mode
        private void GamesListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (CoverModeBtn.IsChecked == true)
            {
                e.Handled = true;
                if ((DateTime.Now - _lastScrollTime).TotalMilliseconds < 250) return;
                _lastScrollTime = DateTime.Now;

                int newIndex = GamesListView.SelectedIndex;
                if (e.Delta < 0) newIndex++; else newIndex--;

                if (newIndex >= 0 && newIndex < GamesListView.Items.Count)
                {
                    GamesListView.SelectedIndex = newIndex;
                }
            }
        }

        // Deselects an item if it is clicked while already selected
        private void GamesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(GamesListView, e.OriginalSource as DependencyObject) as ListViewItem;
            if (item != null && item.IsSelected)
            {
                GamesListView.SelectedIndex = -1;
                e.Handled = true;
            }
        }

        // Selects a game automatically when the mouse hovers over it in Grid Mode
        private void ListViewItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (GridModeBtn.IsChecked == true && sender is ListViewItem item) item.IsSelected = true;
        }

        // --- Search Logic ---

        // Triggers the search timer when the text in the search box changes
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_searchTimer == null)
            {
                _searchTimer = new System.Windows.Threading.DispatcherTimer();
                _searchTimer.Interval = TimeSpan.FromMilliseconds(200);
                _searchTimer.Tick += (s, args) =>
                {
                    _searchTimer.Stop();
                    PerformSearch();
                };
            }
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        // Filters the game collection based on the search query using background processing
        private async void PerformSearch()
        {
            if (Games == null) return;
            if (_allGamesCache == null) InitializeSearchCache();

            string query = SearchTextBox.Text.Trim().ToLower();

            var filteredResults = await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(query)) return null;
                return new HashSet<SteamGame>(_allGamesCache
                    .Where(g => g.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            });

            var view = CollectionViewSource.GetDefaultView(Games);
            view.Filter = item =>
            {
                if (filteredResults == null) return true;
                return filteredResults.Contains((SteamGame)item);
            };

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                view.Refresh();
                if (!view.IsEmpty)
                {
                    GamesListView.SelectedIndex = 0;
                    if (CoverModeBtn.IsChecked == true) CenterSelectedItem();
                }
                RefreshCount();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // --- Data & API Operations ---

        // Handles image caching: loads from local disk if available, otherwise downloads from Steam
        private async Task LoadImageWithCache(SteamGame game)
        {
            try
            {
                string cacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImageCache");
                if (!Directory.Exists(cacheFolder)) Directory.CreateDirectory(cacheFolder);

                string localPath = Path.Combine(cacheFolder, $"{game.AppId}.jpg");

                if (File.Exists(localPath))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(localPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    game.DisplayImage = bitmap;
                }
                else
                {
                    using (HttpClient client = new HttpClient())
                    {
                        byte[] data = await client.GetByteArrayAsync(game.ImageUrl);
                        await File.WriteAllBytesAsync(localPath, data);
                    }
                    BitmapImage bitmap = new BitmapImage(new Uri(localPath));
                    bitmap.Freeze();
                    game.DisplayImage = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache error for {game.Name}: {ex.Message}");
                game.DisplayImage = game.ImageUrl;
            }
        }

        // Connects to Steam API to fetch the user's list of owned games
        private async void LoadGames_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = Properties.Settings.Default.SteamApiKey;
            string steamId = Properties.Settings.Default.SteamId;

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(steamId))
            {
                MessageBox.Show("Please enter your Steam API Key and Steam ID in Settings first!", "Missing Info");
                return;
            }

            string url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={apiKey}&steamid={steamId}&include_appinfo=true&format=json";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string response = await client.GetStringAsync(url);
                    using (JsonDocument doc = JsonDocument.Parse(response))
                    {
                        if (!doc.RootElement.TryGetProperty("response", out JsonElement resp) ||
                            !resp.TryGetProperty("games", out JsonElement gamesArray)) return;

                        foreach (var el in gamesArray.EnumerateArray())
                        {
                            int id = el.GetProperty("appid").GetInt32();
                            if (Games.Any(g => g.AppId == id)) continue;

                            // 1. Get raw minutes from Steam
                            int minutes = el.TryGetProperty("playtime_forever", out var pt) ? pt.GetInt32() : 0;

                            // 2. Convert to your display string immediately
                            string playtimeString = minutes == 0
                                ? "Not played"
                                : $"{Math.Round(minutes / 60.0, 1)} hours";

                            var newGame = new SteamGame
                            {
                                AppId = id,
                                Name = el.GetProperty("name").GetString(),
                                Playtime = playtimeString, // Save the pre-formatted string
                                ImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{id}/library_600x900.jpg",
                                DisplayImage = null
                            };
                            Games.Add(newGame);
                            _ = LoadImageWithCache(newGame);
                        }
                    }
                }
                SaveGamesToDisk();
                InitializeSearchCache();
                RefreshCount();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // Fetches game descriptions from the Steam Store API
        private async Task FetchGameDescription(SteamGame game)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"https://store.steampowered.com/api/appdetails?appids={game.AppId}";
                    string json = await client.GetStringAsync(url);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement.GetProperty(game.AppId.ToString());
                        if (root.GetProperty("success").GetBoolean())
                        {
                            var data = root.GetProperty("data");
                            string rawDesc = data.GetProperty("short_description").GetString();
                            game.Description = System.Net.WebUtility.HtmlDecode(rawDesc)
                                .Replace("<b>", "").Replace("</b>", "").Replace("<br>", "\n");
                        }
                    }
                }
            }
            catch { game.Description = "Details currently unavailable."; }
        }

        // --- Settings Management ---

        // Opens the settings overlay and populates it with saved values
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            ApiKeyInput.Text = Properties.Settings.Default.SteamApiKey;
            SteamIdInput.Text = Properties.Settings.Default.SteamId;
            SettingsOverlay.Visibility = Visibility.Visible;
        }

        // Saves settings to the local configuration file
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.SteamApiKey = ApiKeyInput.Text.Trim();
            Properties.Settings.Default.SteamId = SteamIdInput.Text.Trim();
            Properties.Settings.Default.Save();
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        // Closes the settings overlay without saving
        private void CancelSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        // Deletes all downloaded images from the local cache folder
        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Delete all locally cached game covers?", "Clear Cache", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string cacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImageCache");
                if (Directory.Exists(cacheFolder))
                {
                    int deletedCount = 0;
                    string[] files = Directory.GetFiles(cacheFolder, "*.jpg");
                    foreach (string file in files)
                    {
                        try { File.Delete(file); deletedCount++; } catch { }
                    }
                    MessageBox.Show($"Cache cleared! Deleted {deletedCount} images.", "Success");
                }
            }
        }

        // --- Utilities & Helper Methods ---

        // Callback used by the ScrollHelper dependency property to apply animation values to the ScrollViewer
        private static void OnScrollHelperChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = d as MainWindow;
            var viewer = window?.GetScrollViewer(window.GamesListView);
            if (viewer != null) viewer.ScrollToHorizontalOffset((double)e.NewValue);
        }

        // Recursively searches the visual tree for a child of a specific type
        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T tChild) return tChild;
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        // Finds the ScrollViewer component within a specific UI object
        private ScrollViewer GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer viewer) return viewer;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        // Recalculates the game count label based on the current filtered view
        private void RefreshCount() => CountLabel.Text = $"{CollectionViewSource.GetDefaultView(Games).Cast<object>().Count()} Games";

        // Saves the current game collection metadata to a JSON file
        private void SaveGamesToDisk() => File.WriteAllText(cachePath, JsonSerializer.Serialize(Games));

        // Loads game collection metadata from the local JSON file
        private void LoadGamesFromDisk()
        {
            if (!File.Exists(cachePath)) return;
            var cached = JsonSerializer.Deserialize<ObservableCollection<SteamGame>>(File.ReadAllText(cachePath));
            if (cached != null)
            {
                foreach (var g in cached) { g.DisplayImage = g.ImageUrl; Games.Add(g); }
                InitializeSearchCache();
                RefreshCount();
            }
        }

        // Handles window resizing to ensure the selected game stays centered
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CoverModeBtn.IsChecked == true && GamesListView.SelectedItem != null)
            {
                Dispatcher.BeginInvoke(new Action(() => { CenterSelectedItem(); }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // Switches between Grid Mode and Cover Mode and resets scroll positions
        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (GamesListView == null || GamesListView.Items.Count == 0) return;
            GamesListView.SelectedIndex = -1;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                var scrollViewer = GetScrollViewer(GamesListView);
                if (scrollViewer != null)
                {
                    if (CoverModeBtn.IsChecked == true) { GamesListView.SelectedIndex = 0; CenterSelectedItem(); }
                    else { scrollViewer.ScrollToTop(); scrollViewer.ScrollToHome(); }
                }
            }));
        }

        // Custom logic to override standard scroll-into-view behavior with centered scrolling
        private void GamesListView_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
            if (CoverModeBtn.IsChecked == true && e.TargetObject is FrameworkElement element)
            {
                var scrollViewer = GetScrollViewer(GamesListView);
                if (scrollViewer == null) return;
                try
                {
                    var transform = element.TransformToAncestor(GamesListView);
                    Point relativePoint = transform.Transform(new Point(0, 0));
                    double offsetDelta = (relativePoint.X + (element.ActualWidth / 2)) - (GamesListView.ActualWidth / 2);
                    double newOffset = Math.Max(0, Math.Min(scrollViewer.HorizontalOffset + offsetDelta, scrollViewer.ScrollableWidth));
                    scrollViewer.ScrollToHorizontalOffset(newOffset);
                }
                catch (InvalidOperationException) { element.BringIntoView(); }
            }
        }

        // Fallback logic for when a game image fails to load (tries headers, then icons, then placeholders)
        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var img = sender as Image;
            var game = img?.DataContext as SteamGame;
            if (game == null) return;
            string currentUrl = game.DisplayImage?.ToString() ?? "";

            if (currentUrl.Contains("library_600x900"))
                game.DisplayImage = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/header.jpg";
            else if (currentUrl.Contains("header.jpg") && !string.IsNullOrEmpty(game.IconUrl))
                game.DisplayImage = game.IconUrl;
            else
                game.DisplayImage = "https://community.cloudflare.steamstatic.com/public/images/applications/store/placeholder.png";

            SaveGamesToDisk();
        }
    }
}