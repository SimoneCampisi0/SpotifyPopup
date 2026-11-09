namespace SpotifyPopup;

public readonly record struct ArtworkColor(byte R, byte G, byte B);
public readonly record struct ArtworkPalette(ArtworkColor Start, ArtworkColor End)
{
    public static ArtworkPalette Default => new(new(47, 61, 57), new(12, 21, 21));
}

// Pure color analysis, independent of WPF and media sessions.
public static class ArtworkPaletteExtractor
{
    public const int ThumbnailSize = 64;
    private const int QuantizationShift = 4;
    private const double SecondaryMinimumShare = .04;
    private const double MinimumColorDistance = 48;
    private const double SaturationRetention = .82;
    private const double MaximumLuminance = .045;

    public static ArtworkPalette Extract(ReadOnlySpan<byte> bgra, CancellationToken cancellationToken = default)
    {
        var groups = new Dictionary<int, Bucket>();
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            var weight = bgra[i + 3] / 255d;
            if (weight == 0) continue;
            var b = bgra[i]; var g = bgra[i + 1]; var r = bgra[i + 2];
            var key = (r >> QuantizationShift) << 8 | (g >> QuantizationShift) << 4 | b >> QuantizationShift;
            groups.TryGetValue(key, out var bucket);
            groups[key] = new(bucket.Weight + weight, bucket.R + r * weight, bucket.G + g * weight, bucket.B + b * weight);
        }
        if (groups.Count == 0) return ArtworkPalette.Default;
        var sorted = groups.OrderByDescending(g => g.Value.Weight).ThenBy(g => g.Key).Select(g => g.Value).ToArray();
        var primary = sorted[0].Color;
        var total = sorted.Sum(g => g.Weight);
        var secondary = sorted.Skip(1)
            .Where(g => g.Weight / total >= SecondaryMinimumShare && Distance(primary, g.Color) >= MinimumColorDistance)
            .OrderByDescending(g => g.Weight * Distance(primary, g.Color)).FirstOrDefault();
        return new(Prepare(primary, 76), Prepare(secondary.Weight > 0 ? secondary.Color : primary, 46));
    }

    private static ArtworkColor Prepare(ArtworkColor color, double peak)
    {
        var gray = (color.R + color.G + color.B) / 3d;
        var r = gray + (color.R - gray) * SaturationRetention;
        var g = gray + (color.G - gray) * SaturationRetention;
        var b = gray + (color.B - gray) * SaturationRetention;
        var maximum = Math.Max(r, Math.Max(g, b));
        // Two restrained tonal variations also work for black and very dark monochrome covers.
        if (maximum == 0) return new((byte)(peak / 2), (byte)(peak / 2), (byte)(peak / 2));
        var scale = peak / maximum;
        var result = new ArtworkColor((byte)(r * scale), (byte)(g * scale), (byte)(b * scale));
        while (Luminance(result) > MaximumLuminance)
            result = new((byte)(result.R * .95), (byte)(result.G * .95), (byte)(result.B * .95));
        return result;
    }
    public static double Luminance(ArtworkColor color)
    {
        static double Linear(byte channel) { var value = channel / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }
    private static double Distance(ArtworkColor a, ArtworkColor b) => Math.Sqrt(Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));
    private readonly record struct Bucket(double Weight, double R, double G, double B)
    {
        public ArtworkColor Color => new((byte)(R / Weight), (byte)(G / Weight), (byte)(B / Weight));
    }
}
