# EasyNaive Release Guide

## Current Version

- Product version: `0.1.0-local`
- Target platform: `Windows x64`
- Runtime requirement: `Microsoft .NET Desktop Runtime 8 (x64)`

## Release Artifacts

Default output locations after running the packaging scripts:

- MSI installer: `artifacts/installer/Release/win-x64/EasyNaive-<version>-x64.msi`
- Portable zip package: `artifacts/package/Release/win-x64/EasyNaive-<version>-win-x64-<timestamp>.zip`
- Staged install layout: `artifacts/package/Release/win-x64/EasyNaive-<version>-<timestamp>/`

## Build Commands

### Build portable package

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

### Build MSI installer

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

### Override version

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Version 0.1.1
```

## Release Notes Template

Use the following template when publishing a build:

```markdown
# EasyNaive <version>

## Highlights

- WinForms tray client with multi-node management
- Proxy mode and compatible TUN mode
- sing-box based routing, Clash API hot switching, and subscription import
- MSI installer with start menu, desktop shortcut, and launch-after-install

## Requirements

- Windows x64
- Microsoft .NET Desktop Runtime 8 (x64)

## Artifacts

- EasyNaive-<version>-x64.msi
- EasyNaive-<version>-win-x64-<timestamp>.zip

## Known Limits

- `naive` under TUN is a compatible TUN path, not a full native-UDP VPN
- Full automatic test coverage is not complete yet
```
