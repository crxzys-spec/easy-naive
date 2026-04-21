# Contributing

EasyNaive is licensed under GNU GPL v3.0 or later. Public contribution is still
limited until the third-party binary distribution policy is finalized.

## Development Setup

Requirements:

- Windows x64
- .NET SDK 8
- PowerShell

Build:

```powershell
dotnet build EasyNaive.sln -c Debug
```

Test:

```powershell
dotnet test EasyNaive.sln -c Debug
```

Package:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

## Contribution Rules

- Do not commit reference upstream source trees under `sources/`.
- Do not commit build outputs under `artifacts/`.
- Do not commit user data from `%LocalAppData%\EasyNaive`.
- Do not commit real node credentials, subscription URLs, API secrets, or logs.
- Keep changes focused and include tests for behavior changes where practical.
- Run `dotnet test EasyNaive.sln -c Debug` before submitting changes.

## Coding Notes

- Keep platform-specific Windows integration under `EasyNaive.Platform.Windows`
  where practical.
- Keep sing-box config generation in `EasyNaive.SingBox`.
- Use the existing JSON stores and secret transforms for persisted sensitive
  fields.
- Avoid long-running elevated operations in the main tray process.

## Commit Messages

Use concise imperative commit messages, for example:

```text
Add proxy restore snapshot
Fix TUN helper shutdown status
Update portable package validation
```
