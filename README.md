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
dotnet publish -c Release -r win-x64 -f net10.0 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

### Linux x64

```bash
dotnet publish -c Release -r linux-x64 -f net10.0 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Published files are located in:

- `bin/Release/net10.0/win-x64/publish/`
- `bin/Release/net10.0/linux-x64/publish/`

## License

See [LICENSE](LICENSE) for license information.
