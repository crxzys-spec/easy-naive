# Third-Party Notices

This document tracks third-party components that are used, bundled, or referenced
by EasyNaive. It is a release-readiness document, not legal advice.

Before a public release, verify every entry against the exact shipped versions
and replace this document with finalized notices if required.

## Bundled Runtime Components

### sing-box

- Files: `bin/sing-box/sing-box.exe`, `bin/sing-box/LICENSE`
- Role: proxy core and `naive` outbound provider.
- License file in this repository: `bin/sing-box/LICENSE`
- License summary from bundled file: GNU General Public License version 3 or
  later, plus an additional restriction that derivative work may not use the
  upstream application name or imply association without prior consent.
- Release note: distributing a package that includes `sing-box.exe` requires a
  GPL compliance review, including source offer obligations if applicable.

### libcronet.dll

- File: `bin/sing-box/libcronet.dll`
- Role: runtime dependency used by `sing-box` naive outbound support.
- License source: upstream Chromium/Cronet notices must be verified for the
  exact binary distributed.
- Release note: ensure the final package includes required Chromium/Cronet
  notices.

## Referenced But Not Tracked

### naiveproxy reference source

- Local path: `sources/naiveproxy/`
- Git status: intentionally ignored.
- Role: local reference only.
- Release note: reference source must not be committed unless its license and
  attribution requirements are intentionally handled.

### sing-box reference source

- Local path: `sources/sing-box/`
- Git status: intentionally ignored.
- Role: local reference only.
- Release note: reference source must not be committed unless its license and
  attribution requirements are intentionally handled.

## Development Dependencies

The project uses .NET SDK, NuGet packages, xUnit, coverlet, and WiX tooling for
build, test, and packaging. Their exact transitive licenses should be generated
or reviewed before a public release.

Known direct package references include:

- `System.Security.Cryptography.ProtectedData`
- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`
- `WixToolset.Sdk`
- `WixToolset.Netfx.wixext`
- `WixToolset.UI.wixext`

## Release Checklist For Notices

- Verify exact versions of bundled binaries.
- Include upstream license files in the release package where required.
- Generate a NuGet dependency license report.
- Decide whether `sing-box` is bundled or downloaded separately.
- Confirm the final EasyNaive license is compatible with the distribution model.
