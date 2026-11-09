using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SpotifyPopup;

internal static class ArtworkChecks
{
    internal static async Task<int> Run(MainWindow window, Action<MediaSnapshot> publish, string output)
    {
        var count = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name); count++;
        }
        var red = Cover(255, 0, 0); var blue = Cover(0, 0, 255);
        var pending = new Queue<TaskCompletionSource<ArtworkPalette>>();
        var applied = new List<ArtworkPalette>(); var analyses = 0;
        using (var service = new ArtworkBackgroundService(window.Dispatcher, applied.Add, (_, _) =>
        {
            analyses++;
            var completion = new TaskCompletionSource<ArtworkPalette>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Enqueue(completion); return completion.Task;
        }))
        {
            var first = new MediaSnapshot { Artwork = red, TrackKey = "first", SessionVersion = 1 };
            var old = service.UpdateAsync(first); var oldResult = pending.Dequeue();
            await service.UpdateAsync(first with { Position = 20, Playing = true });
            Check(analyses == 1, "Progress and playback updates do not restart palette extraction");
            var current = service.UpdateAsync(first with { Artwork = blue, TrackKey = "second" }); var newResult = pending.Dequeue();
            var bluePalette = new ArtworkPalette(new(10, 15, 70), new(5, 8, 40));
            newResult.SetResult(bluePalette); await current;
            oldResult.SetResult(new(new(70, 10, 10), new(40, 5, 5))); await old;
            Check(applied.Count == 1 && applied[0] == bluePalette, "A late previous-track result cannot overwrite the current palette");
            await service.UpdateAsync(new() { TrackKey = "loading", ArtworkLoading = true, SessionVersion = 1 });
            Check(applied.Count == 1, "Loading artwork preserves the visible palette");
            await service.UpdateAsync(new() { TrackKey = "loading", SessionVersion = 1 });
            Check(applied.Last() == ArtworkPalette.Default, "Confirmed missing artwork restores the fallback");
            var sessionOld = service.UpdateAsync(first); var sessionOldResult = pending.Dequeue();
            var sessionNew = service.UpdateAsync(first with { SessionVersion = 2 }); var sessionNewResult = pending.Dequeue();
            sessionNewResult.SetResult(bluePalette); await sessionNew;
            var before = applied.Count;
            sessionOldResult.SetResult(ArtworkPalette.Default); await sessionOld;
            Check(applied.Count == before, "Session replacement invalidates results even for the same track and image");
            var closing = service.UpdateAsync(first with { TrackKey = "closing" }); var closingResult = pending.Dequeue();
            before = applied.Count; service.Dispose(); closingResult.SetResult(bluePalette); await closing;
            Check(applied.Count == before, "Closing prevents a pending palette from reaching the window");
        }
        using (var failed = new ArtworkBackgroundService(window.Dispatcher, applied.Add, (_, _) => Task.FromException<ArtworkPalette>(new InvalidOperationException("Invalid artwork"))))
        {
            await failed.UpdateAsync(new() { Artwork = red });
            Check(applied.Last() == ArtworkPalette.Default, "Artwork analysis errors restore the fallback without escaping");
        }
        using (var real = new ArtworkBackgroundService(window.Dispatcher, applied.Add))
        {
            var original = new MediaSnapshot { Artwork = red, TrackKey = "album-one" };
            await real.UpdateAsync(original);
            var expected = applied.Last();
            await real.UpdateAsync(original with { Artwork = Cover(255, 0, 0), TrackKey = "album-two" });
            Check(real.CachedPaletteCount == 1 && applied.Last() == expected, "Identical cover pixels share a cached palette across bitmap objects and tracks");
            await real.UpdateAsync(original with { Artwork = Cover(255, 0, 0, 0) });
            Check(applied.Last() == ArtworkPalette.Default, "Actual transparent bitmap falls back");
            for (var i = 0; i < 70; i++) await real.UpdateAsync(original with { Artwork = Cover((byte)i, 120, 80), TrackKey = "cache-" + i });
            Check(real.CachedPaletteCount == 64, "Artwork cache remains bounded after many different covers");
        }
        var track = new MediaSnapshot { Artwork = red, TrackKey = "gradient", Title = "A long track title for artwork background validation", Artist = "Artist", Album = "Album", Connected = true };
        publish(track);
        var start = (GradientStop)window.FindName("BackgroundStart");
        var end = (GradientStop)window.FindName("BackgroundEnd");
        await Until(() => start.Color.R > start.Color.B * 2, "Red gradient did not reach the window");
        await Task.Delay(600);
        Check(start.Color.R > start.Color.B * 2 && end.Color.R > end.Color.B * 2, "Actual window uses the extracted gradient");
        Save(window, Path.Combine(output, "artwork-red.png"));
        publish(track with { Artwork = blue });
        await Until(() => start.Color.B > start.Color.R * 2, "Blue gradient did not reach the window");
        // Interrupt the current transition and verify that its current color is the next starting point.
        var visible = start.Color;
        publish(track with { Artwork = null });
        Check(Math.Abs(start.Color.R - visible.R) <= 2 && Math.Abs(start.Color.B - visible.B) <= 2, "Interrupted transitions continue from the visible color");
        await Task.Delay(600);
        Check(start.Color.R == ArtworkPalette.Default.Start.R && end.Color.B == ArtworkPalette.Default.End.B, "Missing artwork animates back to the default");
        foreach (var (name, image) in new[] { ("blue", blue), ("white", Cover(255, 255, 255)), ("black", Cover(0, 0, 0)), ("green", Cover(0, 255, 0)), ("two-color", TwoColorCover()) })
        {
            publish(track with { Artwork = image, TrackKey = name });
            var expected = ArtworkPaletteExtractor.Extract(Pixels(image));
            await Until(() => start.Color.R == expected.Start.R && start.Color.G == expected.Start.G && start.Color.B == expected.Start.B, "Gradient did not settle: " + name);
            await Task.Delay(100); Save(window, Path.Combine(output, "artwork-" + name + ".png"));
        }
        window.ToggleCompact(); await Task.Delay(100); Save(window, Path.Combine(output, "artwork-compact.png"));
        window.SetLocked(true); await Task.Delay(100); Save(window, Path.Combine(output, "artwork-locked.png"));
        window.SetLocked(false); window.ToggleCompact(); publish(new()); await Task.Delay(600);
        return count;
    }
    internal static BitmapSource Cover(byte r, byte g, byte b, byte a = 255) => BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
        Enumerable.Range(0, 4).SelectMany(_ => new[] { b, g, r, a }).ToArray(), 8);
    private static BitmapSource TwoColorCover() => BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
        new byte[] { 0, 0, 255, 255, 0, 0, 255, 255, 255, 0, 0, 255, 255, 0, 0, 255 }, 8);
    private static byte[] Pixels(BitmapSource image) { var pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(pixels, image.PixelWidth * 4, 0); return pixels; }
    private static async Task Until(Func<bool> condition, string failure)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition()) { if (DateTime.UtcNow >= deadline) throw new Exception(failure); await Task.Delay(20); }
    }
    private static void Save(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
