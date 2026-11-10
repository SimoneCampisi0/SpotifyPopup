using System.Diagnostics;
using System.Text;

namespace SpotifyPopup;

// A separate process is required: a running Windows executable cannot replace itself.
// The helper preserves the old file for rollback and only runs after an explicit user click.
internal static class UpdateInstaller
{
    internal static void Launch(string verifiedExecutable)
    {
        var target = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");
        if (!File.Exists(target) || !File.Exists(verifiedExecutable))
            throw new FileNotFoundException("Update files are missing.");
        if (Path.GetFullPath(target).Equals(Path.GetFullPath(verifiedExecutable), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The update cannot overwrite its own staging file.");
        var folder = Path.GetDirectoryName(verifiedExecutable)!;
        var script = Path.Combine(folder, "install-update.ps1");
        File.WriteAllText(script, """
param(
    [Parameter(Mandatory=$true)][string]$Target,
    [Parameter(Mandatory=$true)][string]$Package,
    [Parameter(Mandatory=$true)][int]$ParentPid
)
$ErrorActionPreference = 'Stop'
$backup = "$Target.previous"
try {
    if (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue) {
        Wait-Process -Id $ParentPid -Timeout 90 -ErrorAction Stop
    }
    Copy-Item -LiteralPath $Target -Destination $backup -Force
    Copy-Item -LiteralPath $Package -Destination $Target -Force
    Start-Process -FilePath $Target
    Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
}
catch {
    if (Test-Path -LiteralPath $backup) {
        try { Copy-Item -LiteralPath $backup -Destination $Target -Force } catch {}
    }
    Add-Content -LiteralPath "$Package.update-error.txt" -Value $_
    exit 1
}
""", new UTF8Encoding(false));
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-Target");
        start.ArgumentList.Add(target);
        start.ArgumentList.Add("-Package");
        start.ArgumentList.Add(verifiedExecutable);
        start.ArgumentList.Add("-ParentPid");
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var process = Process.Start(start);
        if (process == null) throw new InvalidOperationException("Could not start update helper.");
    }
}
