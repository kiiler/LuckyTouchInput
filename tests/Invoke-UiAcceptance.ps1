param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appPath = Join-Path $root "src\LuckyTouchInput\bin\$Configuration\net10.0-windows\LuckyTouchInput.exe"
$probePath = Join-Path $root "tests\LuckyTouchInput.InputProbe\bin\$Configuration\net10.0-windows\LuckyTouchInput.InputProbe.exe"

if (-not (Test-Path -LiteralPath $appPath)) { throw "Build output not found: $appPath" }
if (-not (Test-Path -LiteralPath $probePath)) { throw "Probe output not found: $probePath" }
if (Get-Process LuckyTouchInput -ErrorAction SilentlyContinue) {
    throw 'Close any existing Lucky Touch Input instance before running acceptance.'
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
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
    private static extern bool IsWindowVisible(IntPtr hwnd);
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
    private static extern bool ShowWindow(IntPtr hwnd, int command);
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
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

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

$probe = $null
$app = $null
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
    Start-Sleep -Milliseconds 200
    $focusedBefore = [System.Windows.Automation.AutomationElement]::FocusedElement
    $focusBefore = "$($focusedBefore.Current.ProcessId):$($focusedBefore.Current.ControlType.ProgrammaticName):$($focusedBefore.Current.Name)"

    $keyboardRect = New-Object LuckyTouchInputAcceptanceNative+RECT
    $targetRect = New-Object LuckyTouchInputAcceptanceNative+RECT
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($appHwnd, [ref]$keyboardRect) | Out-Null
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($probeHwnd, [ref]$targetRect) | Out-Null

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

    [LuckyTouchInputAcceptanceNative]::PostMessage(
        $appHwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    for ($attempt = 0; $attempt -lt 50 -and
        (Get-Process -Id $app.Id -ErrorAction SilentlyContinue); $attempt++) {
        Start-Sleep -Milliseconds 100
    }

    $restoredRect = New-Object LuckyTouchInputAcceptanceNative+RECT
    [LuckyTouchInputAcceptanceNative]::GetWindowRect($probeHwnd, [ref]$restoredRect) | Out-Null
    $leasePath = Join-Path $env:LOCALAPPDATA 'Lucky\TouchInput\native-keyboard-lease.ini'
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
        InputPassed = $typedText -eq 'a' -or $probeTitleAfterInput -like '*KeyDown=*'
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
    if ($null -ne $app -and (Get-Process -Id $app.Id -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $app.Id -Force
    }
    if ($null -ne $probe -and (Get-Process -Id $probe.Id -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $probe.Id -Force
    }
}

$result | Format-List
if (-not ($result.InputPassed -and $result.TargetRestored -and
    $result.NativeSettingsUnchanged -and $result.RecoveryLeaseAbsent -and
    $result.AppExited -and $result.SeamPixels -eq 3)) {
    throw 'Lucky Touch Input UI acceptance failed.'
}
