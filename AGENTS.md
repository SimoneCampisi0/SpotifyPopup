# Agent instructions

## Project overview

Spotify Popup is a Windows desktop application written in C# using WPF and .NET 10. It presents an always-on-top, resizable mini player for the Spotify desktop application. The player does not play audio itself: it reads and controls Spotify through Windows media sessions.

The project targets `net10.0-windows10.0.19041.0`. The supported distribution is a self-contained, single-file executable for 64-bit Windows. The application currently does not use Spotify's Web API or require Spotify API credentials.

## Workspace and dependencies

- Work only inside this project folder. Do not modify Spotify, its installation or data, or files outside the project.
- Keep downloaded dependencies, NuGet packages, CLI data, temporary files, and build output within the project. Use the paths configured by `publish.ps1`; do not install packages or tools globally.
- If an important dependency or SDK is missing, stop and inform the user. Download it into the project only when the user has authorized it.
- The published single-file executable extracts native .NET runtime files to `%TEMP%\.net` by default at runtime. When testing, set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` to a location inside this project before starting it. Do not claim that the executable never extracts files to disk.
- Preserve user-created files and uncommitted changes. Do not delete source, assets, caches, or output unless the task calls for it; check paths before cleanup.

## Project layout

- `core/MainWindow.xaml`: popup layout, control styling and window configuration.
- `core/MainWindow.xaml.cs`: Windows media-session integration, player actions, window behavior and lock/unlock logic.
- `core/App.xaml` and `App.xaml.cs`: WPF application startup and shared resources.
- `core/SpotifyPopup.csproj`: target framework, WPF settings and icon resources.
- `assets/logo.ico`: icon embedded in the executable and used by the window. Preserve it unless the user requests an icon change.
- `publish.ps1`: publishes the self-contained executable using project-local dependency and temporary directories.
- `README.md`: installation, publishing, execution and shutdown instructions. Update it when those workflows or user-visible behavior change.

## Implementation guidance

- Use `Windows.Media.Control` and the Spotify media session exposed by Windows. Metadata and control availability depend on what Spotify actually exposes; handle missing sessions, unsupported controls and absent metadata gracefully.
- Do not claim the source playlist is available through the current integration. Introduce Spotify Web API access, authentication or network dependencies only when required by the task.
- Preserve the always-on-top, draggable and resizable window; its playback controls; the full-title popover when unlocked; and the close button.
- In locked mode, preserve transparency and click-through behavior while keeping a clear, accessible way to unlock. Make sure changes to window position or size do not misalign the unlock control.
- Maintain readable contrast, visible focus and hover feedback, accessible control names, and a usable layout at the minimum window size. Match the existing glass-inspired design without claiming to implement Apple's native Liquid Glass material.
- Keep UI changes in XAML and behavior in C# where practical. Follow existing patterns and avoid adding dependencies for routine changes.

## Build and verification

From the project root, publish with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
```

The expected deliverable is `core\bin\publish\win-x64\SpotifyPopup.exe`. It should remain a single self-contained executable; .NET is needed to build it, but a separate .NET installation is not needed to run it. Publishing may require network access for missing runtime packs.

Run checks appropriate to the change. For user-interface changes, build and, when possible, inspect the actual popup with Spotify open: check layout, title popover, resize, close, lock and unlock. Check playback actions only when relevant and restore any playback state changed during a test. Verify the executable and window icon when modifying the logo or publishing configuration. Avoid changing the user's Spotify files or settings.

Report what was tested and any behavior that could not be verified. If the application is running and blocks a build, avoid terminating unrelated processes and explain any remaining limitation.

