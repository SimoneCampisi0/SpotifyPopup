using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SpotifyPopup;

internal static class DesktopChecks
{
    internal static async Task<int> Run(string output)
    {
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        var entry = new Entry(); var registration = new StartupRegistration(entry, @"C:\Apps with spaces\SpotifyPopup.exe");
        Check(registration.TryApply(true) && entry.Value == "\"C:\\Apps with spaces\\SpotifyPopup.exe\" --autostart", "Startup command quotes paths containing spaces");
        Check(registration.Matches(true), "Startup registration matches current executable");
        entry.Value = "old-path"; Check(!registration.Matches(true), "Moved executable is detected");
        Check(registration.TryApply(true) && registration.Matches(true), "Saving repairs a stale startup path");
        Check(registration.TryApply(false) && entry.Value == null, "Disabling startup removes only the app entry");
        entry.Fail = true; Check(!registration.TryApply(true), "Startup permission failure is recoverable");
        foreach (var start in new[] { false, true }) foreach (var tray in new[] { false, true })
        {
            var store = new SettingsStore(System.IO.Path.Combine(output, $"startup-{start}-{tray}.json"));
            store.Save(new AppSettings { StartWithWindows = start, StartInTray = tray, ToggleShortcut = "Ctrl+Alt+F10", UnlockShortcut = "Ctrl+Alt+F11" });
            var window = new MainWindow(new Fake(), store, true); var wasVisible = false;
            window.IsVisibleChanged += (_, _) => { if (window.IsVisible) wasVisible = true; };
            window.Start(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(window.IsVisible == !tray && wasVisible == !tray, $"Startup combination Windows={start}, tray={tray} has no popup flash");
            if (tray) { window.RevealExisting(); await Dispatcher.Yield(); Check(window.IsVisible, "Existing tray instance can be revealed"); }
            window.Close(); await Dispatcher.Yield();
        }
        var settings = new AppSettings { EssentialMode = true, Language = "en", BackgroundMode = "fixed", AnimateBackground = false };
        var preferences = new SettingsStore(System.IO.Path.Combine(output, "essential.json")); preferences.Save(settings);
        var player = new Fake(); var essential = new MainWindow(player, preferences, false); essential.Show(); await Dispatcher.Yield();
        Check(((Grid)essential.FindName("EssentialLayout")).IsVisible && essential.MinWidth == 280, "Essential layout restores with its own dimensions");
        var changes = new List<string?>(); essential.Player.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        var track = new MediaSnapshot { Connected = true, Title = "one", TrackKey = "one", Duration = 120, Position = 1 };
        player.Publish(track); changes.Clear(); player.Publish(track with { Position = 2 });
        Check(changes.SequenceEqual(new[] { nameof(PlayerState.Elapsed) }), "Timeline tick notifies only elapsed time");
        Check(((System.Windows.Media.GradientStop)essential.FindName("BackgroundStart")).Color.R == ArtworkPalette.Default.Start.R, "Fixed background is applied without animation");
        essential.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)essential.ActualWidth, (int)essential.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(essential); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var stream = System.IO.File.Create(System.IO.Path.Combine(output, "essential.png"))) encoder.Save(stream);
        essential.Player.SetNotice(TextKeys.SettingsSaveFailed); await Dispatcher.Yield();
        essential.Player.SetNotice(""); await Dispatcher.Yield();
        Check(essential.Height == settings.Essential.Height, "Temporary notice does not permanently enlarge essential layout");
        essential.Close();
        using var hotkeyWindow = new SourceWindow();
        var source = HwndSource.FromHwnd(new WindowInteropHelper(hotkeyWindow.Window).EnsureHandle());
        using var hotkeys = new HotkeyManager(source!, () => { }, () => { });
        var shortcuts = new AppSettings { ToggleShortcut = "Ctrl+Alt+F10", UnlockShortcut = "Ctrl+Alt+F11", PlayShortcut = "Ctrl+Alt+F10" };
        Check(!hotkeys.TryApply(shortcuts, out var duplicate) && duplicate == TextKeys.ShortcutDuplicate, "Playback shortcuts cannot duplicate show/hide shortcut");
        shortcuts.PlayShortcut = "";
        Check(hotkeys.TryApply(shortcuts, out _), "Global shortcuts can be registered before opening Settings");
        hotkeys.Suspend();
        Check(hotkeys.TryApply(shortcuts, out _), "Unchanged shortcuts are restored after shortcut capture ends");
        return count;
    }
    private sealed class SourceWindow : IDisposable
    {
        internal Window Window { get; } = new();
        public void Dispose() => Window.Close();
    }
    private sealed class Entry : IStartupEntry
    {
        public string? Value; public bool Fail;
        public string? Read() => Value;
        public void Write(string? value) { if (Fail) throw new UnauthorizedAccessException(); Value = value; }
    }
    private sealed class Fake : IMediaController
    {
        public MediaSnapshot Snapshot { get; private set; } = new();
        public event EventHandler<MediaSnapshot>? Updated;
        public event EventHandler<string>? Notice { add { } remove { } }
        public Task InitializeAsync() => Task.CompletedTask;
        public void Tick() { }
        public void Publish(MediaSnapshot snapshot) { Snapshot = snapshot; Updated?.Invoke(this, snapshot); }
        public Task ExecuteAsync(MediaAction action, double fraction = 0, string? key = null) => Task.CompletedTask;
        public void Dispose() { }
    }
}
