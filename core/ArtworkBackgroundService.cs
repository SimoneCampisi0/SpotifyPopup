using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SpotifyPopup;

// Called on the window dispatcher; frozen bitmap sampling and palette analysis run on a worker.
internal sealed class ArtworkBackgroundService : IDisposable
{
    private const int CacheCapacity = 64;
    private readonly Dispatcher dispatcher;
    private readonly Action<ArtworkPalette> apply;
    private readonly Func<BitmapSource, CancellationToken, Task<ArtworkPalette>> analyze;
    private readonly Dictionary<string, ArtworkPalette> cache = new();
    private readonly Queue<string> cacheOrder = new();
    private readonly object cacheLock = new();
    private CancellationTokenSource? pending;
    private ImageSource? lastArtwork;
    private string? lastTrack;
    private int lastSession = -1;
    private bool lastLoading, disposed;
    private long request;
    internal int CachedPaletteCount { get { lock (cacheLock) return cache.Count; } }

    internal ArtworkBackgroundService(Dispatcher dispatcher, Action<ArtworkPalette> apply,
        Func<BitmapSource, CancellationToken, Task<ArtworkPalette>>? analyze = null)
    {
        this.dispatcher = dispatcher; this.apply = apply;
        this.analyze = analyze ?? ((image, token) => Task.Run(() => Analyze(image, token), token));
    }
    internal void Invalidate() => lastSession = -1;
    internal async Task UpdateAsync(MediaSnapshot snapshot)
    {
        dispatcher.VerifyAccess();
        if (disposed) return;
        if (lastSession == snapshot.SessionVersion && lastTrack == snapshot.TrackKey &&
            ReferenceEquals(lastArtwork, snapshot.Artwork) && lastLoading == snapshot.ArtworkLoading) return;
        lastSession = snapshot.SessionVersion; lastTrack = snapshot.TrackKey;
        lastArtwork = snapshot.Artwork; lastLoading = snapshot.ArtworkLoading;
        var version = ++request;
        pending?.Cancel();
        // Each request owns and disposes its source after the worker has finished.
        pending = null;
        if (snapshot.ArtworkLoading) return;
        if (snapshot.Artwork is not BitmapSource bitmap) { apply(ArtworkPalette.Default); return; }
        var cancellation = new CancellationTokenSource(); pending = cancellation;
        try
        {
            var image = bitmap.IsFrozen ? bitmap : bitmap.CloneCurrentValue();
            if (!image.IsFrozen) image.Freeze();
            var palette = await analyze(image, cancellation.Token);
            if (!disposed && request == version && !cancellation.IsCancellationRequested) apply(palette);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!disposed && request == version) apply(ArtworkPalette.Default);
        }
        finally
        {
            if (ReferenceEquals(pending, cancellation)) pending = null;
            cancellation.Dispose();
        }
    }
    private ArtworkPalette Analyze(BitmapSource image, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var scale = Math.Min(1, (double)ArtworkPaletteExtractor.ThumbnailSize / Math.Max(image.PixelWidth, image.PixelHeight));
        BitmapSource thumbnail = scale < 1 ? new TransformedBitmap(image, new ScaleTransform(scale, scale)) : image;
        var converted = new FormatConvertedBitmap(thumbnail, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        var key = Convert.ToHexString(SHA256.HashData(pixels));
        token.ThrowIfCancellationRequested();
        lock (cacheLock) if (cache.TryGetValue(key, out var existing)) return existing;
        var palette = ArtworkPaletteExtractor.Extract(pixels, token);
        lock (cacheLock)
        {
            token.ThrowIfCancellationRequested();
            if (!cache.ContainsKey(key))
            {
                if (cache.Count >= CacheCapacity) cache.Remove(cacheOrder.Dequeue());
                cache.Add(key, palette); cacheOrder.Enqueue(key);
            }
        }
        return palette;
    }
    public void Dispose()
    {
        dispatcher.VerifyAccess();
        if (disposed) return;
        disposed = true; request++; pending?.Cancel(); pending = null; lastArtwork = null;
        lock (cacheLock) { cache.Clear(); cacheOrder.Clear(); }
    }
}
