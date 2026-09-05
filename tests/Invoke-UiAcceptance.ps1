param(
    [string]$Configuration = 'Release',
    [string]$AppPath,
    [string]$ProbePath,
    [string]$ResultPath,
    [string]$ScreenshotPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $AppPath) { $AppPath = Join-Path $root "src\LuckyTouchInput\bin\$Configuration\net10.0-windows\LuckyTouchInput.exe" }
if (-not $ProbePath) { $ProbePath = Join-Path $root "tests\LuckyTouchInput.InputProbe\bin\$Configuration\net10.0-windows\LuckyTouchInput.InputProbe.exe" }

if (-not (Test-Path -LiteralPath $appPath)) { throw "Build output not found: $appPath" }
if (-not (Test-Path -LiteralPath $probePath)) { throw "Probe output not found: $probePath" }
if (Get-Process LuckyTouchInput -ErrorAction SilentlyContinue) {
    throw 'Close any existing Lucky Touch Input instance before running acceptance.'
}
$profilePath = Join-Path $env:LOCALAPPDATA 'Lucky\TouchInput\settings.ini'
$legacyPath = Join-Path $env:LOCALAPPDATA 'LuckyTouchKeyboard\settings.ini'
if ((Test-Path -LiteralPath $profilePath) -or (Test-Path -LiteralPath $legacyPath)) {
    throw 'Run expanded UI acceptance in a disposable Windows account without existing keyboard settings.'
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Threading;

public static class LuckyTouchInputAcceptanceNative
{
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint source, uint target, bool attach);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter,
        int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy,
        uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out RECT rect, int size);
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadKeyboardLayout(string layout, uint flags);

    public static IntPtr FindVisibleWindow(uint wantedProcessId)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
        {
            uint processId;
            GetWindowThreadProcessId(hwnd, out processId);
            if (processId == wantedProcessId && IsWindowVisible(hwnd))
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static bool ForceForeground(IntPtr hwnd)
    {
        IntPtr foreground = GetForegroundWindow();
        uint ignored;
        uint currentThread = GetCurrentThreadId();
        uint foregroundThread = GetWindowThreadProcessId(foreground, out ignored);
        uint targetThread = GetWindowThreadProcessId(hwnd, out ignored);
        bool attachedForeground = foregroundThread != 0 && currentThread != foregroundThread &&
            AttachThreadInput(currentThread, foregroundThread, true);
        bool attachedTarget = targetThread != 0 && currentThread != targetThread &&
            AttachThreadInput(currentThread, targetThread, true);
        try
        {
            ShowWindow(hwnd, 9);
            BringWindowToTop(hwnd);
            return SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attachedTarget) AttachThreadInput(currentThread, targetThread, false);
            if (attachedForeground) AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    public static bool ActivateByClick(IntPtr hwnd)
    {
        RECT rect;
        POINT original;
        GetWindowRect(hwnd, out rect);
        bool restorePointer = GetCursorPos(out original);
        SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
        SetCursorPos(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(100);
        SetWindowPos(hwnd, new IntPtr(-2), 0, 0, 0, 0, 0x0013);
        if (restorePointer) SetCursorPos(original.X, original.Y);
        return GetForegroundWindow() == hwnd;
    }

    public static void ClickPoint(int x, int y)
    {
        POINT original;
        bool restorePointer = GetCursorPos(out original);
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(100);
        if (restorePointer) SetCursorPos(original.X, original.Y);
    }
}
'@

[LuckyTouchInputAcceptanceNative]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

function Get-VisibleWindow([System.Diagnostics.Process]$Process) {
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        Start-Sleep -Milliseconds 100
        $handle = [LuckyTouchInputAcceptanceNative]::FindVisibleWindow([uint32]$Process.Id)
        if ($handle -ne [IntPtr]::Zero) { return $handle }
    }
    throw "Visible window not found for process $($Process.Id)."
}

function Get-RegistryState([string]$Name) {
    $path = 'HKCU:\Software\Microsoft\TabletTip\1.7'
    if (-not (Test-Path -LiteralPath $path)) { return 'missing-key' }
    $item = Get-ItemProperty -LiteralPath $path -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $item) { return 'missing' }
    return [string]$item.$Name
}

