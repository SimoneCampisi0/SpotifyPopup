using System.Text.Json;

namespace SpotifyPopup;

public sealed class WindowPlacement
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public double Width { get; set; } = 390;
    public double Height { get; set; } = 270;
}

public sealed class AppSettings
{
    public WindowPlacement Normal { get; set; } = new();
    public WindowPlacement Compact { get; set; } = new() { Width = 460, Height = 142 };
    public bool CompactMode { get; set; }
    public bool ShowAlbum { get; set; } = true;
    public bool SnapToEdges { get; set; } = true;
    public double LockedOpacity { get; set; } = 0.58;
    public string ToggleShortcut { get; set; } = "Ctrl+Alt+P";
    public string UnlockShortcut { get; set; } = "Ctrl+Alt+U";
    public string Language { get; set; } = "auto";

    public void Normalize()
    {
        Language = Localizer.Normalize(Language);
        Normal ??= new(); Compact ??= new() { Width = 460, Height = 142 };
        NormalizePlacement(Normal, 330, 255, 390, 270);
        NormalizePlacement(Compact, 360, 138, 460, 142);
        LockedOpacity = double.IsFinite(LockedOpacity) ? Math.Clamp(LockedOpacity, 0.25, 1) : 0.58;
        ToggleShortcut ??= "Ctrl+Alt+P"; UnlockShortcut ??= "Ctrl+Alt+U";
    }
    private static void NormalizePlacement(WindowPlacement p, double minWidth, double minHeight, double width, double height)
    {
        p.Width = double.IsFinite(p.Width) ? Math.Clamp(p.Width, minWidth, 2000) : width;
        p.Height = double.IsFinite(p.Height) ? Math.Clamp(p.Height, minHeight, 1200) : height;
    }
}

public sealed class SettingsStore
{
    public string Path { get; }
    public SettingsStore(string? path = null) => Path = path ?? System.IO.Path.Combine(AppContext.BaseDirectory, "SpotifyPopup.settings.json");
    public AppSettings Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(Path)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new();
            settings.Normalize(); return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        { warning = TextKeys.SettingsReadFailed; return new(); }
    }
    public bool Save(AppSettings settings)
    {
        var temporary = Path + ".tmp";
        try
        {
            settings.Normalize();
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, Path, true); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        { return false; }
    }
}
