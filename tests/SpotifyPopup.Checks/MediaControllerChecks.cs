using System.Windows.Threading;
using SpotifyPopup;

internal static class MediaControllerChecks
{
    internal static async Task<int> Run()
    {
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        var sameMetadataFirst = new MediaMetadata("Same", "Artist", "Album", null, 1);
        var sameMetadataSecond = sameMetadataFirst with { MetadataRevision = 2 };
        Check(sameMetadataFirst.Key != sameMetadataSecond.Key, "Identical metadata uses separate session revisions");
        Check(new MediaMetadata("A\\nB", "C", "D", null).Key !=
              new MediaMetadata("A", "B\\nC", "D", null).Key, "Field lengths prevent track identity collisions");
        var backend = new Backend();
        var controller = new MediaController(Dispatcher.CurrentDispatcher, () => backend, TimeSpan.FromMilliseconds(80));
        var notices = new List<string>(); controller.Notice += (_, key) => notices.Add(key);
        await controller.InitializeAsync();
        Check(controller.Snapshot.Connected && controller.Snapshot.Title == "one", "Real controller publishes backend metadata and capabilities");
        backend.Selected.Accept = false;
        await controller.ExecuteAsync(MediaAction.Next);
        Check(notices.Contains(TextKeys.CommandRejected), "Real controller reports command rejection");
        backend.Selected.Caps = new(false, false, false, false, false, false);
        await controller.RefreshAsync(false);
        Check(!controller.Snapshot.CanSeek && !controller.Snapshot.CanPlayPause, "Real controller respects unsupported capabilities");
        backend.Selected.Caps = new(true, true, true, true, true, true);
        await controller.ExecuteAsync(MediaAction.Seek, .5, "different-track");
        Check(notices.Contains(TextKeys.TrackChanged), "Real controller rejects seeks against stale track identity");
        var late = new TaskCompletionSource<MediaMetadata>(); backend.Selected.Pending = late;
        await controller.RefreshAsync(true);
        Check(!controller.Snapshot.Connected && notices.Contains(TextKeys.MediaTimedOut), "Metadata timeout releases refresh and clears stale session");
        backend = new Backend();
        await controller.InitializeAsync();
        late.SetResult(new("obsolete", "", "", null)); await Task.Delay(20);
        Check(controller.Snapshot.Connected && controller.Snapshot.Title == "one", "Late timed-out metadata cannot replace a recovered session");
        var oldSession = backend.Selected;
        var oldRead = new TaskCompletionSource<MediaMetadata>(); oldSession.Pending = oldRead;
        var refresh = controller.RefreshAsync(true);
        backend.Selected = new Session { Title = "two" }; backend.Signal();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await refresh; await controller.RefreshAsync(true);
        oldRead.SetResult(new("obsolete-session", "", "", null)); await Task.Delay(20);
        Check(controller.Snapshot.Title == "two", "Session replacement cancels obsolete in-flight reads");
        backend.Selected.PendingCommand = new TaskCompletionSource<bool>();
        await controller.ExecuteAsync(MediaAction.Next);
        Check(notices.Count(k => k == TextKeys.MediaTimedOut) == 2, "Command timeout does not retain the command lock");
        backend = new Backend(); await controller.InitializeAsync();
        backend.Selected.Accept = true; await controller.ExecuteAsync(MediaAction.Next);
        Check(backend.Selected.Commands == 1 && controller.Snapshot.Connected, "Playback commands work after timeout recovery");
        var closing = new TaskCompletionSource<MediaMetadata>(); backend.Selected.Pending = closing;
        var work = controller.RefreshAsync(true); controller.Dispose(); await work;
        closing.SetResult(new("after-close", "", "", null)); await Task.Delay(20);
        Check(controller.Snapshot.Title != "after-close", "Closing invalidates pending media results");
        var initializing = new Backend { Initialization = new TaskCompletionSource<bool>() };
        using var failed = new MediaController(Dispatcher.CurrentDispatcher, () => initializing, TimeSpan.FromMilliseconds(40));
        await failed.InitializeAsync();
        Check(!failed.Snapshot.Connected && initializing.Disposed, "Initialization timeout disposes the abandoned backend");
        return count;
    }
    private sealed class Backend : IMediaBackend
    {
        public Session Selected = new(); public bool Disposed; public TaskCompletionSource<bool>? Initialization;
        public event EventHandler? Changed;
        public async Task InitializeAsync(CancellationToken token) { if (Initialization != null) await Initialization.Task.WaitAsync(token); }
        public IMediaSession Find() => Selected;
        public void Signal() => Changed?.Invoke(this, EventArgs.Empty);
        public void Dispose() { Disposed = true; }
    }
    private sealed class Session : IMediaSession
    {
        public string Title = "one"; public bool Accept = true; public int Commands;
        public MediaCapabilities Caps = new(true, true, true, true, true, true);
        public TaskCompletionSource<MediaMetadata>? Pending;
        public TaskCompletionSource<bool>? PendingCommand;
        public event EventHandler<bool>? Changed { add { } remove { } }
        public Task<MediaMetadata> ReadAsync(CancellationToken token) => Pending?.Task ?? Task.FromResult(new MediaMetadata(Title, "artist", "album", null));
        public MediaPlayback Playback() => new(true, TextKeys.Playing, 1, Caps);
        public MediaTimeline Timeline() => new(0, 200, 10, DateTimeOffset.UtcNow);
        public Task<bool> ExecuteAsync(MediaAction action, long position, CancellationToken token) { Commands++; return PendingCommand?.Task ?? Task.FromResult(Accept); }
        public void Dispose() { }
    }
}
