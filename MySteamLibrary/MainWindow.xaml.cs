using MySteamLibrary.Helpers;
using MySteamLibrary.Models;
using MySteamLibrary.Services;
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
    /// <summary>
    /// Interaction logic for MainWindow.xaml. 
    /// Manages the primary UI, user interactions, and coordinates data flow between services.
    /// </summary>
    public partial class MainWindow : Window
    {
        // --- Services ---

        /// <summary> Handles local disk operations for game cover images. </summary>
        private readonly ImageService _imageService = new ImageService();

        /// <summary> Handles Steam API communication and metadata persistence. </summary>
        private readonly SteamService _steamService = new SteamService();

        // --- Data Collections ---

        /// <summary> The primary collection of games bound to the UI. Supports real-time updates. </summary>
        public ObservableCollection<SteamGame> Games { get; set; } = new ObservableCollection<SteamGame>();

        /// <summary> A static list used for fast search operations to avoid threading issues with the UI collection. </summary>
        private List<SteamGame> _allGamesCache;

        // --- State Management ---

        /// <summary> File path for the local JSON database. </summary>
        private string cachePath = "games_cache.json";

        /// <summary> Tracks the timestamp of the last scroll event to throttle mouse wheel input. </summary>
        private DateTime _lastScrollTime = DateTime.MinValue;

        /// <summary> Timer used to 'debounce' search input, preventing performance lag while typing. </summary>
        private System.Windows.Threading.DispatcherTimer _searchTimer;

        /// <summary> 
        /// DependencyProperty used as a bridge to allow WPF Storyboards/Animations 
        /// to control the ScrollViewer's HorizontalOffset. 
        /// </summary>
        public static readonly DependencyProperty ScrollHelperProperty =
            DependencyProperty.Register("ScrollHelper", typeof(double), typeof(MainWindow),
                new PropertyMetadata(0.0, OnScrollHelperChanged));

        // --- Initialization ---

        /// <summary>
        /// Initializes the MainWindow, sets up DataBinding, and loads existing data from disk.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Link the UI list to our ObservableCollection
            GamesListView.ItemsSource = Games;

            // Set up default sorting by Game Name (A-Z)
            ICollectionView view = CollectionViewSource.GetDefaultView(Games);
            view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            // Load saved games and start background image loading
            LoadGamesFromDisk();

            foreach (var game in Games)
            {
                _ = LoadImageWithCache(game);
            }
            InitializeSearchCache();
        }

        /// <summary>
        /// Populates the search cache with a snapshot of the current game list.
        /// </summary>
        private void InitializeSearchCache()
        {
            if (Games != null && Games.Count > 0)
            {
                _allGamesCache = Games.ToList();
            }
        }

        // --- UI Event Handlers (Selection & Scrolling) ---

        /// <summary>
        /// Handles selection changes. Triggers centering logic and lazy-loads game descriptions.
        /// </summary>
        private async void GamesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedGame = GamesListView.SelectedItem as SteamGame;
            if (selectedGame == null) return;

            // Automatically center the game if in Cover Mode
            if (CoverModeBtn.IsChecked == true)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    CenterSelectedItem();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }

            // Lazy-load the description if it hasn't been fetched yet
            if (string.IsNullOrEmpty(selectedGame.Description))
            {
                selectedGame.Description = await _steamService.FetchGameDescriptionAsync(selectedGame.AppId);
            }
        }

        /// <summary>
        /// Calculates the required offset to place the selected item in the middle of the viewport
        /// and executes a smooth CubicEase animation to scroll there.
        /// </summary>
        private void CenterSelectedItem()
        {
            var scrollViewer = UIHelper.GetScrollViewer(GamesListView);
            var item = GamesListView.SelectedItem;
            if (scrollViewer == null || item == null) return;

            var itemsPresenter = UIHelper.FindVisualChild<ItemsPresenter>(GamesListView);
            var stackPanel = VisualTreeHelper.GetChild(itemsPresenter, 0) as StackPanel;

            var container = GamesListView.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (container != null && stackPanel != null)
            {
                // Add margins to the stackpanel so the first/last items can actually reach the center
                double sideMargin = (scrollViewer.ViewportWidth / 2) - (container.ActualWidth / 2);
                stackPanel.Margin = new Thickness(sideMargin, 0, sideMargin, 0);
                stackPanel.UpdateLayout();

                // Calculate the target X position
                var transform = container.TransformToAncestor((Visual)scrollViewer.Content);
                Point relativePos = transform.Transform(new Point(0, 0));
                double targetOffset = relativePos.X - (scrollViewer.ViewportWidth / 2) + (container.ActualWidth / 2);

                // Create the smooth animation
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

        /// <summary>
        /// Throttles mouse wheel input to move selection by exactly one item at a time in Cover Mode.
        /// </summary>
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

        /// <summary>
        /// Allows the user to deselect a game by clicking it again.
        /// </summary>
        private void GamesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(GamesListView, e.OriginalSource as DependencyObject) as ListViewItem;
            if (item != null && item.IsSelected)
            {
                GamesListView.SelectedIndex = -1;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Logic for Grid Mode: hover over an item to select it.
        /// </summary>
        private void ListViewItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (GridModeBtn.IsChecked == true && sender is ListViewItem item) item.IsSelected = true;
        }

        // --- Search Logic ---

        /// <summary>
        /// Restarts the debounce timer whenever the search text changes.
        /// </summary>
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

        /// <summary>
        /// Filters the Games collection on a background thread using a HashSet for high-speed lookups.
        /// Updates the UI view and refreshes the game count.
        /// </summary>
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

        /// <summary>
        /// Orchestrates image loading: checks local storage first, downloads if missing, 
        /// then converts the file into a UI-ready BitmapImage.
        /// </summary>
        private async Task LoadImageWithCache(SteamGame game)
        {
            try
            {
                string localPath = _imageService.GetLocalImagePath(game.AppId);

                // Download if file doesn't exist
                if (!_imageService.DoesImageExistLocally(game.AppId))
                {
                    await _imageService.DownloadAndSaveImageAsync(game.AppId, game.ImageUrl);
                }

                // If file exists, load it into memory and freeze it for cross-thread UI performance
                if (_imageService.DoesImageExistLocally(game.AppId))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(localPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // Prevents the file from being locked on disk
                    bitmap.EndInit();
                    bitmap.Freeze();
                    game.DisplayImage = bitmap;
                }
                else
                {
                    // Fallback to URL to trigger Waterfall logic in Image_ImageFailed
                    game.DisplayImage = game.ImageUrl;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UI Load error for {game.Name}: {ex.Message}");
                game.DisplayImage = game.ImageUrl;
            }
        }

        /// <summary>
        /// Button handler to fetch the user's library from Steam. 
        /// Compares fetched data with current list to avoid duplicates and saves the new list to disk.
        /// </summary>
        private async void LoadGames_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = Properties.Settings.Default.SteamApiKey;
            string steamId = Properties.Settings.Default.SteamId;

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(steamId))
            {
                MessageBox.Show("Please enter your Steam API Key and Steam ID in Settings first!", "Missing Info");
                return;
            }

            try
            {
                var fetchedGames = await _steamService.GetGamesFromApiAsync(apiKey, steamId);

                bool newGamesAdded = false;
                foreach (var game in fetchedGames)
                {
                    if (!Games.Any(g => g.AppId == game.AppId))
                    {
                        Games.Add(game);
                        _ = LoadImageWithCache(game);
                        newGamesAdded = true;
                    }
                }

                if (newGamesAdded)
                {
                    _steamService.SaveGamesToDisk(Games);
                }

                InitializeSearchCache();
                RefreshCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Direct store API call to fetch short descriptions.
        /// Note: This logic is partially duplicated in SteamService.
        /// </summary>
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

        /// <summary> Displays the Settings UI overlay with current saved values. </summary>
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            ApiKeyInput.Text = Properties.Settings.Default.SteamApiKey;
            SteamIdInput.Text = Properties.Settings.Default.SteamId;
            SettingsOverlay.Visibility = Visibility.Visible;
        }

        /// <summary> Saves input settings to the application's configuration file. </summary>
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.SteamApiKey = ApiKeyInput.Text.Trim();
            Properties.Settings.Default.SteamId = SteamIdInput.Text.Trim();
            Properties.Settings.Default.Save();
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary> Dismisses the settings overlay. </summary>
        private void CancelSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Performs a "Hard Reset": Clears the image folder, deletes the JSON database, 
        /// and empties the UI collection.
        /// </summary>
        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("This will delete all cached images and your game list. Continue?",
                                        "Full Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                int deletedCount = _imageService.ClearAllCachedImages();
                _steamService.DeleteGamesCache();
                Games.Clear();
                InitializeSearchCache();
                RefreshCount();

                MessageBox.Show($"Cache cleared! Deleted {deletedCount} images and game metadata.", "Success");
            }
        }

        // --- Utilities & Helper Methods ---

        /// <summary>
        /// Updates the ScrollViewer's offset whenever the ScrollHelper dependency property is animated.
        /// </summary>
        private static void OnScrollHelperChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = d as MainWindow;
            var viewer = UIHelper.GetScrollViewer(window.GamesListView);
            if (viewer != null) viewer.ScrollToHorizontalOffset((double)e.NewValue);
        }

        /// <summary> Helper to drill down the Visual Tree to find a specific element type. </summary>
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

        /// <summary> Helper to locate the ScrollViewer within a control. </summary>
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

        /// <summary> Updates the UI label with the current number of filtered games. </summary>
        private void RefreshCount() => CountLabel.Text = $"{CollectionViewSource.GetDefaultView(Games).Cast<object>().Count()} Games";

        /// <summary> Legacy local save method (Redirected to SteamService in LoadGames_Click). </summary>
        private void SaveGamesToDisk() => File.WriteAllText(cachePath, JsonSerializer.Serialize(Games));

        /// <summary> Loads game collection from the JSON file and prepares image loading. </summary>
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

        /// <summary> Ensures the selection remains centered if the window is resized. </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CoverModeBtn.IsChecked == true && GamesListView.SelectedItem != null)
            {
                Dispatcher.BeginInvoke(new Action(() => { CenterSelectedItem(); }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// Handles switching between Grid and Cover layouts. Resets scroll positions for a clean view.
        /// </summary>
        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (GamesListView == null || GamesListView.Items.Count == 0) return;
            GamesListView.SelectedIndex = -1;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                var scrollViewer = UIHelper.GetScrollViewer(GamesListView);
                if (scrollViewer != null)
                {
                    if (CoverModeBtn.IsChecked == true) { GamesListView.SelectedIndex = 0; CenterSelectedItem(); }
                    else { scrollViewer.ScrollToTop(); scrollViewer.ScrollToHome(); }
                }
            }));
        }

        /// <summary>
        /// Overrides standard scroll behavior to ensure that when an item is brought into view,
        /// it uses our custom centering logic instead of the default left-alignment.
        /// </summary>
        private void GamesListView_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
            if (CoverModeBtn.IsChecked == true && e.TargetObject is FrameworkElement element)
            {
                var scrollViewer = UIHelper.GetScrollViewer(GamesListView);
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

        /// <summary>
        /// The Image Waterfall Fallback Logic.
        /// When a 404 occurs, this method cycles through available Steam image types:
        /// 1. Library 600x900 (Portrait)
        /// 2. App Header (Horizontal)
        /// 3. App Icon (Small hash-based image)
        /// 4. Official Steam Placeholder
        /// </summary>
        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var img = sender as Image;
            var game = img?.DataContext as SteamGame;
            if (game == null) return;
            string currentUrl = game.DisplayImage?.ToString() ?? "";

            // Fallback Level 1: Try Header
            if (currentUrl.Contains("library_600x900"))
                game.DisplayImage = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/header.jpg";

            // Fallback Level 2: Try Icon
            else if (currentUrl.Contains("header.jpg") && !string.IsNullOrEmpty(game.IconUrl))
                game.DisplayImage = game.IconUrl;

            // Fallback Level 3: Generic Placeholder
            else
                game.DisplayImage = "https://community.cloudflare.steamstatic.com/public/images/applications/store/placeholder.png";

            // Save the working URL so we don't repeat this waterfall on next launch
            _steamService.SaveGamesToDisk(Games);
        }
    }
}