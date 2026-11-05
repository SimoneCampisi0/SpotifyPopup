using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;

namespace SpotifyPopup;

public partial class MainWindow : Window
{
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] private static extern int GetWindowLong(IntPtr h, int n);
    [DllImport("user32.dll", EntryPoint="SetWindowLongW")] private static extern int SetWindowLong(IntPtr h, int n, int value);
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Window? unlock;
    private bool busy;
    private bool locked;
    private string currentKey = "";
    public MainWindow() { InitializeComponent(); timer.Tick += async (_, _) => await Refresh(); }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); timer.Start(); await Refresh(); }
        catch (Exception) { TitleText.Text = "Controlli multimediali non disponibili"; }
    }
    private async Task Refresh()
    {
        if (busy || manager == null) return;
        busy = true;
        try
        {
            session = manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase));
            if (session == null) { TitleText.Text = "Apri Spotify e avvia un brano"; ArtistText.Text = ""; AlbumText.Text = ""; Cover.Source = null; Progress.Value = 0; currentKey = ""; return; }
            var props = await session.TryGetMediaPropertiesAsync();
            TitleText.Text = props.Title; ArtistText.Text = props.Artist; AlbumText.Text = props.AlbumTitle;
            string key = props.Title + "\n" + props.Artist + "\n" + props.AlbumTitle;
            if (key != currentKey)
            {
                currentKey = key; Cover.Source = null;
                if (props.Thumbnail != null)
                {
                    using var stream = await props.Thumbnail.OpenReadAsync();
                    using var input = stream.AsStreamForRead();
                    var img = new BitmapImage(); img.BeginInit(); img.CacheOption = BitmapCacheOption.OnLoad; img.StreamSource = input; img.EndInit(); img.Freeze(); Cover.Source = img;
                }
            }
            var info = session.GetPlaybackInfo(); var timeline = session.GetTimelineProperties();
            var position = timeline.Position;
            if (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) position += DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
            var duration = timeline.EndTime - timeline.StartTime;
            position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            if (duration > TimeSpan.Zero && position > duration) position = duration;
            ElapsedText.Text = Format(position); DurationText.Text = Format(duration);
            if (!Progress.IsMouseCaptureWithin) Progress.Value = duration.TotalSeconds > 0 ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds * 100, 0, 100) : 0;
            Progress.IsEnabled = info.Controls.IsPlaybackPositionEnabled && duration > TimeSpan.Zero;
            PlayButton.Content = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing ? "❚❚" : "▶";
        }
        catch (Exception) { TitleText.Text = "Spotify non disponibile"; }
        finally { busy = false; }
    }
    private static string Format(TimeSpan t) => t < TimeSpan.Zero ? "0:00" : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    private async void PlayClick(object sender, RoutedEventArgs e) { if (session != null) { await session.TryTogglePlayPauseAsync(); await Refresh(); } }
    private async void PreviousClick(object sender, RoutedEventArgs e) { if (session != null) { await session.TrySkipPreviousAsync(); await Refresh(); } }
    private async void NextClick(object sender, RoutedEventArgs e) { if (session != null) { await session.TrySkipNextAsync(); await Refresh(); } }
    private async void Seek(object sender, MouseButtonEventArgs e) { if (session != null && Progress.IsEnabled) { var t = session.GetTimelineProperties(); await session.TryChangePlaybackPositionAsync((long)(t.StartTime.Ticks + (t.EndTime - t.StartTime).Ticks * Progress.Value / 100)); } }
    private void OnDrag(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is FrameworkElement el && el is not System.Windows.Controls.Button && e.ButtonState == MouseButtonState.Pressed && !locked) DragMove(); }
    private void LockClick(object sender, RoutedEventArgs e) => SetLocked(true);
    private void SetLocked(bool value)
    {
        locked = value; Opacity = value ? 0.58 : 1;
        var h = new WindowInteropHelper(this).Handle; var style = GetWindowLong(h, -20); SetWindowLong(h, -20, value ? style | 0x20 : style & ~0x20);
        if (value)
        {
            var button = new System.Windows.Controls.Button { Content = "🔓", ToolTip = "Sblocca Spotify Popup", Background = Brushes.White, BorderThickness = new Thickness(0), FontSize = 15 };
            unlock = new Window { Width = 31, Height = 31, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, Content = button, Owner = this };
            button.Click += (_, _) => SetLocked(false); PositionUnlock(); unlock.Show();
        }
        else { unlock?.Close(); unlock = null; }
    }
    private void PositionUnlock() { if (unlock != null) { unlock.Left = Left + Width - 36; unlock.Top = Top + 8; } }
    private void OnLocationChanged(object? sender, EventArgs e) => PositionUnlock();
    private void ExitClick(object sender, RoutedEventArgs e) => Close();
    private void OnClosed(object? sender, EventArgs e) { timer.Stop(); unlock?.Close(); }
}


