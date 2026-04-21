# EasyNaive Smoke Test Checklist

Use this checklist before publishing a preview release.

Recommended target:

- Windows x64
- Clean or disposable test machine when possible
- Microsoft .NET Desktop Runtime 8 x64 installed

Record the tested build:

```text
Version:
Package:
Windows version:
Tester:
Date:
```

## 1. Portable Package

- [ ] Extract the portable zip to a new folder.
- [ ] Confirm the folder contains `EasyNaiveTray.exe`.
- [ ] Confirm the folder contains `EasyNaive.Elevation.exe`.
- [ ] Confirm the folder contains `sing-box\sing-box.exe`.
- [ ] Confirm the folder contains `sing-box\libcronet.dll`.
- [ ] Confirm the folder contains `docs\LICENSE`.
- [ ] Confirm the folder contains `docs\THIRD_PARTY_NOTICES.md`.
- [ ] Launch `EasyNaiveTray.exe`.
- [ ] Main window opens.
- [ ] Tray icon appears.
- [ ] Closing the main window hides it to tray instead of exiting.
- [ ] Double-clicking the tray icon reopens the main window.

## 2. Basic Node Setup

- [ ] Add a manual naive node or import one from text.
- [ ] Node appears in the node list.
- [ ] Select the node as manual node.
- [ ] Click `Refresh Preview`.
- [ ] Generated sing-box config contains the node outbound.
- [ ] Run `Self Check`.
- [ ] `sing-box executable` passes.
- [ ] `libcronet` passes.
- [ ] `Elevation helper` passes.
- [ ] `sing-box check` passes.

## 3. Proxy Mode

- [ ] Set Capture Mode to `Proxy`.
- [ ] Set Route Mode to `Rule`.
- [ ] Click `Connect`.
- [ ] Status becomes `Connected`.
- [ ] Tray icon changes to connected icon.
- [ ] Windows system proxy points to `127.0.0.1:<mixed port>`.
- [ ] Browser can access an external site.
- [ ] Traffic counters update.
- [ ] Active connections count updates.
- [ ] Click `Disconnect`.
- [ ] Status becomes `Disconnected`.
- [ ] Tray icon changes to stopped icon.
- [ ] `sing-box.exe` exits.

## 4. System Proxy Snapshot And Restore

This test verifies EasyNaive does not overwrite another proxy client's settings.

- [ ] Disconnect EasyNaive.
- [ ] Manually set Windows system proxy to a non-EasyNaive value, for example `127.0.0.1:8888`.
- [ ] Start EasyNaive in `Proxy` mode.
- [ ] Confirm Windows system proxy changes to EasyNaive mixed port while connected.
- [ ] Disconnect EasyNaive.
- [ ] Confirm Windows system proxy is restored to the previous non-EasyNaive value.
- [ ] Repeat the test by exiting EasyNaive from the tray while connected.
- [ ] Confirm Windows system proxy is restored after exit.

## 5. Route Mode Hot Switching

Run while connected in `Proxy` mode.

- [ ] Switch Route Mode to `Rule`.
- [ ] Status returns to `Connected` without manual reconnect.
- [ ] Switch Route Mode to `Global`.
- [ ] Status returns to `Connected` without manual reconnect.
- [ ] Switch Route Mode to `Direct`.
- [ ] Status returns to `Connected` without manual reconnect.
- [ ] Existing active connections are refreshed after each mode switch.

## 6. Node Mode And Selection

Run while connected with at least two enabled nodes when possible.

- [ ] Set Node Mode to `Manual`.
- [ ] Select a specific node.
- [ ] Header/status detail shows the selected node.
- [ ] Run `Test Selected`.
- [ ] Latency is shown or a clear error is displayed.
- [ ] Set Node Mode to `Auto`.
- [ ] Status detail shows `Auto -> <real node>` after Clash API state refresh.
- [ ] Run `Test All`.
- [ ] Per-node delay results are shown in the grid.

## 7. Subscription Import

- [ ] Add a subscription profile.
- [ ] Refresh subscriptions.
- [ ] Imported nodes appear in the node list.
- [ ] Subscription errors are shown clearly if refresh fails.
- [ ] Disable a subscription.
- [ ] Confirm disabled subscription does not unexpectedly overwrite manually added nodes.

## 8. TUN Mode

Use a test machine or VM if possible.

- [ ] Set Capture Mode to `Tun`.
- [ ] Click `Connect`.
- [ ] UAC prompt appears for `EasyNaive.Elevation.exe`.
- [ ] Approve UAC.
- [ ] Status becomes `Connected`.
- [ ] Header/status detail shows TUN helper session information.
- [ ] Browser can access an external site.
- [ ] Rule Mode works in TUN mode.
- [ ] Global Mode works in TUN mode.
- [ ] Direct Mode works in TUN mode.
- [ ] Click `Disconnect`.
- [ ] TUN helper session is marked stopped.
- [ ] `sing-box.exe` exits.
- [ ] Network connectivity returns to normal after disconnect.

## 9. Logs And Diagnostics

- [ ] `Open Logs` opens the logs directory.
- [ ] `app.log` exists.
- [ ] `sing-box.log` exists after connect.
- [ ] Self Check dialog opens from main window.
- [ ] Self Check dialog opens from tray menu.
- [ ] Self Check shows clear failures when a required file is temporarily missing.

## 10. Tray Exit

- [ ] Connect in Proxy mode.
- [ ] Right-click tray icon.
- [ ] Click `Exit`.
- [ ] App exits without freezing.
- [ ] `EasyNaiveTray.exe` exits.
- [ ] `sing-box.exe` exits.
- [ ] Windows system proxy is restored or cleared safely.
- [ ] Reopen EasyNaive.
- [ ] App starts normally after previous tray exit.

## 11. MSI Installer

- [ ] Run the MSI installer.
- [ ] Installer blocks or warns clearly if .NET Desktop Runtime 8 is missing.
- [ ] Install succeeds.
- [ ] Start Menu shortcut is created.
- [ ] Desktop shortcut is created.
- [ ] Launch from Start Menu works.
- [ ] Installed app can connect in Proxy mode.
- [ ] Installed app can run Self Check.
- [ ] Uninstall succeeds.
- [ ] Program files are removed.
- [ ] `%LocalAppData%\EasyNaive` is preserved.

## 12. Upgrade Path

If testing over an existing install:

- [ ] Existing `settings.json` is preserved.
- [ ] Existing `nodes.json` is preserved.
- [ ] Existing `subscriptions.json` is preserved.
- [ ] Existing DPAPI-protected secrets still load for the same Windows user.
- [ ] App can connect after upgrade without re-entering node credentials.

## Result

```text
Passed:
Failed:
Skipped:
Notes:
```

Do not publish a release if any failure affects connect/disconnect, proxy
restore, TUN cleanup, or package install/uninstall behavior.
