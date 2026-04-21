# EasyNaive v0.1.0 Release Plan

## Release Type

Recommended first release type: `v0.1.0-preview.1`.

Reason: the app is already locally usable, but the repository is not ready for a
final public release because bundled third-party binary redistribution still
needs a final review.

## Release Goal

Ship a first preview build that users can install and test on Windows x64.

Primary goals:

- Validate proxy mode in real user environments.
- Validate compatible TUN mode and elevation helper behavior.
- Validate subscription import and node switching.
- Validate portable zip and MSI packaging.
- Collect feedback before locking the public license and long-term distribution
  model.

## Included Scope

- WinForms tray client.
- Node CRUD and manual node selection.
- Auto node mode based on sing-box `urltest`.
- Route modes: rule, global, direct.
- Capture modes: proxy and compatible TUN.
- Subscription import, text import, clipboard import.
- Runtime status, current node display, traffic display, and tray state icons.
- Self-check and logs directory access.
- Startup recovery and single-instance guard.
- Portable zip package.
- WiX MSI package.

## Known Limits

- `naive` under TUN is compatible TUN, not a full native-UDP VPN.
- Windows system proxy restoration uses a persisted snapshot, but still needs
  manual validation with other proxy clients installed.
- The current release bundles `sing-box.exe` and `libcronet.dll`; third-party
  notices and redistribution obligations must be reviewed before a public
  final release.

## Release Blockers

These must be resolved before publishing a public GitHub Release:

- [ ] Confirm whether bundling `sing-box.exe` is acceptable for the selected
      distribution model.
- [ ] Finalize `THIRD_PARTY_NOTICES.md` for `sing-box`, `libcronet.dll`, and
      direct NuGet dependencies.
- [ ] Run the manual smoke test checklist on a clean Windows machine.
- [ ] Validate system proxy snapshot/restore with an existing third-party proxy
      configuration.

## Build Commands

Run tests:

```powershell
dotnet test EasyNaive.sln -c Debug
```

Build portable zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.0-preview.1
```

Build MSI:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.0-preview.1
```

## Expected Artifacts

```text
artifacts/package/Release/win-x64/EasyNaive-0.1.0-preview.1-win-x64-<timestamp>.zip
artifacts/installer/Release/win-x64/EasyNaive-0.1.0-x64.msi
```

Note: WiX MSI package versions use three numeric parts, so the MSI filename uses
`0.1.0` even when the product build version is `0.1.0-preview.1`.

## Manual Smoke Test

- [ ] Fresh app launch shows tray icon and main window.
- [ ] Add or import one naive node.
- [ ] Proxy mode connects successfully.
- [ ] Windows system proxy points to the configured mixed port while connected.
- [ ] Existing Windows system proxy settings are restored after disconnect.
- [ ] Rule/global/direct route switching works without manual restart.
- [ ] Manual node selection updates current node display.
- [ ] Auto mode displays the real selected outbound node.
- [ ] Traffic counters update while browsing.
- [ ] Disconnect stops sing-box and clears/restores proxy state.
- [ ] TUN mode starts through the elevation helper.
- [ ] TUN mode can access external sites.
- [ ] Self Check reports expected pass/skip/fail items.
- [ ] Right-click tray exit closes without freezing.
- [ ] Portable zip runs from an extracted folder.
- [ ] MSI installs and launches the app.
- [ ] MSI uninstall removes program files and preserves `%LocalAppData%\EasyNaive`.

## Git Release Steps

After blockers are resolved:

```powershell
git status --short
git tag -a v0.1.0-preview.1 -m "EasyNaive v0.1.0-preview.1"
git push origin main
git push origin v0.1.0-preview.1
```

Then create a GitHub Release for `v0.1.0-preview.1` and attach:

- Portable zip.
- MSI installer.
- Release notes copied from this plan and `docs/release.md`.

## Release Notes Draft

### Highlights

- First EasyNaive preview build for Windows x64.
- sing-box powered proxy client with naive outbound support.
- Tray-based operation with proxy and compatible TUN capture modes.
- Multi-node management, subscriptions, routing modes, diagnostics, and
  packaged zip/MSI artifacts.

### Upgrade Notes

- User data is stored under `%LocalAppData%\EasyNaive`.
- Package and installer upgrades must preserve this directory.

### Known Issues

- Compatible TUN mode does not provide full native UDP VPN behavior for
  `naive`.
- Bundled binary redistribution policy is still pending final approval.
