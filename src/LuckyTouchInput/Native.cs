using System;
using Accessibility;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lucky.TouchInput
{
    internal struct SystemKeyboardState
    {
        internal bool TouchVisible, OskVisible;
        internal bool AnyVisible { get { return TouchVisible || OskVisible; } }
    }
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
    internal struct GUITHREADINFO
    {
        public int size;
        public uint flags;
        public IntPtr active, focus, capture, menuOwner, moveSize, caret;
        public RECT caretRect;
    }

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
        internal const int WM_SHOW_KEYBOARD = 0x8000 + 42;
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
        internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        internal static void ShowExistingKeyboard()
        {
            IntPtr hwnd = FindWindow(null, "Lucky Touch Input");
            if (hwnd != IntPtr.Zero)
                PostMessage(hwnd, WM_SHOW_KEYBOARD, IntPtr.Zero, IntPtr.Zero);
        }
        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute,
            out RECT rect, int size);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        private static extern int GetCloakedAttribute(IntPtr hwnd, uint attribute,
            out uint cloaked, int size);

        internal static bool GetVisibleWindowRect(IntPtr hwnd, out RECT rect)
        {
            return DwmGetWindowAttribute(hwnd, 9, out rect, Marshal.SizeOf<RECT>()) == 0 ||
                GetWindowRect(hwnd, out rect);
        }
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
                GetWindowRect(hwnd, out rect) && rect.Width > 0 && rect.Height > 0;
        }

        internal static string ProcessName(IntPtr hwnd)
        {
            uint processId;
            GetWindowThreadProcessId(hwnd, out processId);
            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                    return process.ProcessName;
            }
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
                "SearchHost", "LockApp", "LogonUI", "consent", "osk", "AtBroker",
                "TabTip", "TextInputHost", "WindowsInputExperience"
            };
            foreach (string name in excluded)
                if (String.Equals(process, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        internal static RECT InputPaneRect()
        {
            object pane = null;
            try
            {
                RECT rect;
                pane = new FrameworkInputPane();
                return ((IFrameworkInputPane)pane).Location(out rect) == 0 ? rect : new RECT();
            }
            // The input pane is not available on every Windows desktop surface.
            catch (Exception) { return new RECT(); }
            finally
            {
                if (pane != null && Marshal.IsComObject(pane)) Marshal.ReleaseComObject(pane);
            }
        }

        internal static SystemKeyboardState GetSystemKeyboardState(Rectangle workingArea)
        {
            var state = new SystemKeyboardState();
            VisitNativeKeyboards(workingArea, delegate(IntPtr window, bool osk) {
                if (osk) state.OskVisible = true;
                else state.TouchVisible = true;
            });
            return state;
        }

        private static void VisitNativeKeyboards(Rectangle workingArea, Action<IntPtr, bool> visit)
        {
            RECT paneRect = InputPaneRect();
            Rectangle pane = Rectangle.FromLTRB(paneRect.Left, paneRect.Top, paneRect.Right, paneRect.Bottom);
            EnumWindows(delegate(IntPtr window, IntPtr parameter) {
                if (!IsWindowVisible(window) || IsIconic(window)) return true;
                RECT rect;
                if (!GetWindowRect(window, out rect) || rect.Width <= 0 || rect.Height <= 0) return true;
                Rectangle bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                if (!workingArea.IntersectsWith(bounds)) return true;
                uint cloaked;
                if (GetCloakedAttribute(window, 14, out cloaked, sizeof(uint)) == 0 && cloaked != 0) return true;
                string process = ProcessName(window);
                bool osk = String.Equals(process, "osk", StringComparison.OrdinalIgnoreCase);
                if (osk || KeyboardInteropPolicy.IsConfirmedTouchWindow(process, WindowClass(window), bounds, pane))
                    visit(window, osk);
                return true;
            }, IntPtr.Zero);
        }

        internal static void HideSystemTouchKeyboard(IntPtr owner)
        {
            if (owner == IntPtr.Zero || !IsWindow(owner)) return;
            // Hide only confirmed touch panes on our monitor. Never toggle, kill OSK, or hide
            // a TextInputHost process merely because it also hosts candidates/emoji/voice UI.
            VisitNativeKeyboards(Screen.FromHandle(owner).WorkingArea, delegate(IntPtr window, bool osk) {
                if (!osk) ShowWindow(window, 0);
            });
        }

        internal static bool Key(ushort virtualKey, bool shifted, bool controlled = false)
        {
            bool sent = Submit(KeySequence(virtualKey, shifted, controlled));
            if (!sent && (shifted || controlled))
            {
                var release = new List<INPUT>();
                if (shifted) release.Add(KeyInput(0x10, true));
                if (controlled) release.Add(KeyInput(0x11, true));
                Submit(release);
            }
            return sent;
        }

        internal static List<INPUT> KeySequence(ushort virtualKey, bool shifted, bool controlled)
        {
            var inputs = new List<INPUT>();
            if (controlled) inputs.Add(KeyInput(0x11, false));
            if (shifted) inputs.Add(KeyInput(0x10, false));
            inputs.Add(KeyInput(virtualKey, false));
            inputs.Add(KeyInput(virtualKey, true));
            if (shifted) inputs.Add(KeyInput(0x10, true));
            if (controlled) inputs.Add(KeyInput(0x11, true));
            return inputs;
        }

        internal static bool Chord(ushort modifier, ushort virtualKey)
        {
            if (modifier == 0x11 || modifier == 0x10)
                return Key(virtualKey, modifier == 0x10, modifier == 0x11);
            var inputs = new List<INPUT> {
                KeyInput(modifier, false), KeyInput(virtualKey, false),
                KeyInput(virtualKey, true), KeyInput(modifier, true)
            };
            return Submit(inputs);
        }

        internal static bool Wheel(IntPtr target, int detents)
        {
            uint processId;
            uint threadId = GetWindowThreadProcessId(target, out processId);
            var info = new GUITHREADINFO { size = Marshal.SizeOf<GUITHREADINFO>() };
            IntPtr recipient = target;
            if (GetGUIThreadInfo(threadId, ref info) && info.focus != IntPtr.Zero &&
                GetAncestor(info.focus, 2) == target)
                recipient = info.focus;
            RECT rect;
            if (!GetWindowRect(recipient, out rect)) return false;
            int x = rect.Left + rect.Width / 2;
            int y = rect.Top + rect.Height / 2;
            // Address the focused control; the pointer is still over our wheel button.
            // WM_MOUSEWHEEL naturally propagates to its parent if not handled there.
            return PostMessage(recipient, 0x020A, new IntPtr(detents * 120 << 16),
                new IntPtr(unchecked((int)((uint)(ushort)x | (uint)(ushort)y << 16))));
        }

        internal static bool Text(string text)
        {
            if (String.IsNullOrEmpty(text)) return true;
            var inputs = new List<INPUT>();
            // Insert literal text. Synthesizing Enter/Tab here could submit a chat or
            // move focus to another field while inserting a multiline clipboard snippet.
            foreach (char character in text.Replace("\r\n", "\n").Replace('\r', '\n'))
            {
                inputs.Add(UnicodeInput(character, false));
                inputs.Add(UnicodeInput(character, true));
                if (inputs.Count >= 240)
                {
                    if (!Submit(inputs)) return false;
                    inputs.Clear();
                }
            }
            return Submit(inputs);
        }

        internal static void BroadcastPinyinSettings()
        {
            BroadcastSettings("Software\\Microsoft\\InputMethod\\Settings\\CHS");
        }

        internal static void BroadcastTabletTipSettings()
        {
            BroadcastSettings("Software\\Microsoft\\TabletTip\\1.7");
        }

        private static void BroadcastSettings(string path)
        {
            // An unrelated hung window must not freeze our settings buttons or shutdown.
            System.Threading.ThreadPool.QueueUserWorkItem(delegate {
                UIntPtr result;
                SendMessageTimeout(new IntPtr(0xFFFF), WM_SETTINGCHANGE, UIntPtr.Zero,
                    path, SMTO_ABORTIFHUNG, 100, out result);
            });
        }

        private static bool Submit(List<INPUT> inputs)
        {
            if (inputs.Count == 0) return true;
            uint expected = (uint)inputs.Count;
            uint sent = SendInput(expected, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            if (sent != expected)
                Diagnostics.Report("input.send", new Win32Exception(Marshal.GetLastWin32Error()));
            return sent == expected;
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
                if (!createdNew)
                {
                    Native.ShowExistingKeyboard();
                    return;
                }
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
