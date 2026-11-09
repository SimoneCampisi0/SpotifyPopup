using System.Net.Http;
using System.Text.Json;

namespace SpotifyPopup;

public sealed class UpdateService
{
    public const string DownloadUrl = "https://github.com/SimoneCampisi0/SpotifyPopup/releases/latest";
    private readonly HttpClient client;
    public UpdateService(HttpClient client) => this.client = client;
    public async Task<Version> LatestAsync(CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/SimoneCampisi0/SpotifyPopup/releases/latest");
        request.Headers.UserAgent.ParseAdd("SpotifyPopup/1.5.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var buffer = new MemoryStream();
        var bytes = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(bytes, cancellation)) != 0)
        {
            if (buffer.Length + read > 128 * 1024) throw new InvalidDataException();
            await buffer.WriteAsync(bytes.AsMemory(0, read), cancellation);
        }
        return Parse(buffer.ToArray());
    }
    public static Version Parse(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException();
        var tag = root.GetProperty("tag_name").GetString();
        if (tag == null || !System.Text.RegularExpressions.Regex.IsMatch(tag, "^v[0-9]+\\.[0-9]+\\.[0-9]+$") || !Version.TryParse(tag[1..], out var version)) throw new InvalidDataException();
        return version;
    }
}
