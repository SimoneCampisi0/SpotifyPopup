using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;

namespace SpotifyPopup;

public sealed class MediaController : IMediaController
{
    private readonly Dispatcher dispatcher;
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private GlobalSystemMediaTransportControlsSessionPlaybackInfo? playback;
    private GlobalSystemMediaTransportControlsSessionTimelineProperties? timeline;
    private bool disposed, refreshing, requested, metadataDirty, commandPending, initializing;
    private bool artworkPending;
    private int generation, metadataRevision;
    private DateTimeOffset nextRecovery, nextArtworkAttempt;
    public MediaSnapshot Snapshot { get; private set; } = new();
    public event EventHandler<MediaSnapshot>? Updated;
    public event EventHandler<string>? Notice;
    public MediaController(Dispatcher dispatcher) => this.dispatcher = dispatcher;

    public async Task InitializeAsync()
    {
        if (disposed || manager != null || initializing) return;
        initializing = true;
        try
        {
            var candidate = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (disposed) return;
            manager = candidate;
            manager.SessionsChanged += SessionsChanged;
            manager.CurrentSessionChanged += CurrentSessionChanged;
            await RefreshAsync(true);
        }
        catch (Exception) { Reset(TextKeys.MediaUnavailable); }
        finally { initializing = false; }
    }
    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => QueueRefresh(true);
    private void CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => QueueRefresh(true);
    private void MediaChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => QueueSessionRefresh(sender, true);
    private void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => QueueSessionRefresh(sender, false);
    private void TimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => QueueSessionRefresh(sender, false);
    private void QueueSessionRefresh(GlobalSystemMediaTransportControlsSession sender, bool metadata)
    {
        if (disposed || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(new Action(() => { if (!disposed && sender == session) { if (metadata) { metadataRevision++; artworkPending = true; nextArtworkAttempt = DateTimeOffset.MinValue; } _ = RefreshAsync(metadata); } }));
    }
    private void QueueRefresh(bool metadata)
    {
        if (disposed || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(new Action(() => { if (!disposed) _ = RefreshAsync(metadata); }));
    }
    private void Attach(GlobalSystemMediaTransportControlsSession? value)
    {
        if (value == session) return;
        Detach(); session = value; generation++;
        playback = null; timeline = null; artworkPending = true; metadataDirty = true;
        Publish(new MediaSnapshot());
        if (session == null) return;
        session.MediaPropertiesChanged += MediaChanged;
        session.PlaybackInfoChanged += PlaybackChanged;
        session.TimelinePropertiesChanged += TimelineChanged;
    }
    private void Detach()
    {
        if (session == null) return;
        session.MediaPropertiesChanged -= MediaChanged;
        session.PlaybackInfoChanged -= PlaybackChanged;
        session.TimelinePropertiesChanged -= TimelineChanged;
        session = null;
    }
    private async Task RefreshAsync(bool metadata)
    {
        if (disposed || manager == null) return;
        requested = true; metadataDirty |= metadata;
        if (refreshing) return;
        refreshing = true;
        try
        {
            while (requested && !disposed)
            {
                requested = false;
                var selected = manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase));
                Attach(selected);
                var target = session; var token = generation;
                if (target == null) { Reset(TextKeys.SpotifyUnavailable); continue; }
                var readMetadata = metadataDirty;
                metadataDirty = false;
                if (readMetadata)
                {
                    var revision = metadataRevision;
                    var props = await target.TryGetMediaPropertiesAsync();
                    if (disposed || token != generation || session != target || revision != metadataRevision) continue;
                    var key = props.Title + "\n" + props.Artist + "\n" + props.AlbumTitle;
                    bool trackChanged = key != Snapshot.TrackKey;
                    if (trackChanged) { artworkPending = true; nextArtworkAttempt = DateTimeOffset.MinValue; }
                    Publish(Snapshot with
                    {
                        Title = props.Title ?? "", TitleFallbackKey = TextKeys.UnknownTrack,
                        Artist = props.Artist ?? "", Album = props.AlbumTitle ?? "", TrackKey = key,
                        Artwork = trackChanged ? null : Snapshot.Artwork,
                        ArtworkLoading = artworkPending
                    });
                    if (artworkPending && DateTimeOffset.UtcNow >= nextArtworkAttempt)
                    {
                        nextArtworkAttempt = DateTimeOffset.UtcNow.AddSeconds(3);
                        try
                        {
                            if (props.Thumbnail != null)
                            {
                                using var stream = await props.Thumbnail.OpenReadAsync();
                                using var input = stream.AsStreamForRead();
                                var image = new BitmapImage();
                                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = input; image.EndInit(); image.Freeze();
                                if (disposed || token != generation || session != target || Snapshot.TrackKey != key || revision != metadataRevision) continue;
                                artworkPending = false; Publish(Snapshot with { Artwork = image, ArtworkLoading = false });
                            }
                            else if (!disposed && token == generation && session == target && revision == metadataRevision)
                                Publish(Snapshot with { Artwork = null, ArtworkLoading = false });
                        }
                        catch (Exception)
                        {
                            // Confirm failure for the background while retaining independent thumbnail retries.
                            if (!disposed && token == generation && session == target && revision == metadataRevision)
                                Publish(Snapshot with { Artwork = null, ArtworkLoading = false });
                        }
                    }
                    else if (Snapshot.ArtworkLoading) Publish(Snapshot with { ArtworkLoading = false });
                }
                if (disposed || token != generation || session != target) continue;
                playback = target.GetPlaybackInfo(); timeline = target.GetTimelineProperties();
                PublishPlayback();
            }
        }
        catch (Exception)
        {
            Detach(); generation++; playback = null; timeline = null;
            Reset(TextKeys.SpotifyUnavailable);
        }
        finally { refreshing = false; }
    }
    private void PublishPlayback()
    {
        if (playback == null || timeline == null || disposed) return;
        var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var controls = playback.Controls;
        var duration = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
        var position = PlaybackMath.Position(timeline.Position.TotalSeconds, timeline.StartTime.TotalSeconds, duration,
            (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds, playback.PlaybackRate ?? 1, playing);
        var status = playback.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => TextKeys.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => TextKeys.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => TextKeys.Stopped,
            _ => TextKeys.Ready
        };
        Publish(Snapshot with
        {
            Connected = true, Playing = playing, Status = status, Position = position, Duration = duration,
            CanPlayPause = controls.IsPlayPauseToggleEnabled || (playing ? controls.IsPauseEnabled : controls.IsPlayEnabled),
            CanPrevious = controls.IsPreviousEnabled, CanNext = controls.IsNextEnabled,
            CanSeek = controls.IsPlaybackPositionEnabled && duration > 0
        });
    }
    public void Tick()
    {
        if (disposed) return;
        PublishPlayback();
        var now = DateTimeOffset.UtcNow;
        if (now >= nextRecovery)
        {
            nextRecovery = now.AddSeconds(5);
            if (manager == null) _ = InitializeAsync();
            else _ = RefreshAsync(session == null || (artworkPending && now >= nextArtworkAttempt));
        }
    }
    public async Task ExecuteAsync(MediaAction action, double fraction = 0, string? expectedTrackKey = null)
    {
        var target = session; var token = generation;
        if (disposed || target == null || commandPending) return;
        commandPending = true;
        try
        {
            var info = target.GetPlaybackInfo(); var controls = info.Controls;
            bool accepted;
            switch (action)
            {
                case MediaAction.PlayPause:
                    if (controls.IsPlayPauseToggleEnabled) accepted = await target.TryTogglePlayPauseAsync();
                    else if (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && controls.IsPauseEnabled) accepted = await target.TryPauseAsync();
                    else if (info.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && controls.IsPlayEnabled) accepted = await target.TryPlayAsync();
                    else { Report(TextKeys.PlaybackUnavailable); return; }
                    break;
                case MediaAction.Previous:
                    if (!controls.IsPreviousEnabled) { Report(TextKeys.PreviousUnavailable); return; }
                    accepted = await target.TrySkipPreviousAsync(); break;
                case MediaAction.Next:
                    if (!controls.IsNextEnabled) { Report(TextKeys.NextUnavailable); return; }
                    accepted = await target.TrySkipNextAsync(); break;
                default:
                    var properties = await target.TryGetMediaPropertiesAsync();
                    var key = properties.Title + "\n" + properties.Artist + "\n" + properties.AlbumTitle;
                    if (disposed || token != generation || target != session || (expectedTrackKey != null && expectedTrackKey != key))
                    { Report(TextKeys.TrackChanged); return; }
                    var currentTimeline = target.GetTimelineProperties();
                    var duration = currentTimeline.EndTime - currentTimeline.StartTime;
                    if (!controls.IsPlaybackPositionEnabled || duration <= TimeSpan.Zero) { Report(TextKeys.SeekUnavailable); return; }
                    accepted = await target.TryChangePlaybackPositionAsync(PlaybackMath.SeekTicks(currentTimeline.StartTime.Ticks, duration.Ticks, fraction));
                    break;
            }
            if (!disposed && token == generation && target == session && !accepted) Report(TextKeys.CommandRejected);
        }
        catch (Exception)
        {
            if (!disposed)
            {
                Detach(); generation++; playback = null; timeline = null; Reset(TextKeys.SpotifyUnavailable);
                Report(TextKeys.Reconnecting);
            }
        }
        finally { commandPending = false; if (!disposed) await RefreshAsync(true); }
    }
    private void Reset(string status) => Publish(new MediaSnapshot { Status = status });
    private void Publish(MediaSnapshot snapshot) { if (disposed) return; Snapshot = snapshot with { SessionVersion = generation }; Updated?.Invoke(this, Snapshot); }
    private void Report(string message) { if (!disposed) Notice?.Invoke(this, message); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; generation++; Detach();
        if (manager != null) { manager.SessionsChanged -= SessionsChanged; manager.CurrentSessionChanged -= CurrentSessionChanged; }
        manager = null; Updated = null; Notice = null;
    }
}
