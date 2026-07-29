# OpenClicker

OpenClicker is a lightweight auto clicker for Windows and Linux, built with **.NET 10**, **Photino.NET**, and **SharpHook**.

## Features

- Adjustable clicks per second and duty cycle
- Application filtering to restrict where clicks are sent
- Toggle and hold-to-activate modes
- Global keyboard or mouse hotkeys
- Left, middle, and right mouse buttons
- English and German interface

## Requirements

- Windows: WebView2 Runtime
- Linux: X11, GTK 3, and WebKit2GTK

## Build

```bash
dotnet build -c Release
```

## Publish

### Windows x64

```bash
dotnet publish OpenClicker.csproj -c Release -f net10.0 -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=embedded
```

### Linux x64

```bash
dotnet publish OpenClicker.csproj -c Release -f net10.0 -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=embedded
```

Published files are located in:

- `bin/Release/net10.0/win-x64/publish/`
- `bin/Release/net10.0/linux-x64/publish/`

Each publish directory contains one executable. The bundled native libraries and web assets are extracted automatically when the application starts. On Windows, extraction uses `%TEMP%/.net`; on Linux, it uses `$HOME/.net`.

User settings are created as `settings.json` next to the executable after the first setting is changed. The executable directory must therefore be writable. The settings file is intentionally not bundled because it is modified at runtime.

## License

See [LICENSE](LICENSE) for license information.
