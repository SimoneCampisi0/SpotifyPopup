using System.Windows.Threading;

namespace SpotifyPopup;

public sealed class MediaController : IMediaController
{
    private readonly Dispatcher dispatcher;
    private readonly Func<IMediaBackend> createBackend;
    private readonly TimeSpan timeout;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource sessionLife = new();
    private IMediaBackend? backend;
    private IMediaSession? session;
    private MediaPlayback? playback;
    private MediaTimeline? timeline;
    private bool disposed, refreshing, requested, metadataDirty, commandPending, initializing;
    private int generation, revision;
    private DateTimeOffset nextRecovery, nextArtworkAttempt;
    public MediaSnapshot Snapshot { get; private set; } = new();
    public event EventHandler<MediaSnapshot>? Updated;
    public event EventHandler<string>? Notice;
    public MediaController(Dispatcher dispatcher) : this(dispatcher, () => new WindowsMediaBackend(), TimeSpan.FromSeconds(8)) { }
    internal MediaController(Dispatcher dispatcher, Func<IMediaBackend> createBackend, TimeSpan timeout)
    { this.dispatcher = dispatcher; this.createBackend = createBackend; this.timeout = timeout; }
    private async Task<T> Deadline<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        deadline.CancelAfter(timeout);
        try { return await operation(deadline.Token).WaitAsync(timeout, cancellation); }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested && !lifetime.IsCancellationRequested) { throw new TimeoutException(); }
    }
    public async Task InitializeAsync()
    {
        if (disposed || backend != null || initializing) return;
        initializing = true;
        var candidate = createBackend();
        try
        {
            await Deadline(async token => { await candidate.InitializeAsync(token); return true; }, lifetime.Token);
            if (disposed) { candidate.Dispose(); return; }
            backend = candidate; backend.Changed += BackendChanged;
            await RefreshAsync(true);
        }
        catch (OperationCanceledException) { candidate.Dispose(); }
        catch (Exception ex) { candidate.Dispose(); Diagnostics.Record("media-initialize", ex); Reset(TextKeys.MediaUnavailable); }
        finally { initializing = false; }
    }
    private void BackendChanged(object? sender, EventArgs e) => QueueRefresh(true, true);
    private void SessionChanged(object? sender, bool metadata)
    {
        if (disposed || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed || sender != session) return;
            if (metadata) revision++;
            _ = RefreshAsync(metadata);
        }));
    }
    private void QueueRefresh(bool metadata, bool checkSession = false)
    {
        if (disposed || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;
            try { if (checkSession) Attach(backend?.Find()); }
            catch (Exception ex) { Recover(ex); }
            _ = RefreshAsync(metadata);
        }));
    }
    private void Attach(IMediaSession? value)
    {
        if (ReferenceEquals(session, value)) return;
        if (session != null) session.Changed -= SessionChanged;
        sessionLife.Cancel(); sessionLife.Dispose(); sessionLife = new();
        session = value; generation++; revision++; playback = null; timeline = null; metadataDirty = true;
        if (session != null) session.Changed += SessionChanged;
        Publish(new());
    }
    internal async Task RefreshAsync(bool metadata)
    {
        if (disposed || backend == null) return;
        requested = true; metadataDirty |= metadata;
        if (refreshing) return;
        refreshing = true;
        try
        {
            while (requested && !disposed && backend != null)
            {
                requested = false;
                Attach(backend.Find());
                var target = session; var version = generation; var readRevision = revision;
                if (target == null) { Reset(TextKeys.SpotifyUnavailable); continue; }
                var cancellation = sessionLife.Token;
                var read = metadataDirty; metadataDirty = false;
                try
                {
                    if (read)
                    {
                        var props = await Deadline(target.ReadAsync, cancellation);
                        if (disposed || generation != version || revision != readRevision) continue;
                        nextArtworkAttempt = DateTimeOffset.UtcNow.AddSeconds(5);
                        Publish(Snapshot with { Title = props.Title, Artist = props.Artist, Album = props.Album,
                            TitleFallbackKey = TextKeys.UnknownTrack, TrackKey = props.Key, Artwork = props.Artwork, ArtworkLoading = false });
                    }
                    if (disposed || generation != version) continue;
                    playback = target.Playback(); timeline = target.Timeline(); PublishPlayback();
                }
                catch (OperationCanceledException) when (disposed || cancellation.IsCancellationRequested) { }
            }
        }
        catch (Exception ex) { Recover(ex); }
        finally { refreshing = false; }
    }
    private void Recover(Exception ex)
    {
        Diagnostics.Record("media-recovery", ex);
        Attach(null);
        if (backend != null) { backend.Changed -= BackendChanged; backend.Dispose(); backend = null; }
        Reset(TextKeys.SpotifyUnavailable);
        Notice?.Invoke(this, ex is TimeoutException ? TextKeys.MediaTimedOut : TextKeys.Reconnecting);
    }
    private void PublishPlayback()
    {
        if (disposed || playback == null || timeline == null) return;
        var c = playback.Controls;
        Publish(Snapshot with
        {
            Connected = true, Playing = playback.Playing, Status = playback.Status,
            Position = PlaybackMath.Position(timeline.Position, timeline.Start, timeline.Duration,
                (DateTimeOffset.UtcNow - timeline.Updated).TotalSeconds, playback.Rate, playback.Playing),
            Duration = timeline.Duration, CanPlayPause = c.PlayPause || (playback.Playing ? c.Pause : c.Play),
            CanPrevious = c.Previous, CanNext = c.Next, CanSeek = c.Seek && timeline.Duration > 0
        });
    }
    public void Tick()
    {
        if (disposed) return;
        PublishPlayback();
        var now = DateTimeOffset.UtcNow;
        if (now < nextRecovery) return;
        nextRecovery = now.AddSeconds(5);
        if (backend == null) _ = InitializeAsync();
        else _ = RefreshAsync(session == null || (Snapshot.Artwork == null && now >= nextArtworkAttempt));
    }
    public async Task ExecuteAsync(MediaAction action, double fraction = 0, string? expectedTrackKey = null)
    {
        var target = session; var version = generation;
        if (disposed || target == null || commandPending) return;
        var cancellation = sessionLife.Token;
        commandPending = true;
        try
        {
            var p = target.Playback(); var c = p.Controls;
            var allowed = action switch
            {
                MediaAction.PlayPause => c.PlayPause || (p.Playing ? c.Pause : c.Play),
                MediaAction.Previous => c.Previous, MediaAction.Next => c.Next, _ => c.Seek
            };
            if (!allowed) { Notice?.Invoke(this, TextKeys.PlaybackUnavailable); return; }
            long ticks = 0;
            if (action == MediaAction.Seek)
            {
                var readRevision = revision;
                var props = await Deadline(target.ReadAsync, cancellation);
                if (disposed || version != generation || readRevision != revision || (expectedTrackKey != null && expectedTrackKey != props.Key))
                { if (!disposed) Notice?.Invoke(this, TextKeys.TrackChanged); return; }
                var t = target.Timeline();
                if (t.Duration <= 0) { Notice?.Invoke(this, TextKeys.SeekUnavailable); return; }
                ticks = PlaybackMath.SeekTicks(TimeSpan.FromSeconds(t.Start).Ticks, TimeSpan.FromSeconds(t.Duration).Ticks, fraction);
            }
            var accepted = await Deadline(token => target.ExecuteAsync(action, ticks, token), cancellation);
            if (!disposed && version == generation && !accepted) Notice?.Invoke(this, TextKeys.CommandRejected);
        }
        catch (OperationCanceledException) when (disposed || cancellation.IsCancellationRequested) { }
        catch (Exception ex) { if (!disposed && version == generation) Recover(ex); }
        finally { commandPending = false; if (!disposed) await RefreshAsync(true); }
    }
    private void Reset(string status) => Publish(new MediaSnapshot { Status = status });
    private void Publish(MediaSnapshot snapshot)
    {
        if (disposed) return;
        var value = snapshot with { SessionVersion = generation };
        if (value == Snapshot) return;
        Snapshot = value; Updated?.Invoke(this, value);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; lifetime.Cancel(); sessionLife.Cancel();
        if (session != null) session.Changed -= SessionChanged;
        if (backend != null) { backend.Changed -= BackendChanged; backend.Dispose(); }
        session = null; backend = null; Updated = null; Notice = null;
    }
}
