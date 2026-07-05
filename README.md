# Quick Window Screenshot

Quick Window Screenshot is a Windows 10/11 desktop tool for capturing the content area of a selected window. It is designed for cases where the target may be a game, video player, terminal, or other hardware-accelerated window, and where the saved image should be the window content rather than the title bar, borders, or surrounding desktop.

The application is written in C#/.NET with a WinUI 3 / Windows App SDK shell and a separate reusable core library. The core uses Win32/DWM geometry APIs plus multiple capture backends.

## Features

- Select a target top-level window from a compact GUI.
- Pick the current foreground window after a short delay.
- Capture manually with a button or global hotkey.
- Capture automatically every N seconds.
- Save PNG files to a configurable output directory.
- Capture the internal content rectangle instead of the whole decorated window.
- Use Windows Graphics Capture (WGC), DXGI Desktop Duplication, or GDI fallback.
- High-DPI aware layout and window geometry handling.

## Requirements

Runtime:

- Windows 10 version 1903 or later, or Windows 11.
- .NET runtime matching the target framework used by the build.
- Windows App SDK runtime, unless using the self-contained Windows App SDK publish output.

Development:

- .NET 10 SDK.
- Visual Studio with WinUI / Windows App SDK development support, or equivalent MSBuild components.
- Windows is required for building and running the WinUI/WGC application.

The app project is configured as an unpackaged WinUI 3 app with self-contained Windows App SDK assets. The target machine still needs the appropriate .NET runtime unless you publish .NET self-contained.

## Download Or Build

After a release build, the zip package is created at:

```text
artifacts/QuickWindowScreenshot-win-x64.zip
```

The current release command publishes to:

```text
artifacts/publish/QuickWindowScreenshot-win-x64
```

To run from source:

```bat
dotnet run --project src/QuickWindowScreenshot/QuickWindowScreenshot.csproj
```

or double-click:

```bat
run.bat
```

## Basic Use

1. Start `QuickWindowScreenshot.exe`.
2. Select a target window from the list, or use the foreground-window picker.
3. Choose the capture backend. The default is WGC.
4. Set the output directory, filename prefix, hotkey, and automatic interval if needed.
5. Click capture, press the configured hotkey, or start automatic capture.

Default settings:

```text
Output directory: %USERPROFILE%\Pictures\Quick Window Screenshot
Hotkey:           Ctrl+Alt+F12
Auto interval:    3 seconds
Filename prefix:  qws
Backend:          Windows Graphics Capture (WGC)
```

Settings are saved to:

```text
%APPDATA%\Quick Window Screenshot\settings.json
```

For compatibility, the app can still read old settings from:

```text
%APPDATA%\QuickScreenshot\settings.json
```

## Capture Backends

### Windows Graphics Capture (WGC)

WGC is the default and recommended backend. It captures the selected window through Windows Graphics Capture and is usually the best option for DirectX games, video playback, Windows Terminal, and other GPU-rendered content.

The application creates a `GraphicsCaptureItem` for the selected HWND, receives a Direct3D frame, copies it through a D3D11 staging texture, and writes the result as PNG.

### DXGI Desktop Duplication

DXGI captures the desktop output for the monitor containing the target content rectangle, then crops the target area. It can work well for visible desktop content, but the result depends on what is visible on screen.

Use DXGI when WGC is unavailable or unsuitable.

### GDI

GDI uses `Graphics.CopyFromScreen` and crops the visible desktop. It is the simplest fallback and is mainly useful for normal, visible desktop windows. It is not a reliable choice for modern GPU-rendered or occluded content.

## Window Geometry And Pixel Accuracy

The tool does not ask the user to hand-draw a rectangle for precise capture. The user selects a window; the application computes the content area from Win32/DWM APIs.

For normal windows, content geometry is based on:

```text
GetClientRect + MapWindowPoints
```

The app is DPI aware, so these coordinates are interpreted in physical screen pixels rather than virtualized coordinates.

Windows Terminal has special handling because its visible terminal canvas is hosted inside child windows. The app detects the XAML content host and excludes the drag/tab bar region.

For WGC, capture is requested from the selected window handle and cropped to the expected content size when needed. For DXGI and GDI, the app crops the visible desktop frame using the computed content rectangle.

## Known Limits

- Minimized windows cannot be captured.
- DXGI and GDI require the target content to be visible and unobstructed on the desktop.
- WGC can be blocked by Windows, protected content, DRM, or app-specific capture restrictions.
- A target window must be fully within a single monitor for the current capture flow.
- Some nonstandard windows may report unusual client geometry; the debug text in the UI shows `clientRect`, `contentRect`, HWND, class name, DPI awareness state, and related information to help diagnose those cases.
- If "bring to front before capture" is enabled and Windows refuses to foreground the target window, the app cancels the capture instead of saving an incorrect image.

## Development

Repository layout:

```text
src/QuickWindowScreenshot.Core/        Capture, settings, hotkeys, and Win32 window logic
src/QuickWindowScreenshot/             WinUI 3 desktop application shell
tests/QuickWindowScreenshot.Tests/     Lightweight test runner
tools/WgcSmoke/                        WGC smoke test tool
QuickWindowScreenshot.slnx             Solution file
```

Build everything:

```bat
dotnet build QuickWindowScreenshot.slnx
```

Run tests:

```bat
dotnet run --no-restore --project tests/QuickWindowScreenshot.Tests/QuickWindowScreenshot.Tests.csproj
```

or:

```bat
test.bat
```

Run the WGC smoke test:

```bat
dotnet run --project tools/WgcSmoke/WgcSmoke.csproj
```

The smoke test creates a 320x180 WinForms target window, captures it with WGC, and verifies that the saved PNG is 320x180.

## Release Build

Framework-dependent win-x64 publish:

```bat
dotnet publish src/QuickWindowScreenshot/QuickWindowScreenshot.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish/QuickWindowScreenshot-win-x64
```

Package the published files:

```powershell
Compress-Archive -Path artifacts/publish/QuickWindowScreenshot-win-x64/* -DestinationPath artifacts/QuickWindowScreenshot-win-x64.zip -Force
```

Generated build and release outputs are ignored by Git.

## Dependencies

Main runtime/UI/capture dependencies include:

- `Microsoft.WindowsAppSDK` for the WinUI 3 desktop shell and native title bar integration.
- `Microsoft.Windows.SDK.NET` / C#/WinRT runtime support for WGC.
- `ScreenCapture.NET.DX11` for DXGI Desktop Duplication.
- Vortice/SharpGen dependencies used by the Direct3D and DXGI paths.

These dependencies are included in the publish output as needed.
