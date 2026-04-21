# Open Source Readiness Checklist

Use this checklist before making the repository public or accepting external
contributions.

## Required Before Public Release

- [x] Choose the final project license and replace `LICENSE`.
- [x] Confirm the chosen license is compatible with the GPL-based distribution
      strategy.
- [ ] Review `sing-box` GPL obligations for bundled binary distribution.
- [ ] Confirm required notices for `libcronet.dll`.
- [ ] Decide whether bundled third-party binaries remain in Git.
- [ ] Generate or review NuGet dependency license notices.
- [ ] Remove private test data, logs, local configs, and credentials.
- [ ] Confirm `sources/` stays ignored and untracked.
- [ ] Confirm `artifacts/` stays ignored and untracked.
- [ ] Confirm package scripts do not include ignored reference source trees.
- [ ] Add a real security contact to `SECURITY.md`.
- [ ] Review README claims against the current product state.

## Recommended Before First Tagged Release

- [ ] Add release notes for the first version.
- [ ] Add screenshots or GIFs after UI stabilizes.
- [ ] Add a manual smoke-test checklist for proxy and TUN modes.
- [ ] Add installer upgrade and uninstall validation notes.
- [ ] Add a dependency update process for `sing-box` and `libcronet.dll`.
- [ ] Add issue templates for bug reports and feature requests.
- [ ] Add pull request template.
- [ ] Decide whether CI should build Debug, test, package, and publish artifacts.

## Current Deliberate Exclusions

The following local paths are intentionally excluded from Git:

```text
sources/
artifacts/
.dotnet/
bin/naiveproxy/
```

The root `bin/sing-box/` keeps only the runtime files required by the package
scripts:

```text
bin/sing-box/sing-box.exe
bin/sing-box/libcronet.dll
bin/sing-box/LICENSE
```
