# Validation

## 0.1.0-alpha.1 — 2026-09-08

### Automated checks

- .NET 10 Release build: 0 warnings, 0 errors.
- 350 automated checks, including legacy layout migration, all three preset switches, one-shot
  modifier input sequences, native ownership combinations, secure-surface exclusion, native-pane
  geometry filtering, and lease acquisition/recovery/non-interference under a disposable HKCU key.
- Real WinForms control layouts at 1280×752, 800×1280, 2480×2152, and 3840×2064 work areas:
  26 letters / 10 digits, desktop row stagger, rightmost Enter, usable key bounds, no taskbar overlap,
  and single-line Tab/Ctrl/Backspace/Enter labels. Rendered portrait and landscape controls inspected.
- Layout rendering disables runtime hooks and target resizing. Tests verify the real settings and
  recovery-file contents remain unchanged; no actual user input or system keyboard preference change
  is required. Registry tests remove their random test-owned key and temporary directory.

### Interactive offline Windows check

Windows 11 Enterprise LTSC 2024 Evaluation, build 26100, started from the existing clean baseline;
network disconnected; self-contained EXE without an installed .NET SDK/Desktop Runtime.
The completed run has **59/59 named checks passing**, task exit code 0, no remaining app/probe/OSK
fixture process, and no remaining native-settings recovery lease. App, probe, and harness run under
the limited interactive test account, not elevated.

Coverage includes actual committed text and retained focus; all three layouts; Ctrl+A and one-shot
modifier release; cursor editing and Shift selection; modified Enter without accidental collapse;
editing shortcuts; Enter stay/collapse; settings persistence; 24 stability samples; target
minimize/restore; scrolling; candidate-page key delivery; single-instance restore; literal multiline
paste; native lease release/reacquisition; and settings recovery after a forced fixture-process exit.

Both native policies were tested against the **real accessibility OSK**: OSK stayed visible, Lucky
yielded, the original target rectangle and native settings were restored, and Lucky resumed after
OSK disappeared. Mutual mode reacquired its lease. The first attempt stopped at fixture cleanup:
Windows denied `WM_CLOSE` with error 5. The completed run used a separate lab controller to terminate
only each test-created OSK after matching PID, start time, session and system executable path.
This validates handoff/resumption, **not** Lucky closing OSK or bypassing Windows integrity boundaries.
No input service or UIPI policy was changed. See [harness instructions](../CONTRIBUTING.md).

| Measurement | Result |
| --- | --- |
| Display / working area | 1280×800 / 1280×752 |
| Keyboard bounds | (0,392)–(1280,752) |
| Target visible frame (DWM) | (0,0)–(1280,389) |
| Visible seam / taskbar overlap | 3 px / 0 px |
| Original / normally restored target | (190,76)–(1090,676), identical |

The [acceptance screenshot](../assets/lucky-touch-input-clean-win11-alpha1.png) is captured from this
run without image editing. It contains only the disposable input probe and the test desktop.
The tested executable SHA-256 is:

`EDCF052BE9F1573418B149F664FB456F4717375ECEBB05FF4AD261F84E0B3F5B`

### Not established by this test

Real UU phone/foldable input, physical touch, every DPI/app minimum-size combination, Chinese
composition and candidate selection, secure-desktop/UAC behaviour, and zero transient **touch**
keyboard popup frames still need explicit testing. The real-OSK check is not a test of the separate
modern Windows touch keyboard. Its geometry filtering, ownership and settings lease are covered
by automated tests; suppression remains experimental. Forced app termination does not restore the
resized target's geometry; its native settings recover at the next launch.

## Published 0.1.0-alpha

Date: 2026-09-05. These results describe the tested executable, not a certification of remote clients.

### Automated checks

- .NET 10 Release build: 0 warnings, 0 errors.
- 58 settings/layout assertions: prototype migration, new-profile precedence, atomic writes,
  invalid values, portrait/foldable/landscape sizing, negative monitor coordinates, and taskbar bounds.
- Package checks: required files, runtime licenses, no local settings/development artifacts, and
  all relative Markdown/image links inside the ZIP.
- A wrong-SDK attempt leaves the previous release ZIP unchanged.

### Interactive clean Windows check

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

### Not established by this test

- Real fingers/stylus input, mobile-client latency, all DPI transitions, or every app's minimum size.
- Chinese candidate composition/selection and third-party IME compatibility on this final build.
- Zero transient native-keyboard frames during every target-app open/close sequence. Suppression is
  experimental; reports should distinguish the touch keyboard from accessibility `osk.exe`.
- Elevated/UAC input, OSK suppression, or geometry recovery after a process is forcibly killed.

Report these with Windows build, display size/scale, mode, IME, client/device, and sanitized screenshots.
