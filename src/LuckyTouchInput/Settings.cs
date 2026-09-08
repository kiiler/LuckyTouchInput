using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Lucky.TouchInput
{
    internal enum PanelKind { Keys, Settings, Variables, Prompts, Tools, Edit }


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
        private bool largeLabels;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal bool LargeLabels
        {
            get { return largeLabels; }
            set { largeLabels = value; FitLabel(); }
        }
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
            AutoEllipsis = true;
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
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null) oldRegion.Dispose();
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
            float maximum = (key && Text.Length == 1 ? 48f : key || largeLabels ? 32f : 16f) * DeviceDpi / 96f;
            float heightLimit = Math.Max(8f, (Height - 4) * (key ? 0.60f : largeLabels ? 0.58f : 0.42f));
            float pixels = Math.Min(maximum, heightLimit);
            int availableWidth = Math.Max(1, Width - Padding.Horizontal - 10);
            int availableHeight = Math.Max(1, Height - Padding.Vertical - 6);
            Font candidate;
            // Include GDI text padding and actual CJK metrics. Character counts caused
            // Backspace/Enter to wrap on portrait displays and made Tab unnecessarily tiny.
            while (true)
            {
                candidate = new Font("Microsoft YaHei UI", pixels, FontStyle.Regular, GraphicsUnit.Pixel);
                Size measured = TextRenderer.MeasureText(Text, candidate, Size.Empty, TextFormatFlags.SingleLine);
                if (pixels <= 8f || measured.Width <= availableWidth && measured.Height <= availableHeight) break;
                float ratio = Math.Min((float)availableWidth / Math.Max(1, measured.Width),
                    (float)availableHeight / Math.Max(1, measured.Height));
                pixels = Math.Max(8f, Math.Min(pixels - 1f, pixels * ratio));
                candidate.Dispose();
            }
            if (fittedFont != null && Math.Abs(fittedFont.Size - pixels) < 0.1f)
            {
                candidate.Dispose();
                return;
            }
            Font old = fittedFont;
            fittedFont = candidate;
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
            if (IsDisposed || Disposing) return;
            if (BackColor == NormalColor) return;
            BackColor = NormalColor;
            Invalidate();
        }
    }
}
