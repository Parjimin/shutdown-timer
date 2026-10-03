# Shutdown Timer

A small Windows shutdown scheduler with a clean 16:9 WPF interface, countdown, quick presets, and optional looping video background.

> Built for people who want a faster alternative to repeatedly typing `shutdown -s -t ...` in Command Prompt.

## Features

- Schedule Windows shutdown in hours, minutes, and seconds
- Quick presets: 5, 10, 30, 45 minutes, 1 hour, and 2 hours
- Live countdown and scheduled shutdown time
- Cancel a pending shutdown at any time
- 16:9 borderless WPF interface
- Optional looping MP4 background
- Hidden hover control to pause/resume the background video
- Crash log written to `%LOCALAPPDATA%\ShutdownTimer\crash.log`
- Portable and Lite Windows x64 builds

## Download

Open the **Releases** section of this repository and download one of these packages:

- **ShutdownTimer-Portable-win-x64.zip** — recommended; includes the .NET runtime, no extra installation required.
- **ShutdownTimer-Lite-win-x64.zip** — much smaller; requires the .NET 8 Desktop Runtime.

Extract the ZIP, then run `ShutdownTimer.exe`.

Windows SmartScreen may warn about unsigned community-built executables. You can inspect the source and GitHub Actions workflow in this repository before running the app.

## Optional video background

The public release does **not** bundle copyrighted media.

To use your own background:

1. Put an MP4 file next to `ShutdownTimer.exe`.
2. Rename it to `background.mp4`.
3. Start the app.

Recommended video settings:

- 16:9
- 1280×720
- H.264 MP4
- 24–30 FPS
- no audio needed
- short seamless loop recommended

If `background.mp4` is missing or cannot be played, the app falls back to a dark background.

## How it works

Shutdown Timer uses Windows' built-in commands:

```text
shutdown.exe /s /t <seconds>
shutdown.exe /a
```

It does not force-close applications with `/f`.

## Build from source

Requirements:

- Windows
- .NET 8 SDK

Clone the repository:

```powershell
git clone https://github.com/Parjimin/shutdown-timer.git
cd shutdown-timer
```

Build a small framework-dependent version:

```powershell
dotnet publish .\ShutdownTimer.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Build a portable self-contained version:

```powershell
dotnet publish .\ShutdownTimer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

## Safety notes

Before scheduling a shutdown, save any important work. Windows and running applications determine how unsaved work is handled during shutdown.

## License

Licensed under the [MIT License](LICENSE).
