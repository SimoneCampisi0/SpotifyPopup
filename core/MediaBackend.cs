using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace SpotifyPopup;

internal sealed record MediaCapabilities(bool PlayPause, bool Play, bool Pause, bool Previous, bool Next, bool Seek);
internal sealed record MediaPlayback(bool Playing, string Status, double Rate, MediaCapabilities Controls);
internal sealed record MediaTimeline(double Start, double Duration, double Position, DateTimeOffset Updated);
internal sealed record MediaMetadata(string Title, string Artist, string Album, ImageSource? Artwork)
{
    internal string Key => Title + "\n" + Artist + "\n" + Album;
}
internal interface IMediaSession : IDisposable
{
    event EventHandler<bool>? Changed;
    Task<MediaMetadata> ReadAsync(CancellationToken cancellation);
    MediaPlayback Playback();
    MediaTimeline Timeline();
    Task<bool> ExecuteAsync(MediaAction action, long position, CancellationToken cancellation);
}
internal interface IMediaBackend : IDisposable
{
    event EventHandler? Changed;
    Task InitializeAsync(CancellationToken cancellation);
    IMediaSession? Find();
}
internal sealed class WindowsMediaBackend : IMediaBackend
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private WindowsMediaSession? selected;
    private bool disposed;
    public event EventHandler? Changed;
    public async Task InitializeAsync(CancellationToken cancellation)
    {
        var candidate = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (disposed) return;
        manager = candidate;
        manager.SessionsChanged += SessionsChanged; manager.CurrentSessionChanged += CurrentChanged;
    }
    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager s, SessionsChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
    private void CurrentChanged(GlobalSystemMediaTransportControlsSessionManager s, CurrentSessionChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
    public IMediaSession? Find()
    {
        var matches = manager?.GetSessions().Where(s => s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase)).ToArray();
        var target = matches?.FirstOrDefault(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) ?? matches?.FirstOrDefault();
        if (target == selected?.Session) return selected;
        selected?.Dispose(); selected = target == null ? null : new WindowsMediaSession(target); return selected;
    }
    public void Dispose()
    {
        disposed = true; selected?.Dispose(); selected = null;
        if (manager != null) { manager.SessionsChanged -= SessionsChanged; manager.CurrentSessionChanged -= CurrentChanged; }
        manager = null; Changed = null;
    }
}
internal sealed class WindowsMediaSession : IMediaSession
{
    internal GlobalSystemMediaTransportControlsSession Session { get; }
    public event EventHandler<bool>? Changed;
    internal WindowsMediaSession(GlobalSystemMediaTransportControlsSession session)
    {
        Session = session; session.MediaPropertiesChanged += MetadataChanged;
        session.PlaybackInfoChanged += PlaybackChanged; session.TimelinePropertiesChanged += TimelineChanged;
    }
    private void MetadataChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) => Changed?.Invoke(this, true);
    private void PlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) => Changed?.Invoke(this, false);
    private void TimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) => Changed?.Invoke(this, false);
    public async Task<MediaMetadata> ReadAsync(CancellationToken cancellation)
    {
        var properties = await Session.TryGetMediaPropertiesAsync().AsTask(cancellation);
        ImageSource? artwork = null;
        if (properties.Thumbnail != null)
        {
            try
            {
                using var stream = await properties.Thumbnail.OpenReadAsync().AsTask(cancellation);
                using var input = stream.AsStreamForRead();
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 256; image.StreamSource = input; image.EndInit(); image.Freeze(); artwork = image;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Diagnostics.Record("artwork-read", ex); }
        }
        cancellation.ThrowIfCancellationRequested();
        return new(properties.Title ?? "", properties.Artist ?? "", properties.AlbumTitle ?? "", artwork);
    }
    public MediaPlayback Playback()
    {
        var info = Session.GetPlaybackInfo(); var c = info.Controls;
        var playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var status = info.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => TextKeys.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => TextKeys.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => TextKeys.Stopped, _ => TextKeys.Ready
        };
        return new(playing, status, info.PlaybackRate ?? 1, new(c.IsPlayPauseToggleEnabled, c.IsPlayEnabled, c.IsPauseEnabled, c.IsPreviousEnabled, c.IsNextEnabled, c.IsPlaybackPositionEnabled));
    }
    public MediaTimeline Timeline()
    {
        var t = Session.GetTimelineProperties();
        return new(t.StartTime.TotalSeconds, Math.Max(0, (t.EndTime - t.StartTime).TotalSeconds), t.Position.TotalSeconds, t.LastUpdatedTime);
    }
    public async Task<bool> ExecuteAsync(MediaAction action, long position, CancellationToken cancellation)
    {
        var p = Playback();
        return action switch
        {
            MediaAction.Previous => await Session.TrySkipPreviousAsync().AsTask(cancellation),
            MediaAction.Next => await Session.TrySkipNextAsync().AsTask(cancellation),
            MediaAction.Seek => await Session.TryChangePlaybackPositionAsync(position).AsTask(cancellation),
            _ when p.Controls.PlayPause => await Session.TryTogglePlayPauseAsync().AsTask(cancellation),
            _ when p.Playing => await Session.TryPauseAsync().AsTask(cancellation),
            _ => await Session.TryPlayAsync().AsTask(cancellation)
        };
    }
    public void Dispose()
    {
        Session.MediaPropertiesChanged -= MetadataChanged; Session.PlaybackInfoChanged -= PlaybackChanged;
        Session.TimelinePropertiesChanged -= TimelineChanged; Changed = null;
    }
}
