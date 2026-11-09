$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DpiCheck {
 [DllImport("user32.dll")] public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
 [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
}
'@
$exe = Join-Path $PWD 'core/bin/publish/win-x64/SpotifyPopup.exe'
if (!(Test-Path $exe) -or (Get-Item $exe).Length -lt 10MB) { throw 'Standalone executable missing or incomplete.' }
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exe)
if ($null -eq $icon) { throw 'Executable icon is missing.' }
$icon.Dispose()
$folder = Join-Path $PWD '.temp/published smoke'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$copy = Join-Path $folder 'SpotifyPopup.exe'
Copy-Item $exe $copy -Force
New-Item -ItemType File -Force -Path (Join-Path $folder 'SpotifyPopup.portable') | Out-Null
$settingsPath = Join-Path $folder 'SpotifyPopup.settings.json'
function Wait-Condition([scriptblock]$condition, [string]$message) {
  $deadline = [DateTime]::UtcNow.AddSeconds(15)
  while (!( & $condition )) {
    if ([DateTime]::UtcNow -gt $deadline) { throw $message }
    Start-Sleep -Milliseconds 200
  }
}
foreach ($tray in @($false, $true)) {
  @{ StartInTray = $tray; StartWithWindows = $false; Language = 'en' } | ConvertTo-Json | Set-Content $settingsPath
  $process = Start-Process $copy -PassThru
  try {
    Start-Sleep -Seconds 3
    $process.Refresh()
    if ($process.HasExited) { throw 'The published executable exited during startup.' }
    if ($tray) {
      if ($process.MainWindowHandle -ne 0) { throw 'Tray startup unexpectedly displayed a main window.' }
    } else {
      Wait-Condition { $process.Refresh(); $process.MainWindowHandle -ne 0 } 'The popup did not become visible.'
    }
    if (!$tray -and ![DpiCheck]::AreDpiAwarenessContextsEqual([DpiCheck]::GetWindowDpiAwarenessContext($process.MainWindowHandle), [IntPtr]::new(-4))) {
      throw 'Published window is not Per-Monitor V2 DPI-aware.'
    }
    $second = Start-Process $copy -PassThru
    try {
      if (!$second.WaitForExit(10000) -or $second.ExitCode -ne 0) { throw 'Second instance did not signal and exit normally.' }
    } finally {
      $second.Refresh(); if (!$second.HasExited) { Stop-Process -Id $second.Id -Force }
    }
    Wait-Condition { $process.Refresh(); $process.MainWindowHandle -ne 0 } 'Second launch did not reveal the original popup.'
    $null = $process.CloseMainWindow()
    if (!$process.WaitForExit(10000) -or $process.ExitCode -ne 0) { throw 'The published executable did not close normally.' }
  } finally {
    $process.Refresh(); if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
  }
}
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  SpotifyPopup.exe" | Set-Content 'core/bin/publish/win-x64/SpotifyPopup.exe.sha256' -Encoding ascii
Write-Host 'Published executable: icon, Per-Monitor V2 DPI awareness, normal/tray startup, paths with spaces, second-instance activation and clean shutdown passed.'
