# EasyNaive

EasyNaive is a Windows tray client for managing `sing-box` with `naive`
outbounds. It provides a local GUI for node management, routing mode switching,
proxy/TUN capture modes, subscriptions, diagnostics, and packaging.

## Status

This project is pre-release. The codebase is functional enough for local
testing, but the public open-source release still needs a final license choice
and a third-party distribution review.

## Features

- Windows tray application built with C# and .NET 8 WinForms.
- Multiple `naive` node management with manual and auto selection.
- Route modes: rule, global, and direct.
- Capture modes: system proxy and compatible TUN mode.
- sing-box config generation and `sing-box check` validation.
- Clash API based hot switching for route and selector state.
- Subscription import, text import, and clipboard import.
- App logs, sing-box logs, self-checks, traffic display, and tray status icons.
- Portable package and WiX MSI build scripts.

## Known Limits

- TUN mode with `naive` is a compatible TUN path, not a full native-UDP VPN.
- The project currently bundles `sing-box.exe` and `libcronet.dll` for local
  packaging. Their upstream licenses and redistribution requirements must be
  reviewed before public release.
- The project license is not final yet. See [LICENSE](LICENSE) and
  [OPEN_SOURCE_CHECKLIST.md](OPEN_SOURCE_CHECKLIST.md).

## Repository Layout

```text
src/
  EasyNaive.App/              WinForms tray app and application logic
  EasyNaive.Core/             Shared models and enums
  EasyNaive.SingBox/          Config generation, Clash API, process control
  EasyNaive.Platform.Windows/ Windows-specific integration
  EasyNaive.Elevation/        Elevated helper for TUN mode

tests/
  EasyNaive.App.Tests/
  EasyNaive.SingBox.Tests/

scripts/
  build-app.ps1
  package.ps1
  build-installer.ps1

packaging/wix/
  WiX installer project
```

Reference upstream source trees are intentionally excluded from Git under
`sources/`.

## Requirements

- Windows x64
- .NET SDK 8 for development
- Microsoft .NET Desktop Runtime 8 x64 for framework-dependent releases

## Build

```powershell
dotnet build EasyNaive.sln -c Debug
```

## Test

```powershell
dotnet test EasyNaive.sln -c Debug
```

## Build Portable Package

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

The portable package is written to:

```text
artifacts/package/Release/win-x64/
```

## Build MSI Installer

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

The MSI is written to:

```text
artifacts/installer/Release/win-x64/
```

## User Data

User data is stored under:

```text
%LocalAppData%\EasyNaive
```

Package upgrades and installer upgrades must preserve this directory.

## Security

Report security issues privately. See [SECURITY.md](SECURITY.md).

## License

The EasyNaive project license has not been finalized. Do not publish this
repository as an open-source release until [LICENSE](LICENSE) is replaced with a
final approved license and third-party notices are reviewed.
