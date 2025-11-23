# OpenClicker

OpenClicker is a lightweight cross-platform auto clicker built with **.NET 10**, **Photino.NET**, and **SharpHook**. It provides global hotkeys, adjustable CPS, duty cycle control, and selectable mouse buttons.

## Features

- Global toggle hotkey (keyboard or mouse)
- Adjustable CPS (clicks per second)
- Adjustable duty cycle (0–100%)
- Selectable mouse button (Left, Right, Middle, XButtons)
- Photino-based UI (`wwwroot/`) with C# backend

## Requirements

- .NET 10 SDK
- Photino.NET runtime prerequisites:
  - Windows: WebView2 runtime or Microsoft Edge Dev/Canary
  - Linux: GTK 3 and WebKit2GTK
  - macOS: Xcode / WebKit
- The native Photino binaries are normally provided by the Photino.NET NuGet package. Building `Photino.Native` yourself is optional and only needed for custom native builds.

## Build

### General .NET build

```bash
dotnet build -c Release
```

### Publish (self-contained, single file)

#### Windows (x64, single-file `.exe`)

```bash
dotnet publish -c Release -r win-x64 -f net10.0 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

#### Linux (x64, single-file)

```bash
dotnet publish -c Release -r linux-x64 -f net10.0 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The published binaries will be located under:

- `bin/Release/net10.0/win-x64/publish/`
- `bin/Release/net10.0/linux-x64/publish/`

### Photino.Native build notes (optional)

If you need to build `Photino.Native` from source, refer to the upstream project. The key points are:

#### Windows

- Open the `Photino.Native` solution in Visual Studio 2019 or later.
- Install the “Desktop development with C++” workload (Linux C++ workload recommended but optional).
- Build in **x64** configuration (not AnyCPU).
- Install either the WebView2 runtime or the Edge Dev Channel; the project will not work correctly in a dev environment with only plain Edge installed.

#### Linux

Install dependencies:

```bash
sudo apt-get update && sudo apt-get install libgtk-3-dev libwebkit2gtk-4.0-dev
```

Compile:

```bash
gcc -std=c++11 -shared -DOS_LINUX Exports.cpp Photino.Linux.cpp -o x64/$(buildConfiguration)/Photino.Native.so 'pkg-config --cflags --libs gtk+-3.0 webkit2gtk-4.0' -fPIC
```

#### macOS

Compile:

```bash
gcc -shared -lstdc++ -DOS_MAC -framework Cocoa -framework WebKit Photino.Mac.mm Exports.cpp Photino.Mac.AppDelegate.mm Photino.Mac.UiDelegate.mm Photino.Mac.UrlSchemeHandler.mm -o x64/$(buildConfiguration)/Photino.Native.dylib
```

For more details on `Photino.Native`, see the official documentation:  
https://docs.tryphotino.io/Photino-Native

## Third-party libraries

OpenClicker uses the following libraries:

- **Photino.NET** – Apache License 2.0
- **SharpHook** – MIT License

See `LICENSE` file for full license information.
