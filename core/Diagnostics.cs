using System.Text;

namespace SpotifyPopup;

public static class Diagnostics
{
    private static readonly object Gate = new();
    public static string DirectoryPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpotifyPopup", "Logs");
    public static void Record(string operation, Exception error)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                var path = Path.Combine(DirectoryPath, "diagnostic.log");
                if (File.Exists(path) && new FileInfo(path).Length >= 256 * 1024)
                    File.Move(path, path + ".previous", true);
                // Do not record paths, account identifiers, track metadata or exception messages.
                var frames = new System.Diagnostics.StackTrace(error, false).GetFrames();
                var trace = string.Join("\n", frames?.Take(12).Select(f => f.GetMethod()?.DeclaringType?.Name + "." + f.GetMethod()?.Name) ?? Array.Empty<string>());
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} v{typeof(Diagnostics).Assembly.GetName().Version} {operation}: {error.GetType().Name} (0x{error.HResult:X8})\n{trace}\n", Encoding.UTF8);
            }
        }
        catch (Exception) { /* Diagnostics must never prevent normal application use. */ }
    }
    public static string Report()
    {
        var header = $"Spotify Popup {typeof(Diagnostics).Assembly.GetName().Version}\nOS: {Environment.OSVersion.Version}\nRuntime: {Environment.Version}\n";
        try { lock (Gate) return header + File.ReadAllText(Path.Combine(DirectoryPath, "diagnostic.log")); }
        catch (Exception) { return header; }
    }
}