function Click-KeyboardButton([string]$Name) {
    $rootElement = [System.Windows.Automation.AutomationElement]::FromHandle($appHwnd)
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $candidates = $rootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
    $button = $null
    foreach ($candidate in $candidates) {
        if ($candidate.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and
            -not $candidate.Current.IsOffscreen) { $button = $candidate; break }
    }
    if ($null -eq $button) { throw "Visible keyboard button missing: $Name" }
    $bounds = $button.Current.BoundingRectangle
    if ($bounds.Width -le 0 -or $bounds.Height -le 0) { throw "Unusable keyboard button: $Name" }
    [LuckyTouchInputAcceptanceNative]::ClickPoint([int]($bounds.Left + $bounds.Width / 2), [int]($bounds.Top + $bounds.Height / 2))
    Start-Sleep -Milliseconds 200
}

function Read-EditorText {
    return $editor.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
}

function Assert-Check([string]$Name, [bool]$Passed) {
    $script:checks[$Name] = $Passed
    if (-not $Passed) { throw "UI check failed: $Name" }
}

$checks = [ordered]@{}

$probe = $null
$app = $null
$recoveryApp = $null
$result = $null
try {
    $beforeTap = Get-RegistryState 'TouchKeyboardTapInvoke'
    $beforeDesktop = Get-RegistryState 'EnableDesktopModeAutoInvoke'

    $probe = Start-Process -FilePath $probePath -PassThru
    $probeHwnd = Get-VisibleWindow $probe
    $beforeRect = New-Object LuckyTouchInputAcceptanceNative+RECT
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($probeHwnd, [ref]$beforeRect) | Out-Null

    $app = Start-Process -FilePath $appPath -PassThru
    $appHwnd = Get-VisibleWindow $app
    if (-not [LuckyTouchInputAcceptanceNative]::ForceForeground($probeHwnd)) {
        [LuckyTouchInputAcceptanceNative]::ActivateByClick($probeHwnd) | Out-Null
    }
    Start-Sleep -Milliseconds 1000
    if ([LuckyTouchInputAcceptanceNative]::GetForegroundWindow() -ne $probeHwnd) {
        throw 'The acceptance probe could not become the foreground window.'
    }

    $probeRoot = [System.Windows.Automation.AutomationElement]::FromHandle($probeHwnd)
    $editCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $editor = $probeRoot.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants, $editCondition)
    if ($null -eq $editor) { throw 'The probe editor was not exposed through UI Automation.' }
    $editor.SetFocus()
    # This phase asserts committed ASCII text, not an IME's uncommitted composition.
    $englishLayout = [LuckyTouchInputAcceptanceNative]::LoadKeyboardLayout('00000409', 0)
    [LuckyTouchInputAcceptanceNative]::PostMessage($probeHwnd, 0x0050, [IntPtr]::Zero, $englishLayout) | Out-Null
    Start-Sleep -Milliseconds 200
    $focusedBefore = [System.Windows.Automation.AutomationElement]::FocusedElement
    $focusBefore = "$($focusedBefore.Current.ProcessId):$($focusedBefore.Current.ControlType.ProgrammaticName):$($focusedBefore.Current.Name)"

    $keyboardRect = New-Object LuckyTouchInputAcceptanceNative+RECT
    $targetRect = New-Object LuckyTouchInputAcceptanceNative+RECT
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($appHwnd, [ref]$keyboardRect) | Out-Null
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($probeHwnd, [ref]$targetRect) | Out-Null
    [LuckyTouchInputAcceptanceNative]::DwmGetWindowAttribute($probeHwnd, 9, [ref]$targetRect, 16) | Out-Null

    $keyboardRoot = [System.Windows.Automation.AutomationElement]::FromHandle($appHwnd)
    $buttonCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $buttons = $keyboardRoot.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
    $aKey = $null
    foreach ($button in $buttons) {
        if ($button.Current.Name -eq 'a') { $aKey = $button; break }
    }
    if ($null -eq $aKey) { throw 'The A key was not exposed through UI Automation.' }
    $keyBounds = $aKey.Current.BoundingRectangle
    [LuckyTouchInputAcceptanceNative]::ClickPoint(
        [int]($keyBounds.Left + $keyBounds.Width / 2),
        [int]($keyBounds.Top + $keyBounds.Height / 2))
    Start-Sleep -Milliseconds 600
    $focusedAfter = [System.Windows.Automation.AutomationElement]::FocusedElement
    $focusAfter = "$($focusedAfter.Current.ProcessId):$($focusedAfter.Current.ControlType.ProgrammaticName):$($focusedAfter.Current.Name)"
    $probeTitleAfterInput = [System.Windows.Automation.AutomationElement]::FromHandle(
        $probeHwnd).Current.Name

    $probeRoot = [System.Windows.Automation.AutomationElement]::FromHandle($probeHwnd)
    $editor = $probeRoot.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants, $editCondition)
    $typedText = $editor.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    $textPatternObject = $null
    if ([string]::IsNullOrEmpty($typedText) -and
        $editor.TryGetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern,
            [ref]$textPatternObject)) {
        $typedText = ([System.Windows.Automation.TextPattern]$textPatternObject).
            DocumentRange.GetText(-1)
    }

    $workArea = [System.Windows.Forms.Screen]::FromHandle($appHwnd).WorkingArea
    [pscustomobject]@{ Text = $typedText; FocusBefore = $focusBefore; FocusAfter = $focusAfter;
        ProbeTitle = $probeTitleAfterInput; Keyboard = $keyboardRect; Target = $targetRect } | ConvertTo-Json -Depth 3 | Write-Output
    Assert-Check 'actual_text' ($typedText -eq 'a')
    Assert-Check 'focus_retained' ($focusBefore -eq $focusAfter)
    Assert-Check 'three_pixel_seam' (($keyboardRect.Top - $targetRect.Bottom) -eq 3)
    Assert-Check 'taskbar_clear' ($keyboardRect.Bottom -eq $workArea.Bottom)
    Assert-Check 'large_key_count' ($buttons.Count -ge 50)

    Click-KeyboardButton '退格'
    Assert-Check 'backspace' ((Read-EditorText) -eq '')
    Click-KeyboardButton '1'
    Click-KeyboardButton '⇧'
    Click-KeyboardButton 'a'
    Assert-Check 'number_and_shift' ((Read-EditorText) -eq '1A')
    Click-KeyboardButton '回车'
    Assert-Check 'enter_inserts_newline' ((Read-EditorText) -match '^1A\r?\n$')
    $sample = New-Object LuckyTouchInputAcceptanceNative+RECT
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($appHwnd, [ref]$sample) | Out-Null
    Assert-Check 'enter_stays_open' ($sample.Height -eq $keyboardRect.Height)

    Click-KeyboardButton '跟随前台'
    $stable = $true
    for ($index = 0; $index -lt 24; $index++) {
        Start-Sleep -Milliseconds 125
        [LuckyTouchInputAcceptanceNative]::GetWindowRect($appHwnd, [ref]$sample) | Out-Null
        $stable = $stable -and [LuckyTouchInputAcceptanceNative]::IsWindowVisible($appHwnd) -and $sample.Height -eq $keyboardRect.Height
    }
    Assert-Check 'pinned_low_resolution_stable_24_samples' $stable
    Click-KeyboardButton '固定应用'
    $floatingTarget = New-Object LuckyTouchInputAcceptanceNative+RECT
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($probeHwnd, [ref]$floatingTarget) | Out-Null
    Assert-Check 'floating_restores_target' ($floatingTarget.Top -eq $beforeRect.Top -and $floatingTarget.Height -eq $beforeRect.Height)
    Click-KeyboardButton '自由悬浮'

    Click-KeyboardButton '设置'
    Click-KeyboardButton '高度 −'
    Assert-Check 'settings_height_saved' ((Get-Content -LiteralPath $profilePath -Raw) -match 'FollowHeight=38')
    Click-KeyboardButton '高度 +'
    Click-KeyboardButton '回车：常驻'
    Click-KeyboardButton '数字'
    Click-KeyboardButton '回车'
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($appHwnd, [ref]$sample) | Out-Null
    Assert-Check 'explicit_enter_collapse' ($sample.Height -lt $keyboardRect.Height)
    Click-KeyboardButton '⌨  打开键盘'
    Click-KeyboardButton '设置'
    Click-KeyboardButton '回车：收起'
    Click-KeyboardButton '系统键盘：共存'
    $leasePath = Join-Path $env:LOCALAPPDATA 'Lucky\TouchInput\native-keyboard-lease.ini'
    Assert-Check 'suppression_lease_created' (Test-Path -LiteralPath $leasePath)
    Assert-Check 'suppression_opt_in' ((Get-RegistryState 'TouchKeyboardTapInvoke') -eq '0')
    Click-KeyboardButton '系统键盘：抑制'
    Assert-Check 'suppression_round_trip' ($beforeTap -eq (Get-RegistryState 'TouchKeyboardTapInvoke') -and
        $beforeDesktop -eq (Get-RegistryState 'EnableDesktopModeAutoInvoke'))
    Assert-Check 'lease_removed_after_opt_out' (-not (Test-Path -LiteralPath $leasePath))
    Click-KeyboardButton '数字'

    $liveRoot = [System.Windows.Automation.AutomationElement]::FromHandle($appHwnd)
    $liveButtons = $liveRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
    $digitCount = @($liveButtons | Where-Object { $_.Current.Name -match '^\d$' -and -not $_.Current.IsOffscreen }).Count
    Assert-Check 'ten_numbers_restored' ($digitCount -eq 10)
    Click-KeyboardButton '—'
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($appHwnd, [ref]$sample) | Out-Null
    Assert-Check 'single_collapse_tap_is_safe' ($sample.Height -eq $keyboardRect.Height)
    Click-KeyboardButton '—'
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($appHwnd, [ref]$sample) | Out-Null
    Assert-Check 'double_collapse_tap' ($sample.Height -lt $keyboardRect.Height)
    $second = Start-Process -FilePath $appPath -PassThru
    $second.WaitForExit(3000) | Out-Null
    Start-Sleep -Milliseconds 400
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($appHwnd, [ref]$sample) | Out-Null
    Assert-Check 'second_launch_restores_keyboard' ($sample.Height -eq $keyboardRect.Height)
    Assert-Check 'single_instance' (@(Get-Process LuckyTouchInput).Count -eq 1)

    $titleBeforePaste = [System.Windows.Automation.AutomationElement]::FromHandle($probeHwnd).Current.Name
    $enterCountBefore = [regex]::Match($titleBeforePaste, 'EnterCount=(\d+)').Groups[1].Value
    [System.Windows.Forms.Clipboard]::SetText("paste-one`npaste-two`tvalue")
    Click-KeyboardButton '变量'
    Click-KeyboardButton '剪贴板'
    $pasteText = (Read-EditorText).Replace("`r`n", "`n")
    $titleAfterPaste = [System.Windows.Automation.AutomationElement]::FromHandle($probeHwnd).Current.Name
    $enterCountAfter = [regex]::Match($titleAfterPaste, 'EnterCount=(\d+)').Groups[1].Value
    Assert-Check 'literal_multiline_paste' ($pasteText.EndsWith("paste-one`npaste-two`tvalue"))
    Assert-Check 'paste_does_not_press_enter' ($enterCountBefore -ne '' -and $enterCountBefore -eq $enterCountAfter)
    Assert-Check 'paste_preserves_editor_focus' ([System.Windows.Automation.AutomationElement]::FocusedElement.Current.ProcessId -eq $probe.Id)
    Click-KeyboardButton '数字'

    # Wheel events must reach the focused editor even while the pointer is over the keyboard.
    Click-KeyboardButton '工具'
    Click-KeyboardButton '滚↓'
    $probeTitle = [System.Windows.Automation.AutomationElement]::FromHandle($probeHwnd).Current.Name
    Assert-Check 'wheel_reaches_editor' ($probeTitle -like '*Wheel=-360*')
    Click-KeyboardButton '候选›'
    $probeTitle = [System.Windows.Automation.AutomationElement]::FromHandle($probeHwnd).Current.Name
    Assert-Check 'candidate_page_key' ($probeTitle -like '*KeyDown=Next*')
    Click-KeyboardButton '数字'

    [LuckyTouchInputAcceptanceNative]::ShowWindow($probeHwnd, 6) | Out-Null
    Start-Sleep -Milliseconds 600
    Assert-Check 'intentional_minimize_preserved' ([LuckyTouchInputAcceptanceNative]::IsIconic($probeHwnd))
    [LuckyTouchInputAcceptanceNative]::ForceForeground($probeHwnd) | Out-Null
    $editor.SetFocus()
    Start-Sleep -Milliseconds 600
    Assert-Check 'restore_from_minimize' ([LuckyTouchInputAcceptanceNative]::IsWindowVisible($appHwnd))

    if ($ScreenshotPath) {
        Start-Sleep -Seconds 4
        $screenBounds = [System.Windows.Forms.Screen]::FromHandle($appHwnd).Bounds
        [LuckyTouchInputAcceptanceNative]::GetWindowRect($appHwnd, [ref]$sample) | Out-Null
        $panelBounds = [Drawing.Rectangle]::FromLTRB($sample.Left, $sample.Top, $sample.Right, $sample.Bottom)
        foreach ($capture in @(
            @{ Rectangle = $screenBounds; Path = $ScreenshotPath },
            @{ Rectangle = $panelBounds; Path = [IO.Path]::ChangeExtension($ScreenshotPath, 'keyboard.png') }
        )) {
            $bitmap = [Drawing.Bitmap]::new($capture.Rectangle.Width, $capture.Rectangle.Height)
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.CopyFromScreen($capture.Rectangle.Location, [Drawing.Point]::Empty, $capture.Rectangle.Size)
                $bitmap.Save($capture.Path, [Drawing.Imaging.ImageFormat]::Png)
            }
            finally { $graphics.Dispose(); $bitmap.Dispose() }
        }
    }

    [LuckyTouchInputAcceptanceNative]::PostMessage(
        $appHwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    for ($attempt = 0; $attempt -lt 50 -and
        (Get-Process -Id $app.Id -ErrorAction SilentlyContinue); $attempt++) {
        Start-Sleep -Milliseconds 100
    }

    $restoredRect = New-Object LuckyTouchInputAcceptanceNative+RECT
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($probeHwnd, [ref]$restoredRect) | Out-Null
    $leasePath = Join-Path $env:LOCALAPPDATA 'Lucky\TouchInput\native-keyboard-lease.ini'
    # Deliberately crash only our fixture process to prove next-launch registry recovery.
    $configurationText = Get-Content -LiteralPath $profilePath -Raw
    [IO.File]::WriteAllText($profilePath, $configurationText.Replace('SuppressNativeKeyboard=False', 'SuppressNativeKeyboard=True'))
    $recoveryApp = Start-Process -FilePath $appPath -PassThru
    $recoveryHwnd = Get-VisibleWindow $recoveryApp
    Assert-Check 'crash_fixture_has_lease' (Test-Path -LiteralPath $leasePath)
    Stop-Process -Id $recoveryApp.Id -Force
    $recoveryApp.WaitForExit(3000) | Out-Null
    Assert-Check 'crash_keeps_recovery_lease' (Test-Path -LiteralPath $leasePath)
    [IO.File]::WriteAllText($profilePath, $configurationText)
    $recoveryApp = Start-Process -FilePath $appPath -PassThru
    $recoveryHwnd = Get-VisibleWindow $recoveryApp
    Assert-Check 'crash_registry_recovered' ($beforeTap -eq (Get-RegistryState 'TouchKeyboardTapInvoke') -and
        $beforeDesktop -eq (Get-RegistryState 'EnableDesktopModeAutoInvoke'))
    Assert-Check 'crash_lease_cleared' (-not (Test-Path -LiteralPath $leasePath))
    [LuckyTouchInputAcceptanceNative]::PostMessage($recoveryHwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Assert-Check 'recovery_instance_exits' ($recoveryApp.WaitForExit(5000))
    $result = [pscustomobject]@{
        KeyboardRect = "$($keyboardRect.Left),$($keyboardRect.Top),$($keyboardRect.Right),$($keyboardRect.Bottom)"
        TargetRect = "$($targetRect.Left),$($targetRect.Top),$($targetRect.Right),$($targetRect.Bottom)"
        BeforeRect = "$($beforeRect.Left),$($beforeRect.Top),$($beforeRect.Right),$($beforeRect.Bottom)"
        RestoredRect = "$($restoredRect.Left),$($restoredRect.Top),$($restoredRect.Right),$($restoredRect.Bottom)"
        SeamPixels = $keyboardRect.Top - $targetRect.Bottom
        KeyCount = $buttons.Count
        TypedText = $typedText
        FocusBefore = $focusBefore
        FocusAfter = $focusAfter
        ProbeTitleAfterInput = $probeTitleAfterInput
        InputPassed = $typedText -eq 'a'
        Checks = $checks
        TargetRestored = $beforeRect.Left -eq $restoredRect.Left -and
            $beforeRect.Top -eq $restoredRect.Top -and
            $beforeRect.Right -eq $restoredRect.Right -and
            $beforeRect.Bottom -eq $restoredRect.Bottom
        NativeSettingsUnchanged = $beforeTap -eq (Get-RegistryState 'TouchKeyboardTapInvoke') -and
            $beforeDesktop -eq (Get-RegistryState 'EnableDesktopModeAutoInvoke')
        RecoveryLeaseAbsent = -not (Test-Path -LiteralPath $leasePath)
        AppExited = -not (Get-Process -Id $app.Id -ErrorAction SilentlyContinue)
    }
}
finally {
    if ($null -ne $recoveryApp -and (Get-Process -Id $recoveryApp.Id -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $recoveryApp.Id -Force
    }
    if ($null -ne $app -and (Get-Process -Id $app.Id -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $app.Id -Force
    }
    if ($null -ne $probe -and (Get-Process -Id $probe.Id -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $probe.Id -Force
    }
}

$result | Format-List
if ($ResultPath) { $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultPath -Encoding UTF8 }
if (-not ($result.InputPassed -and $result.TargetRestored -and
    $result.NativeSettingsUnchanged -and $result.RecoveryLeaseAbsent -and
    $result.AppExited -and $result.SeamPixels -eq 3)) {
    throw 'Lucky Touch Input UI acceptance failed.'
}
