# Spotify Popup

A small, always-on-top Windows mini player for the Spotify desktop app. It shows the current track's artwork, title, artist, album and progress, and controls Spotify through Windows media sessions. The entire interface is in English.

## Download and run

1. Download `SpotifyPopup.exe` from [GitHub Releases](https://github.com/SimoneCampisi0/SpotifyPopup/releases/latest).
2. Put it in a writable folder of your choice.
3. Open Spotify and play or pause a track, then run the executable.

Requires **64-bit Windows 10 version 2004 (build 19041) or later, or Windows 11**, and the Spotify desktop app. The single-file download includes the .NET runtime: no separate .NET installation, Spotify developer account, API credentials or extra SDK is needed to run it.

Spotify determines which metadata and actions are available. Unsupported controls are disabled. The source playlist is not exposed by the current integration and is not displayed.

## Using the popup

- **Playback:** restart the current track, go to the previous/next track, or play/pause. Restart and seeking preserve Spotify's playback state and are available only when Spotify exposes position control.
- **Seek:** click the progress bar, drag its thumb, or focus the slider and use arrow keys, Page Up/Down, Home or End. The elapsed time previews your selection. Keyboard changes are committed on key release. If the track changes during selection, choose a position again.
- **Move and resize:** drag the background or metadata area; drag a window edge to resize. Optional edge snapping helps align the popup to a monitor's work area.
- **Layouts:** use the layout button in the top-right area, or right-click and choose **Switch layout**, to switch between the scalable normal view and the horizontal compact view. Each layout remembers its own size and position.
- **Metadata:** hover over the title, artist or album to read its full text while unlocked. Hide the album in Settings to simplify the display.
- **Status:** Playing, Paused, Stopped and connection states reflect the current session. When disconnected, **Open Spotify** opens the desktop app; play a track there to expose a session.

## Lock, hide and recover

Click **Lock popup** to make the player semitransparent and let clicks pass through to applications below it. Player controls cannot take keyboard input while locked. A separate **Unlock Spotify Popup** button stays accessible over the lock control.

The notification-area icon provides **Show/Hide**, **Lock/Unlock**, **Settings** and **Exit**. Double-click the icon to show the popup. Hiding keeps the app running; the top-left close button and **Exit** close it.

Default global shortcuts:

| Action | Shortcut |
| --- | --- |
| Show or hide popup | Ctrl+Alt+P |
| Unlock and show popup | Ctrl+Alt+U |

Change both shortcuts in **Settings**. Use a modifier combination such as `Ctrl+Alt+F8`; the app reports invalid combinations and conflicts instead of silently replacing a working configuration. If registration fails on startup, use the notification icon or visible Settings button to choose another combination.

Saved windows are brought back onto an available monitor when their previous screen is absent. The unlock helper follows the actual lock control when the popup moves, resizes or changes layout.

## Settings

Settings includes album visibility, edge snapping, locked opacity (25–100%, previewed on the popup), and both global shortcuts. The player returns to full opacity after the Settings dialog closes; the chosen opacity is used when locked. Save applies preferences; Cancel discards changes.

Preferences are saved to **`SpotifyPopup.settings.json` beside the executable**, including each layout's position and size and the last selected layout. Delete that file while the app is closed to reset preferences. No Spotify installation or data files are changed.

If the folder is read-only, the player remains usable and shows a message that preferences could not be saved. Move the app into a writable folder to remember preferences. Closing the app performs a final best-effort save.

## Build requirements

Building requires the **.NET 10 SDK** and internet access on the first restore/publish. Download it from [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0), or install it inside the project folder without a system-wide installation:

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile .\dotnet-install.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\dotnet-install.ps1 -Channel 10.0 -InstallDir "$PWD\.dotnet-sdk"
$env:DOTNET_ROOT = "$PWD\.dotnet-sdk"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet --list-sdks
```

The environment changes apply only to the current PowerShell session. See [Microsoft's dotnet-install documentation](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script).

## Publish a standalone executable

From the project root on Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
```

The distributable is **`core\bin\publish\win-x64\SpotifyPopup.exe`**. Copy the executable on its own. The first publish downloads the Windows runtime packs; the script keeps NuGet packages, CLI data, temporary files and build output inside the project.

At runtime, .NET extracts native runtime components to `%TEMP%\.net` by default. This does not require distributing additional files. When testing within the project, set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` to a directory inside the project before starting the executable.

## Verification and releases

Run from the project root, using the SDK and project-local dependency paths described above:

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
$env:NUGET_PACKAGES = "$PWD\.nuget\packages"
dotnet run --project tests/SpotifyPopup.LogicChecks -c Release
dotnet build tests/SpotifyPopup.Checks -c Release -warnaserror
dotnet run --project tests/SpotifyPopup.Checks -c Release --no-build
```

Logic checks cover timeline origins, interpolation, seek limits and preference persistence/recovery. Windows UI checks run with a simulated media controller, exercise unavailable controls, command failures, keyboard seeking, lock/unlock, off-screen recovery and layout persistence, and save minimum-size renderings under `test-results`. They do not issue commands to a real Spotify session.

The **Windows checks and release** workflow runs those checks, publishes the single-file app, checks its startup/shutdown and icon, and uploads build artifacts. On `main`, it publishes a new GitHub release only after those checks pass, using the version in `core/SpotifyPopup.csproj` and matching notes in `release-notes/vX.Y.Z.md`. Existing published versions are left untouched. Bump the version and add release notes for a subsequent release.

Hands-on verification with Spotify, a screen reader and mixed-DPI monitors remains necessary for behavior that isolated automated checks cannot reproduce. See [the implementation checklist](docs/implementation-checklist.md).

## Force close

Normally, unlock the popup and click the top-left **Exit Spotify Popup** button, or use **Exit** in the right-click/notification menu. If it becomes unresponsive, this PowerShell command closes only Spotify Popup:

```powershell
Stop-Process -Name SpotifyPopup -Force
```

It does not close Spotify itself.
