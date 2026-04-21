# EasyNaive WiX Packaging Notes

This directory is reserved for the installer project.

Current status:

- `scripts/package.ps1` produces a staged application layout and zip archive.
- `scripts/build-installer.ps1` builds a WiX MSI from the latest staged layout.
- Upgrade and uninstall behavior must preserve `%LocalAppData%\EasyNaive`.

Planned next steps:

1. Add Start Menu shortcuts and richer installer UI if needed.
2. Register optional autostart defaults and runtime prerequisites.
3. Keep user data under `%LocalAppData%\EasyNaive` during upgrades and uninstall.
