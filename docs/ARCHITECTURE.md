# Architecture

Lucky Touch Input is one small WinForms application. The code is separated by responsibility,
without a runtime plugin system:

- `KeyboardForm`: touch UI, layout, visibility, and user actions.
- `Native`: Win32 input, foreground-window discovery, and input-pane integration.
- `UserPreferences`: versioned, human-readable per-user settings.
- `NativeKeyboardGuard`: optional Windows touch-keyboard suppression and recovery.
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
