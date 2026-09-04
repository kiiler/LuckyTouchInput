# Lucky Touch Input

Adaptive on-screen keyboard and input panel for Windows tablets and remote sessions.

> Alpha software. Lucky Touch Input does not create a remote connection and is not an input method.
> It runs on the Windows host and works alongside tools such as RustDesk, TeamViewer, UU Remote,
> Moonlight, and Windows App.

[简体中文](README.zh-CN.md)

![Lucky Touch Input main keyboard](assets/lucky-touch-input-main.png)

## Why

Phone and tablet remote-control clients often make the Windows touch keyboard hard to summon,
cover the active editor, or move the desktop unpredictably. Windows tablets can have the same
problem when the standard keyboard is too dense. Lucky Touch Input provides large, persistent keys
and a predictable window-avoidance layer on the Windows host.

## 0.1 alpha features

- No-activation, always-on-top touch panel: tapping a key does not intentionally steal editor focus.
- Follow-foreground, pinned-application, and floating modes.
- Responsive geometry for phones, tablets, foldables, DPI scaling, and multiple monitors.
- Standard QWERTY rows, persistent numbers, Shift, Backspace, Enter, candidate paging, and scrolling.
- Volatile session snippets and visible prompt insertion; neither submits text automatically.
- Optional Microsoft Pinyin full/double-pinyin switch.
- Optional Windows touch-keyboard suppression with recovery after an abnormal exit.
- Per-user startup toggle and a tray-menu recovery path.
- No network, telemetry, account, updater, or third-party runtime package.

## Requirements

- Windows 11 x64
- .NET 10 Desktop Runtime when using a framework-dependent build
- No administrator privileges

The release artifact is intended to be self-contained and portable.

## Quick start

1. Extract the portable x64 package and run `LuckyTouchInput.exe`.
2. Focus the editor or text field you want to type into. The panel follows that foreground window
   and leaves a small seam above itself.
3. Use the mode button to cycle through **Follow foreground**, **Pinned application**, and
   **Floating**. Open **Settings** to adjust height, left seam, Enter behaviour, startup, and
   coexistence with the Windows touch keyboard.
4. Tap `—` twice within two seconds to collapse safely. Double-click the tray icon, or use its
   **Show keyboard** menu item, to restore the panel. Use `×` or the tray menu to exit.

The recommended starting point for remote-control clients is **Follow foreground**, Enter set to
stay open, and Windows touch-keyboard suppression left off. Enable suppression only if the native
keyboard repeatedly appears over the remote session.

## Build and run

Install the .NET 10 SDK, then:

```powershell
dotnet build LuckyTouchInput.sln -c Release
dotnet run --project tests/LuckyTouchInput.Tests -c Release
dotnet run --project src/LuckyTouchInput/LuckyTouchInput.csproj -c Release
```

On an interactive Windows desktop, `pwsh -File tests/Invoke-UiAcceptance.ps1` verifies a real
no-activation key event, target-window avoidance, restoration, and registry non-interference.

Create a portable x64 build:

```powershell
pwsh -File scripts/Publish.ps1
```

## Modes

- **Follow foreground**: target and resize the current ordinary foreground window.
- **Pinned application**: keep targeting the process that was active when the mode was selected.
- **Floating**: keep the keyboard visible without resizing the target.

Settings are written to `%LOCALAPPDATA%\Lucky\TouchInput\settings.ini`. Enter keeps the keyboard
open by default. Windows touch-keyboard suppression is off by default.

## Important boundaries

- The active Windows IME owns text composition and candidates. Candidate buttons emit Page Up and
  Page Down, which can be remapped by an IME.
- `SendInput` is restricted by Windows integrity levels. Elevated targets and the UAC secure desktop
  are unsupported.
- Native keyboard suppression uses Windows per-user input settings and an input-pane observer. It is
  marked experimental and should be enabled only when the standard keyboard conflicts with the app.
- The initial UI is Chinese. English UI resources are planned after the interaction model stabilizes.

See [architecture](docs/ARCHITECTURE.md), [privacy](docs/PRIVACY.md),
[security](SECURITY.md), and [contributing](CONTRIBUTING.md).

## Project status

`0.1.0-alpha` is the first public-design baseline. The self-contained portable package passed an
offline first-launch acceptance on a clean Windows 11 Enterprise LTSC 2024 system (build 26100) at
1280×752: 55 buttons were exposed, an actual key event was delivered without stealing focus, Enter
kept the panel open, the 3 px seam and taskbar remained visible, the target window was restored,
and no native-keyboard, startup, or installer state was left behind.

![Clean Windows 11 acceptance at 1280×752](assets/lucky-touch-input-clean-win11.png)

Before a signed release, the project still needs validation on real touch hardware and a final
application icon.

## License

[MIT](LICENSE)

<details>
<summary>Author and support</summary>

Author: free2ing (零西) · haoyunmail@qq.com

<img src="assets/support-alipay.png" alt="Support via Alipay" width="120">
<img src="assets/support-wechat.png" alt="Support via WeChat" width="120">

</details>
