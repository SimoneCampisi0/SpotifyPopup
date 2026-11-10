using System.Text.Json;

namespace SpotifyPopup;

public sealed class WindowPlacement
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 320;
}

public sealed class AppSettings
{
    public WindowPlacement Normal { get; set; } = new();
    public WindowPlacement Compact { get; set; } = new() { Width = 460, Height = 166 };
    public WindowPlacement Essential { get; set; } = new() { Width = 330, Height = 116 };
    public bool CompactMode { get; set; }
    public bool EssentialMode { get; set; }
    public bool ShowAlbum { get; set; } = true;
    public bool SnapToEdges { get; set; } = true;
    public double LockedOpacity { get; set; } = 0.58;
    public string ToggleShortcut { get; set; } = "Ctrl+Alt+P";
    public string UnlockShortcut { get; set; } = "Ctrl+Alt+U";
    public string PlayShortcut { get; set; } = "";
    public string PreviousShortcut { get; set; } = "";
    public string NextShortcut { get; set; } = "";
    public string BackwardShortcut { get; set; } = "";
    public string ForwardShortcut { get; set; } = "";
    public string Language { get; set; } = "auto";
    public bool StartWithWindows { get; set; }
    public bool StartInTray { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool CheckUpdatesOnStartup { get; set; }
    public string AccentColor { get; set; } = "#A9EACB";
    public int CoverSizePercent { get; set; } = 100;
    public bool ReduceMotion { get; set; }
    public string BackgroundMode { get; set; } = "artwork";
    public string BackgroundColor { get; set; } = "#24483C";
    public bool AnimateBackground { get; set; } = true;
    public bool HideWhenDisconnected { get; set; }
    public bool HideWhenPaused { get; set; }
    public int PauseDelaySeconds { get; set; } = 60;
    public AppSettings Clone() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;
    [System.Text.Json.Serialization.JsonIgnore]
    public string[] Shortcuts => new[] { ToggleShortcut, UnlockShortcut, PlayShortcut, PreviousShortcut, NextShortcut, BackwardShortcut, ForwardShortcut };
    public void Normalize()
    {
        Language = Localizer.Normalize(Language);
        Normal ??= new(); Compact ??= new() { Width = 460, Height = 166 }; Essential ??= new() { Width = 330, Height = 116 };
        NormalizePlacement(Normal, 360, 316, 420, 320);
        NormalizePlacement(Compact, 360, 160, 460, 166);
        NormalizePlacement(Essential, 280, 116, 330, 116);
        LockedOpacity = double.IsFinite(LockedOpacity) ? Math.Clamp(LockedOpacity, .25, 1) : .58;
        ToggleShortcut ??= "Ctrl+Alt+P"; UnlockShortcut ??= "Ctrl+Alt+U";
        PlayShortcut ??= ""; PreviousShortcut ??= ""; NextShortcut ??= ""; BackwardShortcut ??= ""; ForwardShortcut ??= "";
        BackgroundMode = BackgroundMode is "artwork" or "fixed" or "custom" ? BackgroundMode : "artwork";
        if (!ValidColor(BackgroundColor)) BackgroundColor = "#24483C";
        if (!ValidColor(AccentColor)) AccentColor = "#A9EACB";
        CoverSizePercent = Math.Clamp(CoverSizePercent, 75, 110);
        PauseDelaySeconds = Math.Clamp(PauseDelaySeconds, 10, 3600);
    }
    public static bool ValidColor(string? color) => color != null && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$");
    private static void NormalizePlacement(WindowPlacement p, double minWidth, double minHeight, double width, double height)
    {
        p.Width = double.IsFinite(p.Width) ? Math.Clamp(p.Width, minWidth, 2000) : width;
        p.Height = double.IsFinite(p.Height) ? Math.Clamp(p.Height, minHeight, 1200) : height;
    }
}

public sealed class SettingsStore
{
    public string Path { get; }
    private readonly string? legacy;
    public bool Portable { get; }
    public SettingsStore(string? path = null) : this(path, AppContext.BaseDirectory,
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpotifyPopup")) { }
    public SettingsStore(string? path, string baseDirectory, string userDirectory)
    {
        Portable = path == null && File.Exists(System.IO.Path.Combine(baseDirectory, "SpotifyPopup.portable"));
        Path = path ?? System.IO.Path.Combine(Portable ? baseDirectory : userDirectory, "SpotifyPopup.settings.json");
        if (path == null && !Portable) legacy = System.IO.Path.Combine(baseDirectory, "SpotifyPopup.settings.json");
    }
    public AppSettings Load(out string? warning)
    {
        warning = null;
        var source = File.Exists(Path) ? Path : legacy != null && File.Exists(legacy) ? legacy : null;
        if (source == null) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(source)) ?? throw new JsonException();
            settings.Normalize();
            if (source != Path && !Save(settings)) warning = TextKeys.SettingsSaveFailed;
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Diagnostics.Record("settings-read", ex);
            // Never overwrite the only copy of a corrupt file if its backup cannot be made.
            if (ex is JsonException)
            {
                try { File.Copy(source, source + ".corrupt-" + Guid.NewGuid().ToString("N")); }
                catch (Exception backupError) { Diagnostics.Record("settings-backup", backupError); blocked = true; }
            }
            warning = TextKeys.SettingsReadFailed; return new();
        }
    }
    private bool blocked;
    public bool Save(AppSettings settings)
    {
        if (blocked) return false;
        var temporary = Path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            settings.Normalize();
            var parent = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
            Directory.CreateDirectory(parent!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, Path, true); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        { Diagnostics.Record("settings-save", ex); return false; }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception) { } }
    }
}
