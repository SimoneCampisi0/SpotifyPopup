using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SpotifyPopup;

internal static class Program
{
    private static int checks;
    [STAThread]
    private static int Main()
    {
        // Make off-screen verification images independent of GPU surface caching.
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var exit = 0;
        app.Startup += async (_, _) =>
        {
            try { await Run(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; }
            finally { app.Shutdown(exit); }
        };
        app.Run(); return exit;
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name); checks++;
    }
    private static async Task Pump() => await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    private static async Task Run()
    {
        var output = Path.GetFullPath("test-results"); Directory.CreateDirectory(output);
        Diagnostics.DirectoryPath = Path.Combine(output, "logs");
        checks += await MediaControllerChecks.Run();
        var controller = new FakeController();
        var store = new SettingsStore(Path.Combine(output, "ui-settings.json"));
        // Deliberately start off screen to verify recovery, without touching real user settings.
        var preferences = new AppSettings { Language = "en" }; preferences.Normal.X = -100000; preferences.Normal.Y = -100000;
        Check(store.Save(preferences), "Create isolated UI preferences");
        var window = new MainWindow(controller, store, false); window.Show(); await Pump();
        Check(controller.Initialized, "Controller initializes on window load");
        Check(!window.Player.CanPlayPause && !window.Player.CanNext && !window.Player.CanPrevious && !window.Player.CanSeek, "Disconnected controls are all disabled");
        Check(window.OpenSpotifyVisibility == Visibility.Visible, "Open Spotify is available without a session");
        checks += await ArtworkChecks.Run(window, controller.Publish, output);
        var rect = NativeMethods.Bounds(window);
        Check(rect.Left > -100000 && rect.Top > -100000, "An off-screen saved window is recovered");
        controller.Publish(new MediaSnapshot { Title = "A long track title for layout validation", Artist = "Artist with a long display name", Album = "Album", Connected = true,
            Status = "Paused", Duration = 200, Position = 50, CanPlayPause = true, CanSeek = true, CanNext = true, TrackKey = "track-one" });
        await Pump();
        Check(window.Player.CanPlayPause && window.Player.CanNext && !window.Player.CanPrevious, "Individual capability flags reach the UI");
        Check(window.Player.PlayLabel == "Play" && window.OpenSpotifyVisibility == Visibility.Collapsed, "Paused state and action labels agree");
        Check(window.AlbumVisibility == Visibility.Visible, "Album is shown when enabled");
        var play = (Button)window.FindName("PlayButton");
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Pump();
        Check(controller.LastAction == MediaAction.PlayPause, "Play button dispatches a media command");
        controller.ThrowOnCommand = true;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Pump();
        Check(window.Player.Notice.Contains("could not be completed"), "Command exceptions are shown without crashing");
        controller.ThrowOnCommand = false;
        var slider = (Slider)window.FindName("NormalProgress");
        var keyboard = InputManager.Current.PrimaryKeyboardDevice;
        slider.RaiseEvent(new KeyEventArgs(keyboard, PresentationSource.FromVisual(window), Environment.TickCount, Key.Right) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        slider.Value = 65;
        slider.RaiseEvent(new KeyEventArgs(keyboard, PresentationSource.FromVisual(window), Environment.TickCount, Key.Right) { RoutedEvent = Keyboard.PreviewKeyUpEvent });
        await Pump();
        Check(controller.LastAction == MediaAction.Seek && controller.LastFraction == .65 && controller.LastKey == "track-one", "Keyboard seek commits the selected fraction and track identity");
        window.SetLocked(true); await Pump();
        Check(!((Border)window.FindName("RootSurface")).IsEnabled, "Locked mode blocks keyboard interaction with player controls");
        Check(window.OwnedWindows.Count == 1, "Locked mode retains an independent unlock window");
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        Check((NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle) & NativeMethods.WsExTransparent) != 0, "Locked window passes mouse input through");
        window.SetLocked(false); await Pump();
        Check(window.OwnedWindows.Count == 0 && ((Border)window.FindName("RootSurface")).IsEnabled, "Unlock restores interaction and closes helper window");
        window.Width = window.MinWidth; window.Height = window.MinHeight; await Pump(); SaveImage(window, Path.Combine(output, "normal-minimum.png"));
        window.ToggleCompact(); window.Width = window.MinWidth; window.Height = window.MinHeight; await Pump();
        Check(((Grid)window.FindName("CompactLayout")).IsVisible, "Compact layout is available at its minimum size");
        SaveImage(window, Path.Combine(output, "compact-minimum.png"));
        controller.Publish(new MediaSnapshot()); await Pump();
        Check(window.Player.Artist == "" && window.Player.Album == "" && window.Player.Artwork == null && !window.Player.CanSeek, "Disconnect clears old metadata and capabilities");
        Check(window.Player.Elapsed == "0:00" && window.Player.Duration == "0:00", "Disconnect clears timeline");
        Check(HotkeyManager.Parse("Ctrl+Alt+P", out _, out _), "Valid global shortcut parses");
        Check(!HotkeyManager.Parse("P", out _, out _), "Unmodified global shortcut is rejected");
        var preview = 0d;
        var settingsDialog = new SettingsWindow(new AppSettings(), value => preview = value, (_, _) => "A shortcut is already in use.");
        settingsDialog.Owner = window;
        settingsDialog.Loaded += (_, _) =>
        {
            ((Slider)settingsDialog.FindName("OpacitySlider")).Value = 73;
            Check(preview == .73, "Settings opacity preview updates immediately");
            ((CheckBox)settingsDialog.FindName("ShowAlbumBox")).IsChecked = false;
            Check(!settingsDialog.ShowAlbum, "Album preference can be disabled");
            settingsDialog.Close();
        };
        settingsDialog.ShowDialog();
        await CheckLanguages(window, controller, store, output);
        await CheckAbout(window, output);
        window.Close(); await Pump();
        Check(controller.Disposed, "Closing releases the controller");
        var saved = store.Load(out _);
        Check(saved.CompactMode && saved.Compact.Width == 360, "Layout and dimensions persist on close");
        var restarted = new MainWindow(new FakeController(), store, false); restarted.Show(); await Pump();
        Check(Localizer.Current.Culture.Name == "it" && restarted.Player.Title == "Apri Spotify e riproduci un brano", "Restart applies saved Italian before first window content");
        restarted.Close();
        await CheckTray(output);
        Console.WriteLine($"{checks} Windows UI checks passed. No real Spotify commands were issued.");
    }
    private static async Task CheckLanguages(MainWindow window, FakeController controller, SettingsStore store, string output)
    {
        var track = new MediaSnapshot { Title = "Playing", Artist = "Artista originale", Album = "Album originale", TrackKey = "invariant-track",
            Connected = true, Playing = true, Status = TextKeys.Playing, Position = 50, Duration = 200, CanSeek = true, CanPlayPause = true };
        controller.Publish(track);
        window.Player.SetNotice(TextKeys.TrackChanged);
        var commands = controller.CommandCount;
        foreach (var language in new[] { "it", "en" })
        {
            window.Player.SetNotice(TextKeys.TrackChanged);
            Localizer.Current.Apply(language); await Pump();
            Check(window.Player.Title == "Playing" && window.Player.Artist == track.Artist && window.Player.Album == track.Album, "Language preserves even metadata matching a resource key: " + language);
            Check(controller.Snapshot.TrackKey == track.TrackKey && window.Player.Elapsed == "0:50" && window.Player.Duration == "3:20", "Language preserves track identity and timeline: " + language);
            Check(window.Player.PlayLabel == (language == "it" ? "Pausa" : "Pause"), "Dynamic pause label: " + language);
            Check(window.Player.Status == Localizer.Get(TextKeys.Playing) && window.Player.Notice == Localizer.Get(TextKeys.TrackChanged), "Existing status and notice retranslate: " + language);
            Check(System.Windows.Automation.AutomationProperties.GetName((Button)window.FindName("SettingsButton")) == Localizer.Get(TextKeys.Settings), "XAML accessibility binding updates: " + language);
            Check((string)((Slider)window.FindName("NormalProgress")).ToolTip == Localizer.Get(TextKeys.TrackPosition), "Tooltip binding updates: " + language);
            var menu = ((Border)window.FindName("RootSurface")).ContextMenu;
            Check((string)((MenuItem)menu.Items[0]).Header == Localizer.Get(TextKeys.Hide), "Detached context menu binding updates: " + language);
            window.SetLocked(true); await Pump();
            var helper = window.OwnedWindows.Cast<Window>().Single();
            Check(System.Windows.Automation.AutomationProperties.GetName((Button)helper.Content) == Localizer.Get(TextKeys.UnlockPopup), "Unlock helper is localized: " + language);
            Localizer.Current.Apply(language == "it" ? "en" : "it"); await Pump();
            Check((string)((Button)helper.Content).ToolTip == Localizer.Get(TextKeys.UnlockPopup), "Existing unlock helper retranslated");
            window.Width += 20; window.Left += 5; await Pump();
            var point = ((Button)window.FindName("LockButton")).PointToScreen(new Point());
            var bounds = NativeMethods.Bounds(helper);
            Check(Math.Abs(bounds.Left - point.X) <= 1 && Math.Abs(bounds.Top - point.Y) <= 1, "Unlock helper follows move and resize");
            window.SetLocked(false);
            Localizer.Current.Apply(language); window.Player.SetNotice("");
            if (((Grid)window.FindName("CompactLayout")).IsVisible) window.ToggleCompact();
            window.Width = window.MinWidth; window.Height = window.MinHeight; await Pump();
            SaveImage(window, Path.Combine(output, "normal-minimum-" + language + ".png"));
            window.ToggleCompact(); window.Width = window.MinWidth; window.Height = window.MinHeight; await Pump();
            SaveImage(window, Path.Combine(output, "compact-minimum-" + language + ".png"));
            window.Player.SetNotice(TextKeys.SettingsSaveFailed); window.Height = window.MinHeight; await Pump();
            SaveImage(window, Path.Combine(output, "long-notice-" + language + ".png"));
            window.Player.SetNotice("");
            controller.Publish(track with { Playing = false, Status = TextKeys.Paused }); await Pump();
            Check(window.Player.PlayLabel == (language == "it" ? "Riproduci" : "Play"), "Dynamic play label: " + language);
            controller.Publish(new()); await Pump();
            Check(window.Player.Title == Localizer.Get(TextKeys.OpenSpotifyPrompt) && window.Player.Status == Localizer.Get(TextKeys.SpotifyUnavailable), "Disconnected defaults translated: " + language);
            SaveImage(window, Path.Combine(output, "disconnected-" + language + ".png"));
            controller.Publish(track);
        }
        Check(controller.CommandCount == commands, "Language changes issue no media commands");
        Localizer.Current.Apply("en");
        await EditSettings(window, async dialog =>
        {
            await Pump();
            ((ComboBox)dialog.FindName("LanguageBox")).SelectedValue = "it";
            Check(window.Player.PlayLabel == "Pause", "Selecting a language does not apply it before Save");
            dialog.Close();
        });
        Check(Localizer.Current.Culture.Name == "en" && store.Load(out _).Language == "en", "Cancel preserves language and stored preferences");
        await EditSettings(window, async dialog =>
        {
            await Pump();
            ((ComboBox)dialog.FindName("LanguageBox")).SelectedValue = "it";
            dialog.MinHeight = 540; dialog.Height = 540; dialog.Width = dialog.MinWidth;
            await Pump();
            await Task.Delay(300);
            CheckSettingsLayout(dialog);
            SaveImage(dialog, Path.Combine(output, "settings-en.png"));
            FindButtons(dialog).Single(b => (string?)b.Content == "Save").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        Check(Localizer.Current.Culture.Name == "it" && window.Player.PlayLabel == "Pausa" && store.Load(out _).Language == "it", "Save applies and persists language immediately");
        await EditSettings(window, async dialog =>
        {
            await Pump();
            Check(dialog.Title == "Impostazioni di Spotify Popup", "Settings opens in saved language");
            dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight;
            await Pump();
            await Task.Delay(300);
            CheckSettingsLayout(dialog);
            SaveImage(dialog, Path.Combine(output, "settings-it.png"));
            ((ComboBox)dialog.FindName("LanguageBox")).SelectedValue = "en";
            dialog.Close();
        });
        Check(Localizer.Current.Culture.Name == "it", "Cancel retains Italian");
        using (var held = File.Open(store.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await EditSettings(window, async dialog =>
            {
                await Pump();
                ((ComboBox)dialog.FindName("LanguageBox")).SelectedValue = "en";
                FindButtons(dialog).Single(b => (string?)b.Content == "Salva").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            Check(Localizer.Current.Culture.Name == "en" && window.Player.Notice == Localizer.Get(TextKeys.SettingsSaveFailed), "Failed save applies in memory and displays localized warning");
        }
        Check(store.Load(out _).Language == "it", "Failed save preserves previous file");
        Localizer.Current.Apply("it");
        Check(window.Player.Notice == Localizer.Get(TextKeys.SettingsSaveFailed), "Failed save notice retranslated without repeating save");
        // Validation errors remain keys and retranslate in an already open dialog.
        var validation = new SettingsWindow(new AppSettings { Language = "it" }, _ => { }, (_, _) => TextKeys.ShortcutBusy);
        validation.Owner = window;
        validation.Loaded += (_, _) =>
        {
            FindButtons(validation).Single(b => (string?)b.Content == "Salva").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((TextBlock)validation.FindName("ErrorText")).Text == Localizer.Get(TextKeys.ShortcutBusy), "Validation error translated");
            Localizer.Current.Apply("en");
            Check(validation.Title == "Spotify Popup settings" && ((TextBlock)validation.FindName("ErrorText")).Text == Localizer.Get(TextKeys.ShortcutBusy), "Open settings and validation error retranslate");
            validation.Close();
        };
        validation.ShowDialog();
        await EditSettings(window, async dialog =>
        {
            await Pump();
            ((ComboBox)dialog.FindName("LanguageBox")).SelectedValue = "it";
            FindButtons(dialog).Single(b => (string?)b.Content == "Save").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        controller.Publish(new());
    }
    private static async Task CheckAbout(MainWindow window, string output)
    {
        await EditSettings(window, async settings =>
        {
            await Pump();
            var selectedLanguage = settings.SelectedLanguage;
            Exception? failure = null;
            _ = settings.Dispatcher.BeginInvoke(new Action(() =>
            {
                var about = settings.OwnedWindows.OfType<AboutWindow>().Single();
                try
                {
                    Check(about.Owner == settings && about.Title == "About Spotify Popup", "Settings opens an owned About popup");
                    Check(((Image)about.FindName("AppLogo")).Source != null, "About reuses the embedded application logo");
                    Check(((BitmapSource)((Image)about.FindName("AppLogo")).Source).PixelWidth >= 64, "About uses a high-resolution icon frame");
                    Check(((TextBlock)about.FindName("AuthorText")).Text == "Simone Campisi", "About credits the author");
                    Check(((TextBlock)about.FindName("VersionText")).Text == "v1.4.0", "About displays the application version");
                    Check(((TextBox)about.FindName("LicenseText")).Text.Contains("GNU GENERAL PUBLIC LICENSE"), "Full GPL license is available offline");
                    about.UpdateLayout(); SaveImage(about, Path.Combine(output, "about.png"));
                    ((Button)about.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception ex) { failure = ex; }
                finally { if (about.IsVisible) about.Close(); }
            }), DispatcherPriority.ApplicationIdle);
            ((Button)settings.FindName("AboutButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (failure != null) throw failure;
            Check(settings.IsVisible && settings.SelectedLanguage == selectedLanguage, "Closing About returns to Settings without changing preferences");
            settings.Close();
        });
    }
    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button button) yield return button;
            foreach (var nested in FindButtons(child)) yield return nested;
        }
    }
    private static async Task EditSettings(MainWindow window, Func<SettingsWindow, Task> edit)
    {
        Exception? failure = null;
        _ = window.Dispatcher.BeginInvoke(new Action(async () =>
        {
            var dialog = window.OwnedWindows.OfType<SettingsWindow>().Single();
            try { await edit(dialog); }
            catch (Exception ex) { failure = ex; }
            finally { if (dialog.IsVisible) dialog.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        ((Button)window.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (failure != null) throw failure;
        await Pump();
    }
    private static void CheckSettingsLayout(SettingsWindow dialog)
    {
        dialog.UpdateLayout();
        var scroll = (ScrollViewer)dialog.FindName("SettingsScroll");
        Check(scroll.ExtentWidth <= scroll.ViewportWidth + 1, "Settings has no hidden horizontal overflow: " + Localizer.Current.Culture.Name);
        Check(((ComboBox)dialog.FindName("LanguageBox")).ActualWidth > 100 && ((Slider)dialog.FindName("OpacitySlider")).ActualWidth > 100, "Language selector and opacity fit at minimum width");
    }
    private static async Task CheckTray(string output)
    {
        var store = new SettingsStore(Path.Combine(output, "tray-settings.json"));
        store.Save(new AppSettings { Language = "en", ToggleShortcut = "Ctrl+Alt+F10", UnlockShortcut = "Ctrl+Alt+F11" });
        var window = new MainWindow(new FakeController(), store, true); window.Show(); await Pump();
        object? Field(string name) => typeof(MainWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window);
        string? MenuText(string name) => (string?)Field(name)?.GetType().GetProperty("Text")!.GetValue(Field(name));
        Check(Field("tray") != null, "Real notification icon is created");
        Localizer.Current.Apply("it"); await Pump();
        Check(MenuText("trayVisibility") == "Nascondi" && MenuText("trayLock") == "Blocca" && MenuText("traySettings") == "Impostazioni" && MenuText("trayExit") == "Esci", "All existing tray entries retranslate");
        window.SetLocked(true); await Pump();
        Check(MenuText("trayLock") == "Sblocca", "Locked tray action is localized");
        Localizer.Current.Apply("en"); await Pump();
        Check(MenuText("trayLock") == "Unlock" && MenuText("traySettings") == "Settings", "Locked tray reverts to English");
        window.Close(); await Pump();
    }
    private static void SaveImage(Window window, string path)
    {
        window.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private sealed class FakeController : IMediaController
    {
        public MediaSnapshot Snapshot { get; private set; } = new();
        public event EventHandler<MediaSnapshot>? Updated;
        public event EventHandler<string>? Notice { add { } remove { } }
        public bool Initialized, Disposed, ThrowOnCommand;
        public int CommandCount;
        public MediaAction? LastAction;
        public double LastFraction;
        public string? LastKey;
        public Task InitializeAsync() { Initialized = true; Publish(new()); return Task.CompletedTask; }
        public void Tick() { }
        public void Publish(MediaSnapshot snapshot) { Snapshot = snapshot; Updated?.Invoke(this, snapshot); }
        public Task ExecuteAsync(MediaAction action, double fraction = 0, string? expectedTrackKey = null)
        {
            CommandCount++;
            if (ThrowOnCommand) throw new InvalidOperationException("Simulated session disconnect");
            LastAction = action; LastFraction = fraction; LastKey = expectedTrackKey; return Task.CompletedTask;
        }
        public void Dispose() { Disposed = true; Updated = null; }
    }
}
