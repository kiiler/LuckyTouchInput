# Architecture

Lucky Touch Input is one small WinForms application. The code is separated by responsibility,
without a runtime plugin system:

- `KeyboardForm`: touch UI, layout, visibility, and user actions.
- `Native`: Win32 input, foreground-window discovery, and input-pane integration.
- `UserPreferences`: versioned, human-readable per-user settings.
- `KeyboardLayout`: Standard, PhoneLarge, and Everyday presets; layout is independent of targeting mode.
- `KeyboardInteropPolicy`: pure ownership and native-pane eligibility rules.
- `NativeKeyboardGuard`: optional touch-keyboard observer and active-suppression lifetime.
- `NativeKeyboardLease`: atomic recovery record and compare-before-restore per-user settings.
- `LayoutPolicy`: pure geometry rules shared by the app and test harness.

## Modes

- **Follow foreground** sends input to and resizes the current ordinary foreground window.
- **Pinned application** stays attached to one process selected from the current target.
- **Floating** sends input to the current target without resizing it.

Docking is an implementation detail, not the product identity. A future AppBar strategy may be
added behind the same mode boundary if it proves more reliable than per-window resizing.

## Input ownership

The application emits standard virtual-key, Unicode, chord, and wheel input. The active Windows
IME owns composition, candidates, dictionaries, and language behavior. Lucky Touch Input is not
a TSF input method and does not maintain a parallel candidate state.

Ctrl/Shift are UI latches, not globally held keys. Each input sequence contains modifier down/up
events; failed submission attempts release those modifiers. Native-keyboard ownership is evaluated
after resolving a valid target, not from the preference flag alone. Hidden/collapsed/ineligible
states release suppression. OSK always wins; an unsuccessful touch-pane takeover waits for dismissal
or an explicit reopen. Target geometry is restored while Lucky yields.

Modern native pane detection requires a visible, non-cloaked window on the target monitor and
matching `IFrameworkInputPane.Location` geometry; the legacy `TabTip`/`IPTip_Main_Window` pair has
a narrow fallback. Unknown modern surfaces are not hidden. The
[Microsoft Location contract](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-iframeworkinputpane-location)
defines screen coordinates and an empty rectangle for no pane; actual Windows builds still need
integration testing. Process presence is not keyboard visibility.

The lease is written before either of the two TabletTip values is changed. Restoration only
replaces values that still equal the suppression value (DWORD zero). External nonzero values,
type changes, or deletions win; a same-value external write cannot be distinguished. Malformed
leases block new acquisition and remain available for diagnosis. The local-only test suite uses
random disposable registry keys, not the user's actual TabletTip settings.

## Experimental compact companion

The fork also builds a separate compact executable. See [compact design](COMPACT-DESIGN.md)
for its geometry, input ownership and isolated profile. The sections above describe the original application.
