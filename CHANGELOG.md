# Changelog

All notable changes to Lucky Touch Input are documented here.

## [0.1.0-alpha] - Unreleased

### Added

- No-activation touch keyboard for Windows.
- Follow-foreground, pinned-application, and floating modes.
- Adaptive phone, tablet, foldable, DPI, and multi-monitor geometry.
- Number row, candidate paging keys, scrolling, session snippets, and prompt shortcuts.
- Optional Windows touch-keyboard suppression with crash-recovery lease.
- Per-user startup toggle and system-tray recovery entry.
- Local diagnostics that never record typed or clipboard content.
- Bilingual quick-start documentation with a real main-panel screenshot.
- Portable package includes the English and Chinese README files and the MIT license.

### Known limitations

- Windows 11 and Chinese UI are the initial tested surface.
- Elevated windows and the UAC secure desktop are unsupported.
- Window avoidance currently uses target-window resizing; AppBar reservation remains under evaluation.

### Validation baseline

- .NET 10 Release build: 0 warnings, 0 errors.
- Eight layout-policy checks passed.
- Interactive 3840×2064 / 200% DPI check exposed 55 buttons, delivered a key event through
  an active Chinese IME, preserved a 3 px seam, restored the target rectangle, and left the
  Windows touch-keyboard registry values unchanged.
