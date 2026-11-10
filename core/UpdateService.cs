using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SpotifyPopup;

public sealed record ReleasePackage(Version Version, Uri Executable, Uri Checksum);

public sealed class UpdateService
{
    private const string Repository = "SimoneCampisi0/SpotifyPopup";
    public const string DownloadUrl = "https://github.com/SimoneCampisi0/SpotifyPopup/releases/latest";
    private readonly HttpClient client;
    public UpdateService(HttpClient client) => this.client = client;

    public async Task<Version> LatestAsync(CancellationToken cancellation) =>
        Parse(await ReadLatestJsonAsync(cancellation));

    private async Task<byte[]> ReadLatestJsonAsync(CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/" + Repository + "/releases/latest");
        request.Headers.UserAgent.ParseAdd("SpotifyPopup/" + typeof(UpdateService).Assembly.GetName().Version?.ToString(3));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var buffer = new MemoryStream();
        var bytes = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(bytes, cancellation)) != 0)
        {
            if (buffer.Length + read > 128 * 1024) throw new InvalidDataException("Release metadata exceeded the limit.");
            await buffer.WriteAsync(bytes.AsMemory(0, read), cancellation);
        }
        return buffer.ToArray();
    }

    public static Version Parse(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException();
        var tag = root.GetProperty("tag_name").GetString();
        if (tag == null || !Regex.IsMatch(tag, "^v[0-9]+\\.[0-9]+\\.[0-9]+$") || !Version.TryParse(tag[1..], out var version))
            throw new InvalidDataException("Unexpected release tag.");
        return version;
    }

    public static ReleasePackage Package(byte[] json)
    {
        var version = Parse(json);
        using var document = JsonDocument.Parse(json);
        var assets = document.RootElement.GetProperty("assets").EnumerateArray();
        Uri? executable = null, checksum = null;
        foreach (var asset in assets)
        {
            var name = asset.GetProperty("name").GetString();
            if (name is not ("SpotifyPopup.exe" or "SpotifyPopup.exe.sha256")) continue;
            var url = new Uri(asset.GetProperty("browser_download_url").GetString()!, UriKind.Absolute);
            var path = $"/{Repository}/releases/download/v{version}/" + name;
            if (url.Scheme != Uri.UriSchemeHttps || url.Host != "github.com" || url.AbsolutePath != path)
                throw new InvalidDataException("Unexpected download destination.");
            if (name == "SpotifyPopup.exe") executable = url;
            else checksum = url;
        }
        return executable == null || checksum == null
            ? throw new InvalidDataException("Missing release assets.")
            : new(version, executable, checksum);
    }

    public async Task<string> DownloadVerifiedAsync(CancellationToken cancellation)
    {
        var package = Package(await ReadLatestJsonAsync(cancellation));
        var installed = typeof(UpdateService).Assembly.GetName().Version!;
        if (package.Version <= new Version(installed.Major, installed.Minor, installed.Build))
            throw new InvalidOperationException("There is no newer version to install.");

        using var hashResponse = await client.GetAsync(package.Checksum, HttpCompletionOption.ResponseHeadersRead, cancellation);
        hashResponse.EnsureSuccessStatusCode();
        if (hashResponse.Content.Headers.ContentLength is > 4096) throw new InvalidDataException("Checksum file too large.");
        var checksum = await hashResponse.Content.ReadAsStringAsync(cancellation);
        if (checksum.Length > 4096) throw new InvalidDataException("Checksum file too large.");
        var match = Regex.Match(checksum.Trim(), "^([a-fA-F0-9]{64})\\s+\\*?SpotifyPopup\\.exe$");
        if (!match.Success) throw new InvalidDataException("Invalid checksum file.");
        var expectedHash = match.Groups[1].Value;

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpotifyPopup", "Updates", "v" + package.Version);
        Directory.CreateDirectory(folder);
        var destination = Path.Combine(folder, "SpotifyPopup.exe");
        var temp = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using var response = await client.GetAsync(package.Executable, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 350_000_000) throw new InvalidDataException("Installer is too large.");
            using var source = await response.Content.ReadAsStreamAsync(cancellation);
            using var destinationStream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long size = 0; int n;
            while ((n = await source.ReadAsync(buffer, cancellation)) > 0)
            {
                size += n;
                if (size > 350_000_000) throw new InvalidDataException("Installer is too large.");
                sha.AppendData(buffer, 0, n);
                await destinationStream.WriteAsync(buffer.AsMemory(0, n), cancellation);
            }
            await destinationStream.FlushAsync(cancellation);
            if (size < 10_000_000 ||
                !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedHash), sha.GetHashAndReset()))
                throw new InvalidDataException("Release checksum mismatch.");
            File.Move(temp, destination, true);
            return destination;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
