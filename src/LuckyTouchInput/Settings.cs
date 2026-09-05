using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Lucky.TouchInput
{
    internal enum PanelKind { Keys, Settings, Variables, Prompts }

    internal sealed class NativeKeyboardGuard : IDisposable
    {
        private const string RegistryPath = @"Software\Microsoft\TabletTip\1.7";
        private const string TapInvoke = "TouchKeyboardTapInvoke";
        private const string DesktopInvoke = "EnableDesktopModeAutoInvoke";
        private RegistrySnapshot tapSnapshot;
        private RegistrySnapshot desktopSnapshot;
        private readonly Native.WinEventDelegate winEventCallback;
        private readonly ManualResetEvent hookReady = new ManualResetEvent(false);
        private readonly Thread hookThread;
        private IntPtr foregroundHook;
        private IntPtr showHook;
        private uint hookThreadId;
        private volatile bool suppressing;
        private bool leaseSaved;
        private readonly bool recoverySucceeded;
        private bool disposed;

        internal NativeKeyboardGuard()
        {
            recoverySucceeded = RecoverStaleLease();
            winEventCallback = HandleWinEvent;
            hookThread = new Thread(RunWinEventLoop);
            hookThread.IsBackground = true;
            hookThread.Name = "LuckyTouchInput.NativeGuard";
            hookThread.SetApartmentState(ApartmentState.STA);
            hookThread.Start();
            hookReady.WaitOne(1000);
        }

        internal bool Apply(bool suppress)
        {
            if (disposed) return false;
            if (!suppress)
            {
                suppressing = false;
                RestoreOriginal();
                return true;
            }
            if (!recoverySucceeded) return false;
            if (suppressing) return true;
            try
            {
                if (!leaseSaved)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                    {
                        tapSnapshot = RegistrySnapshot.Capture(key, TapInvoke);
                        desktopSnapshot = RegistrySnapshot.Capture(key, DesktopInvoke);
                    }
                    Directory.CreateDirectory(UserPreferences.ConfigDirectory);
                    File.WriteAllLines(UserPreferences.NativeKeyboardLeasePath, new[] {
                        "SchemaVersion=1",
                        "TapInvoke=" + tapSnapshot.Serialize(),
                        "DesktopInvoke=" + desktopSnapshot.Serialize()
                    });
                    leaseSaved = true;
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key.SetValue(TapInvoke, 0, RegistryValueKind.DWord);
                    key.SetValue(DesktopInvoke, 0, RegistryValueKind.DWord);
                }
                Native.BroadcastTabletTipSettings();
                suppressing = true;
                return true;
            }
            catch (Exception exception)
            {
                Diagnostics.Report("native-keyboard.apply", exception);
                RestoreOriginal();
                return false;
            }
        }

        internal void RestoreOriginal()
        {
            // Coexistence must not touch settings changed by Windows or another application.
            if (!leaseSaved) return;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    tapSnapshot.Restore(key, TapInvoke);
                    desktopSnapshot.Restore(key, DesktopInvoke);
                }
                Native.BroadcastTabletTipSettings();
                if (File.Exists(UserPreferences.NativeKeyboardLeasePath))
                    File.Delete(UserPreferences.NativeKeyboardLeasePath);
                leaseSaved = false;
            }
            catch (Exception exception) { Diagnostics.Report("native-keyboard.restore", exception); }
        }

        public void Dispose()
        {
            if (disposed) return;
            suppressing = false;
            if (hookThreadId != 0)
                Native.PostThreadMessage(hookThreadId, Native.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
            if (hookThread != null && hookThread.IsAlive) hookThread.Join(1000);
            if (hookThread == null || !hookThread.IsAlive) hookReady.Dispose();
            RestoreOriginal();
            disposed = true;
        }

        private void RunWinEventLoop()
        {
            try
            {
                hookThreadId = Native.GetCurrentThreadId();
                foregroundHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND,
                    Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, winEventCallback, 0, 0,
                    Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
                showHook = Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW,
                    Native.EVENT_OBJECT_SHOW, IntPtr.Zero, winEventCallback, 0, 0,
                    Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
                hookReady.Set();
                Application.Run();
            }
            finally
            {
                if (foregroundHook != IntPtr.Zero) Native.UnhookWinEvent(foregroundHook);
                if (showHook != IntPtr.Zero) Native.UnhookWinEvent(showHook);
                foregroundHook = IntPtr.Zero;
                showHook = IntPtr.Zero;
                hookThreadId = 0;
                hookReady.Set();
            }
        }

        private void HandleWinEvent(IntPtr hook, uint eventType, IntPtr hwnd,
            int objectId, int childId, uint threadId, uint eventTime)
        {
            if (!suppressing || hwnd == IntPtr.Zero) return;
            try
            {
                string process = Native.ProcessName(hwnd);
                if (String.Equals(process, "TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(process, "TabTip", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(process, "WindowsInputExperience", StringComparison.OrdinalIgnoreCase))
                {
                    Native.HideSystemTouchKeyboard(hwnd);
                }
            }
            catch (Exception exception) { Diagnostics.Report("native-keyboard.event", exception); }
        }

        private static bool RecoverStaleLease()
        {
            string path = UserPreferences.NativeKeyboardLeasePath;
            if (!File.Exists(path)) return true;
            try
            {
                RegistrySnapshot tap = null;
                RegistrySnapshot desktop = null;
                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string[] parts = rawLine.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    if (parts[0] == "TapInvoke") tap = RegistrySnapshot.Parse(parts[1]);
                    else if (parts[0] == "DesktopInvoke")
                        desktop = RegistrySnapshot.Parse(parts[1]);
                }
                if (tap == null || desktop == null)
                    throw new InvalidDataException("Native keyboard lease is incomplete.");
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    tap.Restore(key, TapInvoke);
                    desktop.Restore(key, DesktopInvoke);
                }
                Native.BroadcastTabletTipSettings();
                File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                Diagnostics.Report("native-keyboard.recover", exception);
                return false;
            }
        }

        private sealed class RegistrySnapshot
        {
            private readonly bool existed;
            private readonly int value;

            private RegistrySnapshot(bool existed, int value)
            {
                this.existed = existed;
                this.value = value;
            }

            internal static RegistrySnapshot Capture(RegistryKey key, string name)
            {
                if (key == null) return new RegistrySnapshot(false, 0);
                bool exists = false;
                foreach (string candidate in key.GetValueNames())
                    if (String.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                return exists
                    ? new RegistrySnapshot(true, Convert.ToInt32(
                        key.GetValue(name, 0), CultureInfo.InvariantCulture))
                    : new RegistrySnapshot(false, 0);
            }

            internal void Restore(RegistryKey key, string name)
            {
                if (existed) key.SetValue(name, value, RegistryValueKind.DWord);
                else key.DeleteValue(name, false);
            }

            internal string Serialize()
            {
                return (existed ? "1" : "0") + "," +
                    value.ToString(CultureInfo.InvariantCulture);
            }

            internal static RegistrySnapshot Parse(string text)
            {
                string[] parts = text.Split(',');
                int parsed;
                if (parts.Length != 2 || (parts[0] != "0" && parts[0] != "1") ||
                    !Int32.TryParse(parts[1], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out parsed))
                    throw new InvalidDataException("Native keyboard lease value is invalid.");
                return new RegistrySnapshot(parts[0] == "1", parsed);
            }
        }
    }

    internal static class StartupSettings
    {
        private const string RegistryPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "LuckyTouchInput";

        internal static bool IsEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                        RegistryPath, false))
                    {
                        string command = key == null ? null : key.GetValue(ValueName) as string;
                        return !String.IsNullOrWhiteSpace(command) &&
                            command.IndexOf(Application.ExecutablePath,
                                StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }
                catch (Exception exception)
                {
                    Diagnostics.Report("startup.read", exception);
                    return false;
                }
            }
        }

        internal static string Label
        {
            get { return IsEnabled ? "启动：开" : "启动：关"; }
        }

        internal static bool Toggle(out string message)
        {
            bool enable = !IsEnabled;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (enable)
                        key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"",
                            RegistryValueKind.String);
                    else
                        key.DeleteValue(ValueName, false);
                }
                message = enable ? "已启用登录后启动" : "已关闭登录后启动";
                return true;
            }
            catch (Exception exception)
            {
                Diagnostics.Report("startup.toggle", exception);
                message = "启动设置失败：" + exception.Message;
                return false;
            }
        }
    }

    internal static class MicrosoftPinyinSettings
    {
        private const string RegistryPath = @"Software\Microsoft\InputMethod\Settings\CHS";
        private const string FlypyMapping = "小鹤双拼*2*^*iuvdjhcwfg^xmlnpbksqszxkrltvyovt";

        internal static bool IsDoublePinyin
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                        return key != null && Convert.ToInt32(
                            key.GetValue("Enable Double Pinyin", 0), CultureInfo.InvariantCulture) == 1;
                }
                catch (Exception exception)
                {
                    Diagnostics.Report("microsoft-pinyin.read", exception);
                    return false;
                }
            }
        }

        internal static string Label
        {
            get { return IsDoublePinyin ? "双拼" : "全拼"; }
        }

        internal static bool Toggle(out string message)
        {
            bool enable = !IsDoublePinyin;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (key == null)
                    {
                        message = "未找到微软拼音设置";
                        return false;
                    }
                    if (enable)
                    {
                        object mapping = key.GetValue("UserDefinedDoublePinyinScheme0");
                        if (mapping != null && !String.IsNullOrWhiteSpace(mapping.ToString()) &&
                            !String.Equals(mapping.ToString(), FlypyMapping, StringComparison.Ordinal))
                        {
                            message = "已有自定义双拼，请在拼音设置中选择方案";
                            return false;
                        }
                        if (mapping == null || String.IsNullOrWhiteSpace(mapping.ToString()))
                            key.SetValue("UserDefinedDoublePinyinScheme0", FlypyMapping,
                                RegistryValueKind.String);
                        key.SetValue("DoublePinyinScheme", 10, RegistryValueKind.DWord);
                    }
                    key.SetValue("Enable Double Pinyin", enable ? 1 : 0,
                        RegistryValueKind.DWord);
                }
                Native.BroadcastPinyinSettings();
                message = "已设为" + (enable ? "小鹤双拼" : "全拼") + "，切换一次输入法后生效";
                return true;
            }
            catch (Exception exception)
            {
                Diagnostics.Report("microsoft-pinyin.toggle", exception);
                message = "切换失败：" + exception.Message;
                return false;
            }
        }
    }

    internal sealed class RoundedButton : Button
    {
        private Font fittedFont;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Color NormalColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Color PressedColor { get; set; }

        internal RoundedButton()
        {
            NormalColor = Color.White;
            PressedColor = Color.FromArgb(220, 232, 246);
            BackColor = NormalColor;
            ForeColor = Color.FromArgb(32, 32, 32);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TabStop = false;
            UseVisualStyleBackColor = false;
            Margin = new Padding(6);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            FitLabel();
            int edge = Math.Min(Width, Height);
            if (edge < 2) { Region = null; return; }
            int radius = Math.Min(edge / 2, Math.Min(18, Math.Max(6, edge / 5)));
            using (var path = new GraphicsPath())
            {
                int diameter = radius * 2;
                path.AddArc(0, 0, diameter, diameter, 180, 90);
                path.AddArc(Width - diameter, 0, diameter, diameter, 270, 90);
                path.AddArc(Width - diameter, Height - diameter, diameter, diameter, 0, 90);
                path.AddArc(0, Height - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                Region = new Region(path);
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            FitLabel();
        }

        private void FitLabel()
        {
            if (Width < 8 || Height < 8) return;
            bool key = Tag is ushort;
            float maximum = (key ? 48f : 16f) * DeviceDpi / 96f;
            float heightLimit = Math.Max(8f, (Height - 4) * (key ? 0.60f : 0.42f));
            float widthLimit = Math.Max(8f, (Width - 8f) / Math.Max(1, Text.Length));
            float pixels = Math.Min(maximum, Math.Min(heightLimit, widthLimit));
            if (fittedFont != null && Math.Abs(fittedFont.Size - pixels) < 0.5f) return;
            Font old = fittedFont;
            fittedFont = new Font("Microsoft YaHei UI", pixels, FontStyle.Regular, GraphicsUnit.Pixel);
            Font = fittedFont;
            if (old != null) old.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && fittedFont != null) { fittedFont.Dispose(); fittedFont = null; }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            BackColor = PressedColor;
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            ResetVisual();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ResetVisual();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            ResetVisual();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            ResetVisual();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            ResetVisual();
        }

        private void ResetVisual()
        {
            if (BackColor == NormalColor) return;
            BackColor = NormalColor;
            Invalidate();
        }
    }
}
