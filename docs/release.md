# EasyNaive Release Guide

## Current Version

- Product version: `0.1.1-preview.2`
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

## Installer Upgrade Behavior

The MSI uses a stable `UpgradeCode` and enables same-version major upgrades. This is intentional for preview builds because semantic versions such as `0.1.0-preview.1` and `0.1.0-preview.2` are both converted to MSI product version `0.1.0`.

ICE61 is suppressed for the installer project because same-version preview upgrades intentionally remove the installed product with the same MSI product version.

User data is stored outside the install directory under `%LocalAppData%\EasyNaive` and must be preserved across uninstall and upgrade.

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
