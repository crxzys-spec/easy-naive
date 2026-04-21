# Security Policy

## Supported Versions

EasyNaive is currently pre-release. Security fixes are handled on the main
development line until a versioned release policy is created.

## Reporting a Vulnerability

Do not open a public issue for suspected vulnerabilities.

Report privately to the project maintainer using the currently configured
private contact channel. If this repository is published publicly, replace this
section with a real security contact email or private advisory process.

Please include:

- Affected version or commit.
- Operating system version.
- Capture mode: proxy or TUN.
- Steps to reproduce.
- Impact assessment.
- Any relevant logs with secrets removed.

## Sensitive Data

Never include the following in public issues, logs, screenshots, or commits:

- Node passwords.
- Subscription URLs.
- Clash API secrets.
- `%LocalAppData%\EasyNaive` state files.
- Full generated configs containing credentials.

## Security-Sensitive Areas

- Windows system proxy registry changes.
- TUN elevation helper and UAC flow.
- sing-box process lifecycle.
- DPAPI protected persisted secrets.
- Subscription parsing and import.
- Package and installer contents.
