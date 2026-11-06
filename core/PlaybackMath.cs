namespace SpotifyPopup;

public static class PlaybackMath
{
    public static double Position(double absolutePosition, double start, double duration, double elapsed, double rate, bool playing)
    {
        var position = absolutePosition - start + (playing ? Math.Max(0, elapsed) * rate : 0);
        return double.IsFinite(position) && duration > 0 ? Math.Clamp(position, 0, duration) : 0;
    }
    public static long SeekTicks(long start, long duration, double fraction) => start + (long)(Math.Max(0, duration) * Math.Clamp(double.IsFinite(fraction) ? fraction : 0, 0, 1));
    public static string Format(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return "0:00";
        var span = TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.MaxValue.TotalSeconds - 1));
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{(int)span.TotalMinutes}:{span.Seconds:00}";
    }
}
