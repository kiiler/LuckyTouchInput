# 0.1.0-alpha validation

Date: 2026-09-05. These results describe the tested executable, not a certification of remote clients.

## Automated checks

- .NET 10 Release build: 0 warnings, 0 errors.
- 58 settings/layout assertions: prototype migration, new-profile precedence, atomic writes,
  invalid values, portrait/foldable/landscape sizing, negative monitor coordinates, and taskbar bounds.
- Package checks: required files, runtime licenses, no local settings/development artifacts, and
  all relative Markdown/image links inside the ZIP.
- A wrong-SDK attempt leaves the previous release ZIP unchanged.

## Interactive clean Windows check

Windows 11 Enterprise LTSC 2024 Evaluation, build 26100; network disconnected; no installed .NET SDK
or Desktop Runtime. A self-contained EXE and the disposable input probe are used. All 34 named UI
checks pass, as do normal-exit target restoration and registry non-interference.

| Measurement | Result |
| --- | --- |
| Display / working area | 1280×800 / 1280×752 |
| Keyboard bounds | (0,392)–(1280,752) |
| Target visible frame (DWM) | (0,0)–(1280,389) |
| Visible seam / taskbar overlap | 3 px / 0 px |
| Exposed buttons | 55 |
| Original / normally restored target | (190,76)–(1090,676), identical |

The UI harness asserts committed text, focus retention, numbers/Shift/Backspace, Enter newline and
stay/collapse settings, pinned-mode stability over 24 samples, floating restoration, settings
persistence, all ten numbers returning, double-tap collapse, second-launch recovery, one instance,
literal multiline paste without an Enter key event, focused-control wheel delivery, Page Down key
delivery, intentional minimize/restore, registry round trips, and forced-process-kill lease recovery.

The ASCII phase explicitly selects the English keyboard layout. An uncommitted IME composition or
merely receiving KeyDown does not count as successful text input. Screenshots are captured from the
running app; they are not design mockups. The tested executable SHA-256 is:

`466CC022AE1550A3FDBB9D2E0788BCC8E71FB333532C2B62DAC01F1937EFD682`

## Not established by this test

- Real fingers/stylus input, mobile-client latency, all DPI transitions, or every app's minimum size.
- Chinese candidate composition/selection and third-party IME compatibility on this final build.
- Zero transient native-keyboard frames during every target-app open/close sequence. Suppression is
  experimental; reports should distinguish the touch keyboard from accessibility `osk.exe`.
- Elevated/UAC input, OSK suppression, or geometry recovery after a process is forcibly killed.

Report these with Windows build, display size/scale, mode, IME, client/device, and sanitized screenshots.
