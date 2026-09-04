using System;
using Accessibility;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lucky.TouchInput
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWPLACEMENT
    {
        public int length, flags, showCmd;
        public POINT ptMinPosition, ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public INPUTUNION data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public KEYBDINPUT keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, flags, time;
        public UIntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort virtualKey, scanCode;
        public uint flags, time;
        public UIntPtr extraInfo;
    }

    [ComImport, Guid("D5120AA3-46BA-44C5-822D-CA8092C1FC72")]
    internal class FrameworkInputPane { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("5752238B-24F0-495A-82F1-2FD593056796")]
    internal interface IFrameworkInputPane
    {
        [PreserveSig] int Advise([MarshalAs(UnmanagedType.IUnknown)] object window,
            [MarshalAs(UnmanagedType.IUnknown)] object handler, out int cookie);
        [PreserveSig] int AdviseWithHWND(IntPtr hwnd,
            [MarshalAs(UnmanagedType.IUnknown)] object handler, out int cookie);
        [PreserveSig] int Unadvise(int cookie);
        [PreserveSig] int Location(out RECT rect);
    }

    [ComImport, Guid("4CE576FA-83DC-4F88-951C-9D0782B4E376")]
    internal class UIHostNoLaunch { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("37C994E7-432B-4834-A2F7-DCE1F13B834B")]
    internal interface ITipInvocation { void Toggle(IntPtr hwnd); }

    internal static class Native
    {
        internal const uint KEYEVENTF_KEYUP = 0x0002;
        internal const uint KEYEVENTF_UNICODE = 0x0004;
        internal const uint MOUSEEVENTF_WHEEL = 0x0800;
        internal const uint SWP_KEEP_MAXIMIZED_NO_CLAMP = 0x0454;
        internal const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        internal const uint EVENT_OBJECT_SHOW = 0x8002;
        internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        internal const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
        internal const uint WM_QUIT = 0x0012;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint OBJID_CARET = 0xFFFFFFF8;
        private const int SELFLAG_TAKEFOCUS = 0x1;
        private static readonly Guid IidAccessible =
            new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");

        internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        internal delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr hwnd,
            int objectId, int childId, uint threadId, uint eventTime);

        [DllImport("user32.dll")]
        internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")]
        internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
            IntPtr module, WinEventDelegate callback, uint processId, uint threadId, uint flags);
        [DllImport("user32.dll")]
        internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]
        internal static extern bool PostThreadMessage(uint threadId, uint message,
            UIntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")]
        internal static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);
        [DllImport("user32.dll")]
        internal static extern bool SetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr hwnd, char[] className, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message,
            UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);
        [DllImport("oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint objectId,
            ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out IAccessible accessible);

        internal static bool RefocusCaret(IntPtr hwnd)
        {
            IAccessible caret = null;
            try
            {
                Guid interfaceId = IidAccessible;
                if (AccessibleObjectFromWindow(hwnd, OBJID_CARET,
                    ref interfaceId, out caret) != 0 || caret == null)
                    return false;

                int left, top, width, height;
                caret.accLocation(out left, out top, out width, out height, 0);
                RECT target;
                if (!GetWindowRect(hwnd, out target) || height <= 0 ||
                    left < target.Left || left >= target.Right ||
                    top < target.Top || top >= target.Bottom)
                    return false;

                caret.accSelect(SELFLAG_TAKEFOCUS, 0);
                return true;
            }
            // Not every desktop framework exposes an MSAA caret. That is an expected fallback.
            catch (Exception) { return false; }
            finally
            {
                if (caret != null && Marshal.IsComObject(caret))
                    try { Marshal.FinalReleaseComObject(caret); }
                    catch (Exception exception)
                    {
                        Diagnostics.Report("caret.release", exception);
                    }
            }
        }

        internal static IntPtr FindProcessWindow(string processName)
        {
            if (String.IsNullOrWhiteSpace(processName)) return IntPtr.Zero;
            IntPtr foreground = GetForegroundWindow();
            if (String.Equals(ProcessName(foreground), processName,
                StringComparison.OrdinalIgnoreCase) && IsUsableWindow(foreground))
                return foreground;

            var processIds = new HashSet<uint>();
            try
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                    using (process)
                        processIds.Add((uint)process.Id);
            }
            catch (Exception exception)
            {
                Diagnostics.Report("target.enumerate", exception);
                return IntPtr.Zero;
            }
            IntPtr found = IntPtr.Zero;
            long largestArea = 0;
            EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
            {
                uint processId;
                GetWindowThreadProcessId(hwnd, out processId);
                if (processIds.Contains(processId) && IsUsableWindow(hwnd))
                {
                    RECT rect;
                    if (GetWindowRect(hwnd, out rect))
                    {
                        long area = (long)rect.Width * rect.Height;
                        if (area > largestArea)
                        {
                            largestArea = area;
                            found = hwnd;
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static bool IsUsableWindow(IntPtr hwnd)
        {
            RECT rect;
            return hwnd != IntPtr.Zero && IsWindowVisible(hwnd) &&
                GetWindowRect(hwnd, out rect) && rect.Width >= 600 && rect.Height >= 400;
        }

        internal static string ProcessName(IntPtr hwnd)
        {
            uint processId;
            GetWindowThreadProcessId(hwnd, out processId);
            try { return Process.GetProcessById((int)processId).ProcessName; }
            // The process can exit between querying the window and opening the process object.
            catch (Exception) { return String.Empty; }
        }

        internal static string WindowClass(IntPtr hwnd)
        {
            var buffer = new char[256];
            int length = GetClassName(hwnd, buffer, buffer.Length);
            return length > 0 ? new String(buffer, 0, length) : String.Empty;
        }

        internal static bool IsSystemSurface(IntPtr hwnd)
        {
            string process = ProcessName(hwnd);
            string windowClass = WindowClass(hwnd);
            if (String.Equals(windowClass, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(windowClass, "XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(windowClass, "Progman", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(windowClass, "WorkerW", StringComparison.OrdinalIgnoreCase))
                return true;
            if (windowClass.StartsWith("CEF-OSC-WIDGET", StringComparison.OrdinalIgnoreCase) &&
                process.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase))
                return true;
            string[] excluded = {
                "LuckyTouchInput", "ShellExperienceHost", "StartMenuExperienceHost",
                "SearchHost", "LockApp", "LogonUI", "consent"
            };
            foreach (string name in excluded)
                if (String.Equals(process, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        internal static RECT InputPaneRect()
        {
            try
            {
                RECT rect;
                ((IFrameworkInputPane)new FrameworkInputPane()).Location(out rect);
                return rect;
            }
            // The input pane is not available on every Windows desktop surface.
            catch (Exception) { return new RECT(); }
        }

        internal static void HideSystemTouchKeyboard(IntPtr hwnd)
        {
            RECT rect = InputPaneRect();
            if (rect.Width <= 0 || rect.Height <= 0) return;
            try { ((ITipInvocation)new UIHostNoLaunch()).Toggle(hwnd); }
            // Suppression is optional; a missing private touch-keyboard surface is non-fatal.
            catch (Exception) { }
        }

        internal static void Key(ushort virtualKey, bool shifted)
        {
            var inputs = new List<INPUT>();
            if (shifted) inputs.Add(KeyInput(0x10, false));
            inputs.Add(KeyInput(virtualKey, false));
            inputs.Add(KeyInput(virtualKey, true));
            if (shifted) inputs.Add(KeyInput(0x10, true));
            Submit(inputs);
        }

        internal static void Chord(ushort modifier, ushort virtualKey)
        {
            var inputs = new List<INPUT> {
                KeyInput(modifier, false), KeyInput(virtualKey, false),
                KeyInput(virtualKey, true), KeyInput(modifier, true)
            };
            Submit(inputs);
        }

        internal static void Wheel(int detents)
        {
            var input = new INPUT();
            input.type = 0;
            input.data.mouse.mouseData = unchecked((uint)(detents * 120));
            input.data.mouse.flags = MOUSEEVENTF_WHEEL;
            Submit(new List<INPUT> { input });
        }

        internal static void Text(string text)
        {
            if (String.IsNullOrEmpty(text)) return;
            var inputs = new List<INPUT>();
            foreach (char character in text)
            {
                if (character == '\r') continue;
                if (character == '\n' || character == '\t')
                {
                    Submit(inputs);
                    inputs.Clear();
                    Key(character == '\n' ? (ushort)0x0D : (ushort)0x09, false);
                    continue;
                }
                inputs.Add(UnicodeInput(character, false));
                inputs.Add(UnicodeInput(character, true));
                if (inputs.Count >= 240)
                {
                    Submit(inputs);
                    inputs.Clear();
                }
            }
            Submit(inputs);
        }

        internal static void BroadcastPinyinSettings()
        {
            UIntPtr result;
            SendMessageTimeout(new IntPtr(0xFFFF), WM_SETTINGCHANGE, UIntPtr.Zero,
                "Software\\Microsoft\\InputMethod\\Settings\\CHS",
                SMTO_ABORTIFHUNG, 1000, out result);
        }

        internal static void BroadcastTabletTipSettings()
        {
            UIntPtr result;
            SendMessageTimeout(new IntPtr(0xFFFF), WM_SETTINGCHANGE, UIntPtr.Zero,
                "Software\\Microsoft\\TabletTip\\1.7",
                SMTO_ABORTIFHUNG, 1000, out result);
        }

        private static void Submit(List<INPUT> inputs)
        {
            if (inputs.Count == 0) return;
            uint expected = (uint)inputs.Count;
            uint sent = SendInput(expected, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            if (sent != expected)
                Diagnostics.Report("input.send", new Win32Exception(Marshal.GetLastWin32Error()));
        }

        private static INPUT KeyInput(ushort virtualKey, bool up)
        {
            var input = new INPUT();
            input.type = 1;
            input.data.keyboard.virtualKey = virtualKey;
            input.data.keyboard.flags = up ? KEYEVENTF_KEYUP : 0;
            return input;
        }

        private static INPUT UnicodeInput(char character, bool up)
        {
            var input = new INPUT();
            input.type = 1;
            input.data.keyboard.scanCode = character;
            input.data.keyboard.flags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0);
            return input;
        }
    }

    public static class KeyboardHost
    {
        [STAThread]
        public static void Main()
        {
            bool createdNew;
            using (var mutex = new System.Threading.Mutex(true,
                "Local\\Lucky.TouchInput.SingleInstance", out createdNew))
            {
                if (!createdNew) return;
                Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender,
                    System.Threading.ThreadExceptionEventArgs eventArgs)
                {
                    Diagnostics.Report("ui.unhandled", eventArgs.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender,
                    UnhandledExceptionEventArgs eventArgs)
                {
                    Exception exception = eventArgs.ExceptionObject as Exception;
                    if (exception != null) Diagnostics.Report("app.unhandled", exception);
                };
                Application.Run(new KeyboardForm());
            }
        }
    }
}
