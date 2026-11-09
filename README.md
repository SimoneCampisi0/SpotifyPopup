# Spotify Popup

A small, always-on-top Spotify player for Windows. Keep your music controls within reach while you work, browse, or play.

[Download for Windows](https://github.com/SimoneCampisi0/SpotifyPopup/releases/latest/download/SpotifyPopup.exe) · [Releases](https://github.com/SimoneCampisi0/SpotifyPopup/releases) · [Report an issue](https://github.com/SimoneCampisi0/SpotifyPopup/issues)

[![Windows checks and release](https://github.com/SimoneCampisi0/SpotifyPopup/actions/workflows/windows-release.yml/badge.svg)](https://github.com/SimoneCampisi0/SpotifyPopup/actions/workflows/windows-release.yml)

## Features

- **Always-on-top popup:** move it around your desktop, resize it, and switch between normal and compact layouts.
- **Spotify playback controls:** restart the current track, skip backward or forward, play/pause, and seek with the mouse or keyboard.
- **Track information:** cover artwork, title, artist, album, playback status, and progress. Hover over truncated metadata when unlocked to read the full text.
- **Artwork-based backgrounds:** a diagonal gradient follows the cover's colors, with smooth transitions, background palette processing, and a bounded in-memory cache.
- **Graceful artwork fallback:** missing or invalid covers use a dark default background; outdated results cannot replace a newer track's palette.
- **Lock mode:** make the popup semitransparent and let mouse clicks pass through it. A separate unlock button stays available.
- **Desktop convenience:** notification-area controls, configurable global shortcuts, optional edge snapping, and off-screen window recovery.
- **Saved preferences:** window placement, dimensions, layout, album visibility, locked opacity, shortcuts, and language.
- **English and Italian interface:** choose a language or follow the system language in Settings.
- **About dialog:** open Settings → About for the app logo, version, author, and complete GPL v3.0 license text, available offline.

## Requirements

To run the downloaded executable:

- 64-bit Windows 10 version 2004 (build 19041) or later, or Windows 11.
- The Spotify desktop application, open with a track playing or paused.

The release executable is **self-contained**: a separate .NET installation is not required. Spotify Popup uses Windows media sessions and does not require a Spotify developer account, API credentials, or a separate Spotify authorization flow.

Metadata and available controls depend on what Spotify exposes to Windows. Spotify Popup controls the Spotify desktop app; it does not play audio itself. The source playlist is not available through the current integration.

## Download and run

1. Open the [latest release](https://github.com/SimoneCampisi0/SpotifyPopup/releases/latest).
2. Download **SpotifyPopup.exe** from the release assets.
3. Place it in a writable folder where you want to keep the app.
4. Open Spotify and start or pause a track.
5. Run **SpotifyPopup.exe**.

Each release also includes `SpotifyPopup.exe.sha256`. To calculate the executable's SHA-256 hash in PowerShell and compare it with that file:

```powershell
Get-FileHash .\SpotifyPopup.exe -Algorithm SHA256
```

The app saves `SpotifyPopup.settings.json` beside the executable. If the folder is not writable, the app remains usable and reports that preferences could not be saved.

Although the app is distributed as one executable, .NET extracts bundled native components to `%TEMP%\.net` by default at runtime.

## Using the popup

| Action | How |
| --- | --- |
| Move the popup | Drag an empty area of the window. |
| Resize | Drag a window edge or the resize grip. |
| Switch layouts | Use the layout button or the background context menu. |
| Read long metadata | Hover over the text while the popup is unlocked. |
| Seek | Click or drag the progress slider, or focus it and use the keyboard. |
| Lock | Select the lock button. The popup becomes semitransparent and passes clicks through. |
| Unlock | Select the separate unlock button, use the unlock shortcut, or choose Unlock from the notification-area menu. |
| Open Settings | Use the settings button, background context menu, or notification-area menu. |
| Hide or show | Use the notification-area menu or the show/hide shortcut. |
| Exit | Use the popup's close button or Exit in the notification-area menu. Hiding the popup keeps the app running. |

The default global shortcuts are:

| Shortcut | Action |
| --- | --- |
| `Ctrl+Alt+P` | Show or hide the popup. |
| `Ctrl+Alt+U` | Unlock and show the popup. |

Both shortcuts are configurable in Settings. If a shortcut is already registered by another application, Spotify Popup reports the conflict.

Settings also lets you adjust locked opacity, toggle album metadata, enable or disable edge snapping, and choose the interface language. Select **About** at the bottom of Settings to view the app's author and license. The About dialog's text is in English.

### Updating

Exit Spotify Popup, replace `SpotifyPopup.exe` with the executable from a newer release, and run it again. Keep `SpotifyPopup.settings.json` in the same folder to retain your preferences.

### If Spotify is not detected

Open the Spotify desktop application and play or pause a track. Spotify Popup refreshes media-session information and attempts to reconnect automatically. The popup also provides an **Open Spotify** action when disconnected.

If a particular playback control is disabled, Spotify may not currently expose that action through its Windows media session.

## Build from source

Building requires **Windows**, the **.NET 10 SDK**, Git, and PowerShell.

```powershell
git clone https://github.com/SimoneCampisi0/SpotifyPopup.git
cd SpotifyPopup
powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
```

The script publishes a self-contained, single-file executable for `win-x64` to:

```text
core\bin\publish\win-x64\SpotifyPopup.exe
```

It keeps NuGet packages, CLI data, temporary files, and build output inside the project. Restore and publishing may need network access to download missing SDK reference or runtime packs.

Run the published app with:

```powershell
& .\core\bin\publish\win-x64\SpotifyPopup.exe
```

The application is written in C# and WPF and targets `net10.0-windows10.0.19041.0`.

## Verification

From the repository root on Windows, configure project-local directories and run the checks:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.nuget\packages'
$env:TEMP = Join-Path $PWD '.temp'
$env:TMP = $env:TEMP
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $PWD '.temp\bundle'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null

dotnet run --project tests/SpotifyPopup.LogicChecks -c Release
dotnet build tests/SpotifyPopup.Checks -c Release -warnaserror
dotnet run --project tests/SpotifyPopup.Checks -c Release --no-build
```

The logic checks cover playback calculations, settings persistence, localization resources, and palette extraction. The Windows UI checks use a simulated media controller to verify layouts, controls, asynchronous artwork updates, transitions, caching, lock/unlock behavior, Settings, and About without issuing real Spotify playback commands.

The [Windows workflow](.github/workflows/windows-release.yml) also publishes the standalone executable, checks its icon and startup/shutdown behavior, and uploads the executable and checksum. On `main`, it publishes a new versioned release when the project version and matching release notes are ready.

Generated UI verification images are available as workflow artifacts. Real Spotify playback, screen-reader behavior, and mixed-DPI multi-monitor interactions still require hands-on verification. In locked mode, readability over desktop content depends on the selected opacity.

## Project layout

| Path | Purpose |
| --- | --- |
| `core/` | WPF application, media integration, settings, localization, artwork backgrounds, and About dialog. |
| `assets/logo.ico` | Original application icon. |
| `tests/SpotifyPopup.LogicChecks/` | Logic regression checks. |
| `tests/SpotifyPopup.Checks/` | Isolated Windows UI checks. |
| `publish.ps1` | Windows standalone publishing script. |
| `release-notes/` | Release notes and verification details. |
| `.github/workflows/windows-release.yml` | Windows checks, packaging, and release automation. |
| `AGENTS.md` | Instructions for contributors using coding agents. |

## Author and license

Created by **[Simone Campisi](https://github.com/SimoneCampisi0)**.

Spotify Popup is distributed under the **GNU General Public License v3.0**. See [LICENSE](LICENSE) for the full terms.

This is an independent project and is not affiliated with or endorsed by Spotify.
