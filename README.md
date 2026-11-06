# Spotify Popup

Spotify Popup is a small, always-on-top Windows player for the Spotify desktop app. It displays the current track's artwork, title, artist, album, and playback progress, and provides restart, previous, play/pause, next, and seek controls through Windows media controls.

Click the restart button on the left of the playback controls to return the current track to the beginning, keeping its playing or paused state. Click anywhere on the progress bar to seek directly to that position, or drag its thumb to choose a position. Restart and seek are enabled when Spotify exposes playback-position control for the current track.

Drag the popup to move it, including to another monitor, and drag a window edge to resize it. Hover over the track title while the popup is unlocked to see the full title. Click the lock to make the popup semitransparent and allow clicks to pass through it; use the separate unlock button to interact with it again. Spotify determines which metadata and controls are available. The current version does not display the source playlist.

## Requirements

- Windows 10 version 2004 (build 19041) or later, or Windows 11.
- The Spotify desktop app, open with a track playing or paused.
- The .NET 10 SDK to build the project. The SDK includes the .NET Desktop Runtime needed to run this build.
- Internet access on the first build so NuGet can restore the Windows SDK reference package.

No Spotify developer account, API credentials, or extra SDK installation is required for the current Windows media integration.

## Install dependencies

Download and install the [.NET 10 SDK for Windows](https://dotnet.microsoft.com/download/dotnet/10.0). Verify the installation in PowerShell:

```powershell
dotnet --list-sdks
```

Alternatively, install the SDK **inside the project folder** without a system-wide installation. From the project root, run:

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile .\dotnet-install.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\dotnet-install.ps1 -Channel 10.0 -InstallDir "$PWD\.dotnet-sdk"
$env:DOTNET_ROOT = "$PWD\.dotnet-sdk"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet --list-sdks
```

The `DOTNET_ROOT` and `PATH` changes apply only to the current PowerShell session; repeat them in a new session. For details, see Microsoft's [dotnet-install documentation](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script).

## Create a standalone executable

To publish a single self-contained executable for 64-bit Windows, run from the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
```

The distributable is `core\bin\publish\win-x64\SpotifyPopup.exe`. Copy this executable on its own; it includes the .NET runtime and does not require a separate .NET installation on the target PC. The .NET SDK is still needed to build it. The first publish needs internet access to download the Windows runtime packs into the project's NuGet cache. The script keeps NuGet packages, .NET CLI data, temporary files and build output inside the project folder.

When launched, the single-file app extracts native runtime components to the Windows temporary directory (`%TEMP%\.net` by default). This is managed by .NET; it does not require distributing additional files with the executable.
## Run

Open Spotify and start playing a track. Then, from the project root, run:

```powershell
.\core\bin\publish\win-x64\SpotifyPopup.exe
```

You can also open that executable from File Explorer. The standalone executable includes the .NET runtime.

## Close or kill

To close normally, unlock the popup if needed, then click the **X** in the upper-left corner. You can also right-click the popup and choose **Chiudi**.

If the popup is unresponsive, open PowerShell and force-close only Spotify Popup:

```powershell
Stop-Process -Name SpotifyPopup -Force
```

This command does not close Spotify itself.






