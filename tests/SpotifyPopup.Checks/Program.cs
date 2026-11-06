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
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Application.ResourceAssembly = typeof(MainWindow).Assembly;
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
        var controller = new FakeController();
        var store = new SettingsStore(Path.Combine(output, "ui-settings.json"));
        // Deliberately start off screen to verify recovery, without touching real user settings.
        var preferences = new AppSettings(); preferences.Normal.X = -100000; preferences.Normal.Y = -100000;
        Check(store.Save(preferences), "Create isolated UI preferences");
        var window = new MainWindow(controller, store, false); window.Show(); await Pump();
        Check(controller.Initialized, "Controller initializes on window load");
        Check(!window.Player.CanPlayPause && !window.Player.CanNext && !window.Player.CanPrevious && !window.Player.CanSeek, "Disconnected controls are all disabled");
        Check(window.OpenSpotifyVisibility == Visibility.Visible, "Open Spotify is available without a session");
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
        window.Close(); await Pump();
        Check(controller.Disposed, "Closing releases the controller");
        var saved = store.Load(out _);
        Check(saved.CompactMode && saved.Compact.Width == 360, "Layout and dimensions persist on close");
        Console.WriteLine($"{checks} Windows UI checks passed. No real Spotify commands were issued.");
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
        public MediaAction? LastAction;
        public double LastFraction;
        public string? LastKey;
        public Task InitializeAsync() { Initialized = true; Publish(new()); return Task.CompletedTask; }
        public void Tick() { }
        public void Publish(MediaSnapshot snapshot) { Snapshot = snapshot; Updated?.Invoke(this, snapshot); }
        public Task ExecuteAsync(MediaAction action, double fraction = 0, string? expectedTrackKey = null)
        {
            if (ThrowOnCommand) throw new InvalidOperationException("Simulated session disconnect");
            LastAction = action; LastFraction = fraction; LastKey = expectedTrackKey; return Task.CompletedTask;
        }
        public void Dispose() { Disposed = true; Updated = null; }
    }
}
