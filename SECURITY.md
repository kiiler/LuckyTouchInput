# Security policy

## Supported version

Only the latest prerelease is supported while the project is in alpha.

## Reporting a vulnerability

Please open a private security advisory in the repository rather than a public issue.
Do not include passwords, clipboard contents, typed text, private hostnames, or screenshots
containing personal data.

## Security boundaries

- Lucky Touch Input runs at the current user's integrity level and does not request elevation.
- Windows `SendInput` cannot reliably send input to higher-integrity windows.
- The UAC secure desktop is intentionally unsupported.
- Native touch-keyboard suppression is optional and restores the previous per-user registry values.
- The application has no network client, telemetry, updater, or remote-control transport.
