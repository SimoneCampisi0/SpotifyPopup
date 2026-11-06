using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            if (session == null) { TitleText.Text = "Apri Spotify e avvia un brano"; ArtistText.Text = ""; AlbumText.Text = ""; Cover.Source = null; Progress.Value = 0; Progress.IsEnabled = RestartButton.IsEnabled = false; ElapsedText.Text = DurationText.Text = "0:00"; currentKey = ""; return; }
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
            Progress.IsEnabled = RestartButton.IsEnabled = info.Controls.IsPlaybackPositionEnabled && duration > TimeSpan.Zero;
            PlayButton.Content = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing ? "\uE769" : "\uE768";
        }
        catch (Exception) { TitleText.Text = "Spotify non disponibile"; Progress.IsEnabled = RestartButton.IsEnabled = false; }
        finally { busy = false; }
    }
    private static string Format(TimeSpan t) => t < TimeSpan.Zero ? "0:00" : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    private async void PlayClick(object sender, RoutedEventArgs e) { if (session != null) { await session.TryTogglePlayPauseAsync(); await Refresh(); } }
    private async void PreviousClick(object sender, RoutedEventArgs e) { if (session != null) { await session.TrySkipPreviousAsync(); await Refresh(); } }
    private async void NextClick(object sender, RoutedEventArgs e) { if (session != null) { await session.TrySkipNextAsync(); await Refresh(); } }
    private async void RestartClick(object sender, RoutedEventArgs e) => await SeekToFraction(0);
    private async void Seek(object sender, MouseButtonEventArgs e) => await SeekToFraction(Progress.Value / 100);
    private async Task SeekToFraction(double fraction)
    {
        var targetSession = session;
        if (targetSession == null || !Progress.IsEnabled) return;
        try
        {
            var timeline = targetSession.GetTimelineProperties();
            var duration = timeline.EndTime - timeline.StartTime;
            if (!targetSession.GetPlaybackInfo().Controls.IsPlaybackPositionEnabled || duration <= TimeSpan.Zero) return;
            var offset = TimeSpan.FromTicks((long)(duration.Ticks * Math.Clamp(fraction, 0, 1)));
            if (await targetSession.TryChangePlaybackPositionAsync((timeline.StartTime + offset).Ticks))
            {
                Progress.Value = Math.Clamp(fraction, 0, 1) * 100;
                ElapsedText.Text = Format(offset);
            }
        }
        catch (Exception) { await Refresh(); }
    }
    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (locked || e.ButtonState != MouseButtonState.Pressed) return;
        for (DependencyObject? element = e.OriginalSource as DependencyObject; element != null && element != RootSurface; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ButtonBase or Slider or Thumb or ResizeGrip) return;
        }
        DragMove();
    }
    private void LockClick(object sender, RoutedEventArgs e) => SetLocked(true);
    private void SetLocked(bool value)
    {
        locked = value; Opacity = value ? 0.58 : 1;
        ToolTipService.SetIsEnabled(TitleText, !value);
        if (value && TitleText.ToolTip is ToolTip titleToolTip) titleToolTip.IsOpen = false;
        var h = new WindowInteropHelper(this).Handle; var style = GetWindowLong(h, -20); SetWindowLong(h, -20, value ? style | 0x20 : style & ~0x20);
        if (value)
        {
            var button = new Button { Content = "\uE785", ToolTip = "Sblocca Spotify Popup", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15, Background = new SolidColorBrush(Color.FromArgb(240, 39, 57, 52)), Foreground = Brushes.White, Style = (Style)FindResource("GlassButton") };
            System.Windows.Automation.AutomationProperties.SetName(button, "Sblocca Spotify Popup");
            unlock = new Window { Width = 32, Height = 32, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, Content = button, Owner = this };
            button.Click += (_, _) => SetLocked(false); PositionUnlock(); unlock.Show();
        }
        else { unlock?.Close(); unlock = null; }
    }
    private void PositionUnlock() { if (unlock != null) { unlock.Left = Left + ActualWidth - 51; unlock.Top = Top + 11; } }
    private void OnLocationChanged(object? sender, EventArgs e) => PositionUnlock();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => PositionUnlock();
    private void ExitClick(object sender, RoutedEventArgs e) => Close();
    private void OnClosed(object? sender, EventArgs e) { timer.Stop(); unlock?.Close(); }
}



