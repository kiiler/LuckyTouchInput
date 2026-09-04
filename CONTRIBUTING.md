# Contributing

Keep changes small, Windows-focused, and testable on real touch or remote-control surfaces.

1. Build with the .NET 10 SDK on Windows 11.
2. Run `dotnet build LuckyTouchInput.sln -c Release`.
3. Run `dotnet run --project tests/LuckyTouchInput.Tests -c Release`.
4. On an interactive desktop, run `pwsh -File tests/Invoke-UiAcceptance.ps1`.
5. For UI changes, report the monitor resolution, working area, DPI, mode, and measured window rectangles.

`tests/LuckyTouchInput.InputProbe` is a disposable local integration target. It stores no text and
closes without a save prompt, so UI Automation can verify input and window restoration safely.

Do not add telemetry, cloud services, arbitrary command execution, bundled input-method dictionaries,
or silent registry changes. A new dependency needs a concrete user-facing reason and license review.

Typed text, clipboard contents, private paths, and machine-specific settings must never be committed.
