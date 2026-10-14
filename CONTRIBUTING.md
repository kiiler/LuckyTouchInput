# Contributing

Keep changes small, Windows-focused, and testable on real touch or remote-control surfaces.

1. Build with the .NET 10 SDK on Windows 11.
2. Run `dotnet build LuckyTouchInput.sln -c Release`.
3. Run `dotnet run --project tests/LuckyTouchInput.Tests -c Release`.
4. In a disposable interactive Windows account/VM without a keyboard profile, run
   `pwsh -File tests/Invoke-UiAcceptance.ps1`. Never use your daily work account for the full suite.
5. For UI changes, report the monitor resolution, working area, DPI, mode, and measured window rectangles.

The fast tests also instantiate WinForms controls with runtime hooks, tray creation, and target-window
management disabled. They check the actual mobile key rectangles and leave the current settings and
native-keyboard lease unchanged. Add `-- --render-directory artifacts/layout-checks` to render these
test controls. These images are control-layout renders, not screenshots of a live remote session.

`tests/LuckyTouchInput.InputProbe` is a disposable local integration target. It stores no text and
closes without a save prompt, so UI Automation can verify input and window restoration safely.

The acceptance script also accepts `-AppPath`, `-ProbePath`, `-ResultPath`, and `-ScreenshotPath`
for self-contained builds in an offline VM. Add `-CheckOsk` to verify handoff to the actual Windows
accessibility keyboard; it refuses a pre-existing OSK and requests closure only of its own fixture.
Windows may deny a normal-integrity script's close message to UIAccess OSK. In that case the suite
waits 60 seconds for a person to close OSK, or for a disposable lab controller using the optional
`-OskCloseRequestPath` JSON file. A controller must verify the PID, start time, session, and system
executable path before closing that exact fixture. Never elevate Lucky or the input probe to make
the test pass, disable UIPI, or use this option on a desktop relying on OSK for accessibility.
Virtual work-area tests size the actual
layout controls independently of the runner's screen limit; only the interactive suite establishes
native window geometry. `scripts/Publish.ps1` checks the SDK before replacing
an old package and validates every local documentation link inside the ZIP. The icon is an original
vector in `assets/icon.svg`; regenerate the multi-size ICO with `scripts/Build-Icon.ps1` on Windows.

Do not add telemetry, cloud services, arbitrary command execution, bundled input-method dictionaries,
or silent registry changes. A new dependency needs a concrete user-facing reason and license review.

Typed text, clipboard contents, private paths, and machine-specific settings must never be committed.

## Compact companion

See [compact development](docs/COMPACT-DEVELOPMENT.md) for its separate project, fast checks,
opt-in input-injection harness, packaging and preliminary phone acceptance scope.
