# Spotify Popup

Spotify Popup is a small, always-on-top Windows player for the Spotify desktop app. It displays the current track's artwork, title, artist, album, and playback progress, and provides previous, play/pause, next, and seek controls through Windows media controls.

Drag the popup to move it, including to another monitor. Click the lock to make it semitransparent and allow clicks to pass through it; use the separate unlock button to interact with it again. Spotify determines which metadata and controls are available. The current version does not display the source playlist.

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

## Build

From the project root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The script restores the required NuGet packages and builds the Debug configuration. It keeps NuGet packages, .NET CLI files, application data used during the build, and temporary files inside the project folder. Build output is placed under `SpotifyPopup\bin\` and `SpotifyPopup\obj\`.

## Run

Open Spotify and start playing a track. Then, from the project root, run:

```powershell
.\SpotifyPopup\bin\Debug\net10.0-windows10.0.19041.0\SpotifyPopup.exe
```

You can also open that executable from File Explorer. If you installed the SDK locally, use the same PowerShell session with the local SDK on `PATH`; a machine running the executable also needs the .NET 10 Desktop Runtime installed or available through `DOTNET_ROOT`.

## Close or kill

To close normally, unlock the popup if needed, right-click it, and choose **Chiudi**.

If the popup is unresponsive, open PowerShell and force-close only Spotify Popup:

```powershell
Stop-Process -Name SpotifyPopup -Force
```

This command does not close Spotify itself.

