# Changelog

## [0.1.0-alpha.1] - 2026-09-08

- Add an opt-in phone large-key preset: 48% height, larger utility labels, smaller margins,
  and a compact toolbar. Height remains adjustable; the preset does not change Windows DPI/resolution.
- Widen Enter and put it at the far right of a five-button bottom row. Candidate paging, scrolling,
  and cursor arrows now live in the Tools panel; variables and prompts remain accessible there.
- Exclude native input surfaces and accessibility OSK from foreground window-avoidance targets.
- Add real WinForms control-layout tests across 1280, 3840, foldable, and portrait work areas.
- Package filenames default to the version in the application project.
- Add an Everyday preset with desktop row staggering, Tab, one-shot Ctrl, cursor arrows, and
  a Tools → Edit panel. Existing layout settings migrate; selecting a preset resets its height.
- Keep Shift+Tab, Shift+arrow, and Ctrl/Shift+Enter usable; only bare Enter triggers optional collapse.
- Use measured GDI text dimensions so portrait Backspace/Enter labels stay on one line.
- Release native suppression on collapse, target loss, secure-surface detection, and exit. Yield to
  visible accessibility OSK without terminating it, and restore the target while yielding.
- Avoid repeated failed takeover; confirm modern touch panes by geometry rather than process alone.
- Persist suppression recovery before registry writes, preserve detected external preference changes,
  recover alpha leases, and retain incomplete leases instead of replacing the recovery baseline.
- Split keyboard ownership policy and registry leases out of the UI/settings code. Dispose replaced
  font/region resources, and avoid touching disposed buttons after a layout switch.

Validation: 350 settings/control-layout/ownership/isolated-registry checks and 59 interactive checks
pass on offline Windows 11 build 26100. The app and probe remain unelevated; the lab controller
closes only its verified OSK fixtures when Windows denies the harness's close message. Native touch
popup timing, physical touch, Chinese composition, and UAC remain outside this VM acceptance.

## [0.1.0-alpha] - 2026-09-05

First public prerelease for Windows 11 x64. Chinese UI, MIT license, self-contained portable ZIP.

### Features

- No-activation QWERTY touch keyboard with numbers, Shift, Backspace, Enter, candidate paging,
  and focused-control scrolling.
- Follow foreground, pinned application, and floating modes, with per-mode height and a small
  visible seam above the keyboard. Taskbar space is left available.
- Safe collapse/restore, tray recovery, repeated-EXE-launch recovery, and optional login startup.
- Session-only snippets, literal multiline insertion, and visible prompt shortcuts.
- Optional Microsoft Pinyin full/double-pinyin switch that preserves existing custom mappings.
- Opt-in native touch-keyboard suppression with a recoverable per-user settings lease.
- One-time import of prototype settings without modifying the original.
- English/Chinese documentation, original vector/app icon, bug-report form, Windows CI,
  runtime license notices, and SHA-256 package checks.

### Fixes before the public release

- Pinned mode no longer rejects a target after shrinking it below 400 px.
- Intentional minimization is respected; target window geometry is restored on normal exit.
- Window avoidance uses DWM's visible frame, not the invisible resize border.
- Key labels fit the real button height, including the bottom row on small displays.
- Settings notifications no longer block the UI while waiting on unrelated windows.
- Native-keyboard suppression hides a confirmed pane instead of using a toggle that can reopen it.
- Coexistence does not overwrite per-user settings changed while the keyboard is running.
- Input stops if the intended target cannot be brought to the foreground.
- Text snippets no longer synthesize Enter or Tab key presses.
- Settings writes are atomic; invalid modes fall back safely.
- Publishing validates the SDK before replacing a package and checks packaged documentation links.

### Validation

- Release build: 0 warnings, 0 errors; 58 settings/layout assertions.
- Offline Windows 11 Enterprise LTSC 2024, build 26100, 1280×800 display / 1280×752 working area:
  actual committed text, focus retention, settings/modes, scrolling, candidate key delivery,
  Enter policies, single-instance recovery, visible 3 px seam, minimization, and crash-lease recovery.
- See [validation scope](docs/VALIDATION.md) for the measured results and explicit gaps.

### Known limits

- Unsigned alpha; Windows 11 x64 and Chinese UI are the initial release surface.
- Real touch hardware, all DPI combinations, and every remote client are not certified.
- Candidate composition belongs to the active IME; delivery of a page key is not an IME compatibility test.
- Native touch-keyboard suppression remains experimental. The separate accessibility keyboard
  (osk.exe), elevated windows, and the UAC secure desktop are not controlled.
- After a forced process kill, native settings recover on the next launch; the previously resized
  application's geometry may need manual restoration.
