# Spotify Popup

A small, always-on-top Spotify player for Windows. Keep your music controls within reach while you work, browse, or play.

[Download for Windows](https://github.com/SimoneCampisi0/SpotifyPopup/releases/latest/download/SpotifyPopup.exe) · [Releases](https://github.com/SimoneCampisi0/SpotifyPopup/releases) · [Report an issue](https://github.com/SimoneCampisi0/SpotifyPopup/issues)

[![Windows checks and release](https://github.com/SimoneCampisi0/SpotifyPopup/actions/workflows/windows-release.yml/badge.svg)](https://github.com/SimoneCampisi0/SpotifyPopup/actions/workflows/windows-release.yml)

## Design in v1.5.0

The player uses a shared charcoal-and-mint design across the overlay, Settings, About and notification-area menu. Cover artwork, title and playback are the visual priorities. Controls keep their sizes when resizing the player; only artwork is scaled in smaller layouts.

- Open the **Spotify Popup menu** in the upper-left corner (or right-click the background) to choose Comfortable, Compact or Essential. A checkmark identifies the active layout.
- Use the top-right buttons for Settings, Lock and Exit. The separate unlock control remains available in locked mode.
- Settings has five sections: General, Appearance, Visibility, Shortcuts and Recovery. The footer keeps Save and Cancel available while a section scrolls.
- Switches, inputs, dropdowns, sliders, menus and keyboard focus indicators share the same theme. Dropdowns support keyboard selection; sliders retain mouse and keyboard seeking.
- To edit a shortcut, select its field and press a combination. Backspace clears it; Tab moves on and Escape cancels the dialog. The app suspends its own global shortcuts while Settings is open and restores them on closing.
- Custom-color controls appear only for a custom background; the swatch previews valid input without applying it. Pause-delay input is enabled only when the relevant automatic visibility option is on.
- Resetting all preferences uses an inline confirmation, then requires Save. Cancel continues to preserve existing preferences.
- About keeps the full offline license behind **Read the license**, with update and diagnostic actions together below it.

The executable declares Per-Monitor V2 DPI awareness. The Windows packaging check verifies the published window's DPI-awareness context; real monitor-to-monitor transitions still require hands-on verification. Existing preferences and positions are preserved, with dimensions clamped where needed to fit the redesigned layouts.

## Features

- **Windows startup:** optionally start at sign-in for your Windows account, with an independent tray-only startup option. Both are off by default.
- **Single instance:** opening the app again reveals the existing popup without changing its lock state.
- **Always-on-top popup:** move it around your desktop, resize it, and switch between normal, compact and essential layouts.
- **Spotify playback controls:** restart the current track, skip backward or forward, play/pause, and seek with the mouse or keyboard.
- **Track information:** cover artwork, title, artist, album, playback status, and progress. Hover over truncated metadata when unlocked to read the full text.
- **Background preferences:** choose artwork colors, a fixed dark theme or a custom RGB color, and optionally disable transitions. Custom colors are darkened for text contrast.
- **Artwork-based backgrounds:** a diagonal gradient follows the cover's colors, with smooth transitions, background palette processing, and a bounded in-memory cache.
- **Graceful artwork fallback:** missing or invalid covers use a dark default background; outdated results cannot replace a newer track's palette.
- **Lock mode:** make the popup semitransparent and let mouse clicks pass through it. A separate unlock button stays available.
- **Desktop convenience:** notification-area controls, configurable global shortcuts, optional edge snapping, and off-screen window recovery.
- **Automatic visibility:** optionally hide after Spotify disconnects or after a prolonged pause; playback restores automatically hidden windows.
- **Optional media shortcuts:** configure play/pause, previous/next and ten-second backward/forward seeks.
- **Recovery and diagnostics:** reset placement or preferences, copy bounded local diagnostic logs, and recover from media-operation timeouts.
- **On-demand update checks:** check stable GitHub releases from About and open the official download page.
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
3. Place it in a stable folder where you want to keep the app.
4. Open Spotify and start or pause a track.
5. Run **SpotifyPopup.exe**.

Each release also includes `SpotifyPopup.exe.sha256`. To calculate the executable's SHA-256 hash in PowerShell and compare it with that file:

```powershell
Get-FileHash .\SpotifyPopup.exe -Algorithm SHA256
```

By default, preferences are saved to `%LocalAppData%\SpotifyPopup\SpotifyPopup.settings.json`, so the executable folder does not need to be writable. Existing preferences beside the executable are copied automatically on the first launch when no user-profile settings exist; the original file is retained.

For **portable mode**, create an empty file named `SpotifyPopup.portable` beside `SpotifyPopup.exe` before starting it. Preferences then stay beside the executable and the folder must be writable. To move from user-profile storage to portable mode, exit the app and copy the current JSON beside the executable before adding the marker. To return to user-profile storage while keeping portable edits, exit the app and copy that JSON to the user-profile location before removing the marker. Existing destination settings take precedence.

Invalid JSON is backed up with a `.corrupt-<id>` suffix before defaults can replace it. If that backup cannot be made, saving is blocked for the session to preserve the original file.

Although the app is distributed as one executable, .NET extracts bundled native components to `%TEMP%\.net` by default at runtime.

## Using the popup

| Action | How |
| --- | --- |
| Move the popup | Drag an empty area of the window. |
| Resize | Drag a window edge or the resize grip. |
| Switch layouts | Open the Spotify Popup menu or right-click the background, then choose Comfortable, Compact or Essential. |
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

Settings also lets you adjust locked opacity, toggle album metadata, enable edge snapping and choose the interface language. The essential bar retains cover artwork, title, artist and play/pause; window controls become visible on hover or keyboard focus. Other playback commands remain available through configured shortcuts. Each layout remembers its own dimensions.

### Start with Windows and start in the notification area

Open **Settings → General → Startup**. The two options are independent and disabled by default:

| Start with Windows | Start in the notification area | Result |
| --- | --- | --- |
| Off | Off | Manual startup shows the popup. |
| Off | On | Manual startup creates only the notification icon. |
| On | Off | Sign-in starts the app and shows the popup. |
| On | On | Sign-in starts the app in the notification area. |

Choose **Save** to apply. Startup registration uses only the current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry named `SpotifyPopup`; administrator rights are not required. Paths containing spaces are quoted. Windows may also let you disable startup from its Startup Apps settings.

Keep the executable at the registered path. If you move it, run it from the new location and save Settings with startup enabled to repair registration. A missing or different entry is reported at startup when the saved preference is enabled. Disable startup before permanently deleting the app. An executable that has already been moved cannot repair its old entry until you launch it again.

Tray-only startup creates the notification icon before deciding to show the popup, avoiding a flash or focus grab. If the tray cannot be created, the window is shown instead. Double-click the icon, choose Show, use the show/hide shortcut or run the executable again to reveal it. Reopening preserves the lock state.

### Automatic visibility and shortcuts

Automatic visibility is optional. A disconnection has a five-second grace period. Paused playback uses the configured delay (10–3600 seconds). Resuming playback shows a popup hidden by the automatic policy without activating it. Manual show/hide takes precedence until connection or playback changes; saving Settings starts a new policy evaluation. Tray-only startup does not force the popup to appear.

Playback shortcuts are empty by default. Leave a field empty to disable that action. Duplicates and conflicts are rejected; previous registrations are restored if the new configuration cannot be registered. Seek shortcuts move ten seconds and require Spotify to expose seeking.

### Updates, recovery and diagnostics

Open **Settings → About** to check for updates or copy diagnostics. Update checks contact the official GitHub releases API only when requested, have a ten-second deadline, and offer the official release page for newer stable versions. Updates are installed manually.

Local diagnostic logs are stored under `%LocalAppData%\SpotifyPopup\Logs`; they rotate at approximately 256 KiB with one previous file. They record operation names, exception types, error codes and method names, excluding exception messages, paths, account identifiers and music metadata. Diagnostic failures do not stop the app.

**Reset window position and sizes** applies immediately to all three layouts. **Reset all preferences** asks for an inline confirmation, fills the dialog with defaults and requires Save; Cancel discards that reset. Saving defaults also disables Windows startup and optional shortcuts.

Media operations have an eight-second deadline. A timed-out operation releases the controller, reports recovery and cannot apply stale results later. Reconnection attempts continue automatically.

### Updating

Exit Spotify Popup, replace `SpotifyPopup.exe` with the executable from a newer release, and run it again. User-profile preferences are retained automatically. In portable mode, keep the marker and settings JSON beside the replacement executable.

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

The logic checks cover playback calculations, migration and portable persistence, corruption backup, visibility policy, release parsing, localization resources and palette extraction. The Windows UI checks use a simulated media controller to verify layouts, controls, asynchronous artwork updates, transitions, caching, lock/unlock behavior, Settings, and About without issuing real Spotify playback commands. Controller tests exercise the actual recovery logic through a simulated Windows boundary, including metadata/command/initialization timeouts, late results, session replacement and closing. Desktop checks cover all four startup combinations, no-flash tray initialization, startup command quoting and repair, essential layout and targeted timeline notifications.

The [Windows workflow](.github/workflows/windows-release.yml) also publishes the standalone executable, checks its icon and startup/shutdown behavior, and uploads the executable and checksum. On `main`, it publishes a new versioned release when the project version and matching release notes are ready.

Generated UI verification images are available as workflow artifacts. Real Spotify playback, actual sign-out/sign-in startup, screen-reader behavior and mixed-DPI multi-monitor interactions still require hands-on verification. In locked mode, readability over desktop content depends on the selected opacity.

## Project layout

| Path | Purpose |
| --- | --- |
| `core/` | Shared theme, WPF application, media integration, settings, localization, artwork backgrounds, and About dialog. |
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
