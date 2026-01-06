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
        /// <summary> Handles search operations </summary>
        private readonly SearchService _searchService = new SearchService();

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
            // At the very end of CenterSelectedItem()
            if (CarouselModeBtn.IsChecked == true)
            {
                UpdateCarouselPerspective();
            }
        }

        /// <summary>
        /// Throttles mouse wheel input to move selection by exactly one item at a time in Cover Mode.
        /// </summary>
        //private void GamesListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        //{
        //    if (CoverModeBtn.IsChecked == true || CarouselModeBtn.IsChecked == true)
        //    {
        //        e.Handled = true;
        //        if ((DateTime.Now - _lastScrollTime).TotalMilliseconds < 250) return;
        //        _lastScrollTime = DateTime.Now;

        //        int newIndex = GamesListView.SelectedIndex;
        //        if (e.Delta < 0) newIndex++; else newIndex--;

        //        if (newIndex >= 0 && newIndex < GamesListView.Items.Count)
        //        {
        //            GamesListView.SelectedIndex = newIndex;
        //            // Force the ListView to bring the new selection to the center
        //            GamesListView.ScrollIntoView(GamesListView.SelectedItem);
        //        }
        //    }
        //}



        private void GamesListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (CoverModeBtn.IsChecked == true || CarouselModeBtn.IsChecked == true)
            {
                e.Handled = true;
                if ((DateTime.Now - _lastScrollTime).TotalMilliseconds < 250) return;
                _lastScrollTime = DateTime.Now;

                int newIndex = GamesListView.SelectedIndex;
                if (e.Delta < 0) newIndex++; else newIndex--;

                if (newIndex >= 0 && newIndex < GamesListView.Items.Count)
                {
                    GamesListView.SelectedIndex = newIndex;

                    // This is the fix. We replace ScrollIntoView with your centering logic.
                    // We use the Dispatcher to ensure the "SelectedIndex" change has 
                    // finished before we calculate the math.
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CenterSelectedItem();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
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

        private void UpdateCarouselPerspective()
        {
            if (CarouselModeBtn.IsChecked != true) return;

            int selectedIndex = GamesListView.SelectedIndex;
            if (selectedIndex < 0) return;

            for (int i = 0; i < GamesListView.Items.Count; i++)
            {
                var container = GamesListView.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                if (container == null) continue;

                var border = UIHelper.FindChild<Border>(container, "BaseBorder");
                if (border == null) continue;

                // Ensure we have 3 transforms (Skew, Scale, Translate)
                if (!(border.RenderTransform is TransformGroup group) || group.Children.Count < 3 || group.IsFrozen)
                {
                    var newGroup = new TransformGroup();
                    newGroup.Children.Add(new SkewTransform(0, 0));    // 0
                    newGroup.Children.Add(new ScaleTransform(1, 1));   // 1
                    newGroup.Children.Add(new TranslateTransform(0, 0)); // 2
                    border.RenderTransform = newGroup;
                    group = newGroup;
                }

                var skew = (SkewTransform)group.Children[0];
                var scale = (ScaleTransform)group.Children[1];
                var trans = (TranslateTransform)group.Children[2];

                // --- UPDATED LOGIC BLOCK ---
                double angle = 0;
                double size = 0.9;
                double translateY = 0; // Reset to 0 to start
                double translateX = 0; // Optional: used to pull items closer
                double opac = 0.4;
                int zIndex = 0;

                if (i < selectedIndex) // Left Side
                {
                    angle = -20;
                    translateY = 30;   // Explicitly lower the left side
                    translateX = 20;   // Move slightly toward the center
                    zIndex = i;
                }
                else if (i > selectedIndex) // Right Side
                {
                    angle = 20;
                    translateY = 30;   // Match the height of the left side exactly
                    translateX = -20;  // Move slightly toward the center
                    zIndex = GamesListView.Items.Count - i;
                }
                else // SELECTED (The Focus)
                {
                    angle = 0;
                    size = 1.45;        // Large focus
                    translateY = -20; // Your high lift
                    translateX = 0;
                    opac = 1.0;
                    zIndex = 1000;

                    // --- DIAGNOSTIC LOGGING ---
                    var textPanel = UIHelper.FindChild<StackPanel>(container, "TextPanel");
                    if (textPanel != null)
                    {
                        // Get the Name and Playtime from the DataContext
                        if (container.DataContext is SteamGame game)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Carousel Selection] Game: {game.Name} | Playtime: {game.Playtime}");
                        }

                        // Check the current state of the UI element
                        System.Diagnostics.Debug.WriteLine($"[UI State] Visibility: {textPanel.Visibility}, Opacity: {textPanel.Opacity}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[Carousel Error] Could not find TextPanel in the Visual Tree.");
                    }


                }

                var duration = TimeSpan.FromMilliseconds(600);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                // Animate Scale (Bigger)
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(size, duration) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(size, duration) { EasingFunction = ease });

                // Animate Vertical Position (Higher/Lower)
                trans.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateY, duration) { EasingFunction = ease });

                // Animate Tilt
                skew.BeginAnimation(SkewTransform.AngleYProperty, new DoubleAnimation(angle, duration) { EasingFunction = ease });

                // Animate Opacity
                border.BeginAnimation(OpacityProperty, new DoubleAnimation(opac, duration) { EasingFunction = ease });

                Panel.SetZIndex(container, zIndex);
            }
        }

        // --- Search Logic ---

        // --- Search Logic ---

        /// <summary>
        /// Handles changes in the search input. Updates the UI state of the clear button 
        /// and manages the debounce timer to prevent excessive API/filtering calls.
        /// </summary>
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Update the visibility of the 'X' button based on whether there is text to clear.
            if (ClearSearchBtn != null)
            {
                ClearSearchBtn.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            // Initialize the debounce timer if it doesn't exist.
            if (_searchTimer == null)
            {
                _searchTimer = new System.Windows.Threading.DispatcherTimer();
                _searchTimer.Interval = TimeSpan.FromMilliseconds(200);
                _searchTimer.Tick += (s, args) =>
                {
                    _searchTimer.Stop();
                    PerformSearch(); // Execute the actual search logic after the user stops typing.
                };
            }

            // Reset the timer on every keystroke.
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        /// <summary>
        /// Clears the search box and returns focus to the input field.
        /// This will automatically trigger the TextChanged event.
        /// </summary>
        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            // Clearing the text triggers SearchTextBox_TextChanged, 
            // which handles the UI reset and refreshes the game list.
            SearchTextBox.Text = string.Empty;

            // Return focus to the text box so the user can immediately start a new search.
            SearchTextBox.Focus();
        }

        /// <summary>
        /// Executes the search logic. 
        /// Uses SearchService to find matches in the background and refreshes the UI view.
        /// </summary>
        private async void PerformSearch()
        {
            if (Games == null || _allGamesCache == null) return;

            // Step 1: Get the filtered results from the service
            var filteredResults = await _searchService.FilterGamesAsync(_allGamesCache, SearchTextBox.Text);

            // Step 2: Apply the filter to the CollectionView
            var view = CollectionViewSource.GetDefaultView(Games);
            view.Filter = item =>
            {
                if (filteredResults == null) return true; // Show all if search is empty
                return filteredResults.Contains((SteamGame)item);
            };

            // Step 3: Refresh UI elements on the Background priority to keep typing smooth
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                view.Refresh();

                GamesListView.SelectedIndex = -1;

                // Only auto-select and center if we are in Cover Mode (where selection is needed for the 3D effect)
                if (CoverModeBtn.IsChecked == true && !view.IsEmpty)
                {
                    GamesListView.SelectedIndex = 0;
                    CenterSelectedItem();
                }

                RefreshCount();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // --- Data & API Operations ---

        /// <summary>
        /// Manages the lifecycle of a game's visual representation.
        /// 1. Checks for a local cached version of the image.
        /// 2. If missing, attempts a graceful download (handling 404s via ImageService).
        /// 3. If available locally, loads the file into a 'Frozen' BitmapImage for UI performance.
        /// 4. If download fails, sets the URL to trigger the secondary Waterfall fallback logic.
        /// </summary>
        /// <param name="game">The SteamGame object to update with a displayable image.</param>
        private async Task LoadImageWithCache(SteamGame game)
        {
            try
            {
                string localPath = _imageService.GetLocalImagePath(game.AppId);

                // Try to download only if it doesn't exist locally
                bool exists = _imageService.DoesImageExistLocally(game.AppId);

                if (!exists)
                {
                    // The method now returns true/false instead of crashing on 404
                    exists = await _imageService.DownloadAndSaveImageAsync(game.AppId, game.ImageUrl);
                }

                if (exists)
                {
                    // Load from local disk
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
                    // Trigger the Waterfall because the download failed (404)
                    game.DisplayImage = game.ImageUrl;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UI Load error: {ex.Message}");
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

        /// <summary> 
        /// Ensures the selection remains centered if the window is resized. 
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Check for both modes here
            if ((CoverModeBtn.IsChecked == true || CarouselModeBtn.IsChecked == true)
                && GamesListView.SelectedItem != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CenterSelectedItem();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
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
                    if (CoverModeBtn.IsChecked == true || CarouselModeBtn.IsChecked == true) { GamesListView.SelectedIndex = 0; CenterSelectedItem(); }
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
        /// Triggered when an image fails to load (usually a 404 from Steam's CDN).
        /// Delegates the "Waterfall" fallback logic to the SteamService and saves the
        /// successful fallback URL to prevent future load failures.
        /// </summary>
        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (sender is Image img && img.DataContext is SteamGame game)
            {
                string currentUrl = game.DisplayImage?.ToString() ?? "";

                // Ask the service for the next step in the waterfall
                game.DisplayImage = _steamService.GetFallbackImageUrl(game.AppId, currentUrl, game.IconUrl);

                // Save the result so we don't have to fail again next time
                _steamService.SaveGamesToDisk(Games);
            }
        }
    }
}