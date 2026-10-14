; AutoHotkey v1 example. Set this path to your extracted compact executable.
; Merge manually; do not install a second LButton Up handler beside an existing one.
#NoEnv
#SingleInstance Force
EnvGet, compactLocalAppData, LOCALAPPDATA
CompactExe := compactLocalAppData . "\Programs\LuckyTouchInput-CompactTest\LuckyTouchInput.CompactTest.exe"

~LButton Up::
    CompactTripleTap()
return

CompactTripleTap()
{
    global CompactExe
    static count := 0, firstAt := 0, firstX := 0, firstY := 0, lastLaunch := 0
    CoordMode, Mouse, Screen
    MouseGetPos, x, y, tappedWindow
    WinGet, processName, ProcessName, ahk_id %tappedWindow%
    if (processName = "LuckyTouchInput.exe" || processName = "LuckyTouchInput.CompactTest.exe")
    {
        count := 0
        return
    }
    now := A_TickCount
    if (lastLaunch && now - lastLaunch >= 0 && now - lastLaunch < 1500)
        return
    if (!count || now - firstAt < 0 || now - firstAt > 800
        || Abs(x - firstX) > 40 || Abs(y - firstY) > 40)
    {
        count := 1
        firstAt := now
        firstX := x
        firstY := y
        return
    }
    count += 1
    if (count < 3)
        return
    count := 0
    lastLaunch := now
    if FileExist(CompactExe)
        Run, % """" . CompactExe . """",, UseErrorLevel
}
