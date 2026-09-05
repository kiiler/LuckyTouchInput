using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lucky.TouchInput
{
    internal sealed class KeyboardForm : Form
    {
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;
        private const int CenterGapPixels = 8;
        private static readonly TimeSpan ForegroundGrace = TimeSpan.FromSeconds(2);
        private readonly Timer monitor;
        private readonly UserPreferences preferences;
        private readonly NativeKeyboardGuard nativeKeyboardGuard;
        private readonly string[] sessionVariables = new string[3];
        private TableLayoutPanel root;
        private Control triggerPanel;
        private Label title;
        private RoundedButton modeButton;
        private RoundedButton toolsButton;
        private RoundedButton variablesButton;
        private RoundedButton promptsButton;
        private RoundedButton settingsButton;
        private RoundedButton shiftButton;
        private RoundedButton rightShiftButton;
        private Control auxiliaryRow;
        private PanelKind panel = PanelKind.Keys;
        private IntPtr targetWindow;
        private IntPtr savedWindow;
        private WINDOWPLACEMENT savedPlacement;
        private bool placementSaved;
        private bool shiftNext;
        private bool collapsed;
        private bool advancedSettings;
        private DateTime lastPinnedForegroundUtc = DateTime.MinValue;
        private DateTime collapseConfirmUntilUtc = DateTime.MinValue;
        private string statusMessage = String.Empty;
        private DateTime statusUntil = DateTime.MinValue;
        private NotifyIcon trayIcon;
        private TableLayoutPanel toolbar;

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return parameters;
            }
        }

        internal KeyboardForm() : this(UserPreferences.Load(), true) { }

        // The non-running form also permits real control-layout checks without input hooks,
        // user settings writes, tray icons, or changing another application's window.
        internal KeyboardForm(UserPreferences settings, bool startRuntime)
        {
            preferences = settings;
            nativeKeyboardGuard = startRuntime ? new NativeKeyboardGuard() : null;
            if (nativeKeyboardGuard != null && !nativeKeyboardGuard.Apply(preferences.SuppressNativeKeyboard))
                preferences.SuppressNativeKeyboard = false;
            Text = "Lucky Touch Input";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(238, 239, 242);
            AutoScaleMode = AutoScaleMode.None;
            BuildLayout();
            if (startRuntime) BuildTrayIcon();

            monitor = new Timer();
            monitor.Interval = 125;
            monitor.Tick += delegate { RefreshState(); };
            if (startRuntime) Shown += delegate { RefreshState(); monitor.Start(); };
            FormClosing += delegate {
                monitor.Stop();
                RestoreTargetWindow();
                if (nativeKeyboardGuard != null) nativeKeyboardGuard.Dispose();
                if (trayIcon != null) trayIcon.Dispose();
            };
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == Native.WM_SHOW_KEYBOARD)
            {
                ShowKeyboard();
                return;
            }
            if (message.Msg == WM_MOUSEACTIVATE)
            {
                message.Result = new IntPtr(MA_NOACTIVATE);
                return;
            }
            base.WndProc(ref message);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (monitor != null) monitor.Dispose();
                if (nativeKeyboardGuard != null) nativeKeyboardGuard.Dispose();
                if (trayIcon != null) trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        private void BuildLayout()
        {
            root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = BackColor;
            root.Padding = new Padding(8, 6, 8, 8);
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            for (int row = 1; row < 6; row++)
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            Controls.Add(root);

            var trigger = new Panel();
            trigger.Dock = DockStyle.Fill;
            trigger.Padding = new Padding(8);
            trigger.BackColor = BackColor;
            var triggerButton = ActionButton("⌨  打开键盘", delegate { ExpandKeyboard(); });
            triggerButton.Dock = DockStyle.Fill;
            trigger.Controls.Add(triggerButton);
            trigger.Visible = false;
            triggerPanel = trigger;
            Controls.Add(triggerPanel);
            root.BringToFront();

            toolbar = new TableLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.ColumnCount = 8;
            toolbar.RowCount = 1;
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            title = new Label();
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Padding = new Padding(18, 0, 0, 0);
            title.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Regular);
            title.ForeColor = Color.FromArgb(75, 75, 75);
            toolbar.Controls.Add(title, 0, 0);
            modeButton = ActionButton(String.Empty, delegate { CycleMode(); });
            toolsButton = ActionButton("工具", delegate {
                TogglePanel(panel != PanelKind.Keys && panel != PanelKind.Settings ? panel : PanelKind.Tools);
            });
            variablesButton = ActionButton("变量", delegate { TogglePanel(PanelKind.Variables); });
            promptsButton = ActionButton("提示", delegate { TogglePanel(PanelKind.Prompts); });
            settingsButton = ActionButton("设置", delegate { TogglePanel(PanelKind.Settings); });
            toolbar.Controls.Add(modeButton, 1, 0);
            toolbar.Controls.Add(toolsButton, 2, 0);
            toolbar.Controls.Add(variablesButton, 3, 0);
            toolbar.Controls.Add(promptsButton, 4, 0);
            toolbar.Controls.Add(settingsButton, 5, 0);
            toolbar.Controls.Add(ActionButton("—", delegate { RequestCollapse(); }), 6, 0);
            toolbar.Controls.Add(ActionButton("×", delegate { Close(); }), 7, 0);
            root.Controls.Add(toolbar, 0, 0);

            auxiliaryRow = BuildNumberRow();
            root.Controls.Add(auxiliaryRow, 0, 1);
            root.Controls.Add(KeyRow(new[] {
                K("q", 0x51), K("w", 0x57), K("e", 0x45), K("r", 0x52), K("t", 0x54),
                K("y", 0x59), K("u", 0x55), K("i", 0x49), K("o", 0x4F), K("p", 0x50)
            }), 0, 2);
            root.Controls.Add(KeyRow(new[] {
                Spacer(0.5f), K("a", 0x41), K("s", 0x53), K("d", 0x44), K("f", 0x46),
                K("g", 0x47), K("h", 0x48), K("j", 0x4A), K("k", 0x4B),
                K("l", 0x4C), Spacer(0.5f)
            }), 0, 3);
            shiftButton = ShiftKey();
            rightShiftButton = ShiftKey();
            root.Controls.Add(KeyRow(new[] {
                shiftButton, K("z", 0x5A), K("x", 0x58), K("c", 0x43),
                K("v", 0x56), K("b", 0x42), K("n", 0x4E), K("m", 0x4D),
                rightShiftButton
            }), 0, 4);

            var actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.RowCount = 1;
            actions.ColumnCount = 5;
            actions.Margin = new Padding(0);
            float[] actionWeights = { 1.25f, 1f, 5f, 1f, 1.75f };
            foreach (float weight in actionWeights)
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, weight * 10f));
            actions.Controls.Add(ActionButton("中 / 英", delegate { SendKey(0x10); }), 0, 0);
            actions.Controls.Add(K(",", 0xBC), 1, 0);
            actions.Controls.Add(K("空格", 0x20), 2, 0);
            actions.Controls.Add(K("。", 0xBE), 3, 0);
            var enter = K("回车", 0x0D);
            enter.NormalColor = Color.FromArgb(0, 103, 192);
            enter.PressedColor = Color.FromArgb(0, 83, 160);
            enter.BackColor = enter.NormalColor;
            enter.ForeColor = Color.White;
            actions.Controls.Add(enter, 4, 0);
            root.Controls.Add(actions, 0, 5);
            UpdateToolbar();
        }

        private void BuildTrayIcon()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示键盘", null, delegate { ShowKeyboard(); });
            menu.Items.Add("收起键盘", null, delegate { CollapseKeyboard(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Close(); });

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Lucky Touch Input";
            trayIcon.Icon = Icon;
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { ShowKeyboard(); };
        }

        private void ShowKeyboard()
        {
            if (preferences.Mode == KeyboardMode.PinnedApplication &&
                Native.FindProcessWindow(preferences.PinnedProcessName) == IntPtr.Zero)
            {
                preferences.Mode = KeyboardMode.FollowForeground;
                preferences.Save();
                SetStatus("固定应用已关闭，已恢复跟随前台");
            }
            if (collapsed) ExpandKeyboard();
            RefreshState();
            if (!Visible) Show();
        }

        private Control BuildNumberRow()
        {
            return KeyRow(new[] {
                K("1", 0x31), K("2", 0x32), K("3", 0x33), K("4", 0x34), K("5", 0x35),
                K("6", 0x36), K("7", 0x37), K("8", 0x38), K("9", 0x39), K("0", 0x30),
                K("退格", 0x08, 1.25f)
            });
        }

        private Control BuildSettingsRow()
        {
            if (advancedSettings)
                return KeyRow(new[] {
                    PanelButton("← 常用", delegate { ShowSettingsPage(false); }),
                    PanelButton("输入法", delegate { SendChord(0x5B, 0x20); }),
                    PanelButton("拼音设置", delegate { OpenWindowsInputSettings(); }),
                    PanelButton("触键设置", delegate { OpenTouchKeyboardSettings(); }),
                    PanelButton("Rime 菜单", delegate { SendChord(0x11, 0xC0); }),
                    PanelButton("立即收起", delegate { CollapseKeyboard(); }),
                    PanelButton("重置布局", delegate { ResetLayout(); }),
                    PanelButton("左缝 −", delegate { AdjustGap(-12); }),
                    PanelButton("左缝 +", delegate { AdjustGap(12); })
                });
            return KeyRow(new[] {
                PanelButton(ModeLabel(), delegate { CycleMode(); }),
                PanelButton(EnterPolicyLabel(), delegate { ToggleEnterPolicy(); }),
                PanelButton(NativePolicyLabel(), delegate { ToggleNativePolicy(); }),
                PanelButton(StartupSettings.Label, delegate { ToggleStartup(); }),
                PanelButton("高度 −", delegate { AdjustHeight(-4); }),
                PanelButton("高度 +", delegate { AdjustHeight(4); }),
                PanelButton(preferences.LargeKeys ? "布局：手机大键" : "布局：标准", delegate { ToggleLargeKeys(); }),
                PanelButton("全 / 双", delegate { ToggleDoublePinyin(); }),
                PanelButton("更多", delegate { ShowSettingsPage(true); })
            });
        }

        private Control BuildVariablesRow()
        {
            return KeyRow(new[] {
                PanelButton("存 1", delegate { SaveVariable(0); }),
                PanelButton("贴 1", delegate { InsertVariable(0); }),
                PanelButton("存 2", delegate { SaveVariable(1); }),
                PanelButton("贴 2", delegate { InsertVariable(1); }),
                PanelButton("存 3", delegate { SaveVariable(2); }),
                PanelButton("贴 3", delegate { InsertVariable(2); }),
                PanelButton("剪贴板", delegate { InsertClipboard(); }),
                PanelButton("日期", delegate { InsertText(DateTime.Now.ToString("yyyy-MM-dd")); }),
                PanelButton("时间", delegate { InsertText(DateTime.Now.ToString("HH:mm")); }),
                PanelButton("清空", delegate { ClearVariables(); })
            });
        }

        private Control BuildToolsRow()
        {
            return KeyRow(new[] {
                K("候选‹", 0x21), K("候选›", 0x22),
                PanelButton("滚↑", delegate { ScrollTarget(3); }),
                PanelButton("滚↓", delegate { ScrollTarget(-3); }),
                K("←", 0x25), K("→", 0x27),
                PanelButton("变量", delegate { TogglePanel(PanelKind.Variables); }),
                PanelButton("提示", delegate { TogglePanel(PanelKind.Prompts); })
            });
        }

        private Control BuildPromptsRow()
        {
            return KeyRow(new[] {
                PromptButton("继续", "继续。"),
                PromptButton("纠错", "请纠正上面的错别字和语病，保留原意。"),
                PromptButton("润色", "请把上面的内容润色得更自然，保留原意。"),
                PromptButton("总结", "请把上面的内容总结为简洁要点。"),
                PromptButton("解释", "请解释上面的内容，先给结论。"),
                PromptButton("列步骤", "请把任务拆成可执行步骤。"),
                PromptButton("查遗漏", "请检查当前方案的风险、遗漏和可验证点。"),
                PromptButton("直接执行", "请直接开始执行，并在完成后进行实际验收。"),
                PanelButton("撤销", delegate { SendChord(0x11, 0x5A); }),
                PanelButton("重做", delegate { SendChord(0x11, 0x59); })
            });
        }

        private RoundedButton PromptButton(string label, string prompt)
        {
            var button = PanelButton(label, delegate { InsertText(prompt); });
            button.NormalColor = Color.FromArgb(244, 238, 252);
            button.PressedColor = Color.FromArgb(224, 210, 244);
            button.BackColor = button.NormalColor;
            return button;
        }

        private RoundedButton K(string label, ushort virtualKey, float weight = 1f)
        {
            var button = new RoundedButton();
            button.Text = label;
            button.Tag = virtualKey;
            button.LargeLabels = preferences.LargeKeys;
            if (preferences.LargeKeys) button.Margin = new Padding(4);
            button.AccessibleDescription = weight.ToString(CultureInfo.InvariantCulture);
            button.Dock = DockStyle.Fill;
            button.Click += SendKeyClick;
            return button;
        }

        private RoundedButton ShiftKey()
        {
            var button = K("⇧", 0x10, 1.25f);
            button.Click -= SendKeyClick;
            button.Click += delegate {
                shiftNext = !shiftNext;
                UpdateShiftButtons();
            };
            return button;
        }

        private RoundedButton Spacer(float weight)
        {
            var spacer = new RoundedButton();
            spacer.Text = String.Empty;
            spacer.Tag = null;
            spacer.AccessibleDescription = weight.ToString(CultureInfo.InvariantCulture);
            spacer.Visible = false;
            return spacer;
        }

        private RoundedButton ActionButton(string label, EventHandler click)
        {
            var button = new RoundedButton();
            button.Text = label;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(5, 8, 5, 8);
            button.LargeLabels = preferences.LargeKeys;
            if (preferences.LargeKeys) button.Margin = new Padding(4);
            button.Click += click;
            return button;
        }

        private RoundedButton PanelButton(string label, EventHandler click)
        {
            var button = new RoundedButton();
            button.Text = label;
            button.Tag = null;
            button.LargeLabels = preferences.LargeKeys;
            if (preferences.LargeKeys) button.Margin = new Padding(4);
            button.AccessibleDescription = "1";
            button.Dock = DockStyle.Fill;
            button.Click += click;
            return button;
        }

        private TableLayoutPanel KeyRow(RoundedButton[] buttons)
        {
            var row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.RowCount = 1;
            row.Margin = new Padding(0);
            bool centerGap = buttons.Length % 2 == 0;
            int splitIndex = centerGap ? buttons.Length / 2 : -1;
            row.ColumnCount = buttons.Length + (centerGap ? 1 : 0);
            float total = 0f;
            foreach (RoundedButton button in buttons)
                total += Single.Parse(button.AccessibleDescription, CultureInfo.InvariantCulture);
            int column = 0;
            for (int index = 0; index < buttons.Length; index++)
            {
                if (index == splitIndex)
                {
                    row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, CenterGapPixels));
                    column++;
                }
                float weight = Single.Parse(buttons[index].AccessibleDescription,
                    CultureInfo.InvariantCulture);
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, weight / total * 100f));
                row.Controls.Add(buttons[index], column++, 0);
            }
            return row;
        }

        private void SendKeyClick(object sender, EventArgs eventArgs)
        {
            var button = sender as RoundedButton;
            if (button == null || button.Tag == null) return;
            ushort virtualKey = (ushort)button.Tag;
            bool shifted = shiftNext && (virtualKey >= 0x30 && virtualKey <= 0x5A ||
                virtualKey == 0xBC || virtualKey == 0xBE);
            if (!FocusTargetWindow()) return;
            bool sent = ReportInputResult(Native.Key(virtualKey, shifted));
            if (sent && virtualKey == 0x0D && preferences.CollapseAfterEnter)
                BeginInvoke((Action)CollapseKeyboard);
            if (shiftNext)
            {
                shiftNext = false;
                UpdateShiftButtons();
            }
        }

        private void UpdateShiftButtons()
        {
            Color color = shiftNext ? Color.FromArgb(204, 228, 252) : Color.White;
            if (shiftButton != null)
            {
                shiftButton.NormalColor = color;
                shiftButton.BackColor = color;
            }
            if (rightShiftButton != null)
            {
                rightShiftButton.NormalColor = color;
                rightShiftButton.BackColor = color;
            }
        }

        private void SendKey(ushort virtualKey)
        {
            if (FocusTargetWindow()) ReportInputResult(Native.Key(virtualKey, false));
        }

        private void ScrollTarget(int detents)
        {
            if (FocusTargetWindow()) ReportInputResult(Native.Wheel(targetWindow, detents));
        }

        private void SendChord(ushort modifier, ushort virtualKey)
        {
            if (FocusTargetWindow()) ReportInputResult(Native.Chord(modifier, virtualKey));
        }

        private void InsertText(string text)
        {
            if (String.IsNullOrEmpty(text)) return;
            if (FocusTargetWindow() && ReportInputResult(Native.Text(text)))
                SetStatus("已插入，可检查后再回车");
        }

        private bool ReportInputResult(bool succeeded)
        {
            if (!succeeded) SetStatus("输入未送达：请点选普通权限的输入框");
            return succeeded;
        }

        private bool FocusTargetWindow()
        {
            if (targetWindow == IntPtr.Zero || !Native.IsWindow(targetWindow))
                targetWindow = ResolveTargetWindow();
            if (targetWindow == IntPtr.Zero || Native.IsSystemSurface(targetWindow))
                return ReportInputResult(false);
            Native.SetForegroundWindow(targetWindow);
            if (Native.GetForegroundWindow() != targetWindow)
                return ReportInputResult(false);
            Native.RefocusCaret(targetWindow);
            return true;
        }

        private void RefreshState()
        {
            if (preferences.SuppressNativeKeyboard)
                Native.HideSystemTouchKeyboard(Handle);
            IntPtr resolved = ResolveTargetWindow();
            if (resolved == IntPtr.Zero || Native.IsIconic(resolved))
            {
                if (Visible) Hide();
                RestoreTargetWindow();
                targetWindow = IntPtr.Zero;
                return;
            }

            if (targetWindow != resolved)
            {
                RestoreTargetWindow();
                targetWindow = resolved;
            }
            PositionKeyboard();
            if (!Visible) Show();
            if (!collapsed && preferences.Mode != KeyboardMode.Floating)
                ForceTargetWindow();
            UpdateToolbar();
        }

        private IntPtr ResolveTargetWindow()
        {
            IntPtr foreground = Native.GetForegroundWindow();
            string process = Native.ProcessName(foreground);

            if (preferences.Mode == KeyboardMode.PinnedApplication)
            {
                // Prefer our existing target after resizing it below typical desktop sizes.
                IntPtr pinnedWindow = targetWindow != IntPtr.Zero && Native.IsWindowVisible(targetWindow) &&
                    String.Equals(Native.ProcessName(targetWindow), preferences.PinnedProcessName,
                        StringComparison.OrdinalIgnoreCase)
                    ? targetWindow : Native.FindProcessWindow(preferences.PinnedProcessName);
                if (pinnedWindow == IntPtr.Zero) return IntPtr.Zero;
                if (String.Equals(process, preferences.PinnedProcessName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    lastPinnedForegroundUtc = DateTime.UtcNow;
                    return pinnedWindow;
                }

                bool transientSurface = foreground == Handle ||
                    String.Equals(process, "TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(process, "Wacom_Tablet", StringComparison.OrdinalIgnoreCase) ||
                    Native.IsSystemSurface(foreground);
                if (transientSurface || DateTime.UtcNow - lastPinnedForegroundUtc <= ForegroundGrace)
                    return targetWindow != IntPtr.Zero && Native.IsWindow(targetWindow)
                        ? targetWindow : pinnedWindow;
                return IntPtr.Zero;
            }

            if (String.Equals(process, "TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(process, "Wacom_Tablet", StringComparison.OrdinalIgnoreCase) ||
                Native.IsSystemSurface(foreground))
                return targetWindow != IntPtr.Zero && Native.IsWindow(targetWindow)
                    ? targetWindow : IntPtr.Zero;

            if (foreground == IntPtr.Zero || !Native.IsWindowVisible(foreground) ||
                Native.IsSystemSurface(foreground))
                return IntPtr.Zero;
            return foreground;
        }

        private void PositionKeyboard()
        {
            Screen screen = Screen.FromHandle(targetWindow);
            Rectangle work = screen.WorkingArea;
            if (collapsed)
            {
                int triggerWidth = Math.Min(440, Math.Max(300, work.Width / 9));
                int triggerHeight = Math.Min(120, Math.Max(88, work.Height / 20));
                Rectangle triggerBounds = new Rectangle(work.Right - triggerWidth,
                    work.Bottom - triggerHeight, triggerWidth, triggerHeight);
                if (Bounds != triggerBounds) Bounds = triggerBounds;
                return;
            }
            Rectangle desired = LayoutPolicy.KeyboardBounds(work, preferences.Mode,
                preferences.HeightPercent, preferences.LeftGap, preferences.LargeKeys);
            ApplyKeyboardBounds(desired);
        }

        internal void ApplyKeyboardBounds(Rectangle desired)
        {
            int height = desired.Height;
            int toolbarHeight = preferences.LargeKeys
                ? Math.Clamp(height / 10, 56, 104)
                : Math.Min(104, Math.Max(70, height / 9));
            if (root != null && root.RowStyles.Count > 0 &&
                Math.Abs(root.RowStyles[0].Height - toolbarHeight) > 0.5f)
                root.RowStyles[0].Height = toolbarHeight;
            if (Bounds != desired) Bounds = desired;
            AdaptToolbar(desired.Width);
        }

        private void AdaptToolbar(int width)
        {
            // The original fixed 922 px of buttons left no room for status on smaller hosts.
            int unit = preferences.LargeKeys
                ? Math.Clamp((width - 32) / 9, 40, 220)
                : Math.Clamp((width - 160) / 12, 40, 82);
            int[] widths = preferences.LargeKeys
                ? new[] { 0, unit * 2, unit, 0, 0, unit, unit, unit }
                : new[] { 0, unit * 2, unit * 2, unit, unit, unit, unit, unit };
            variablesButton.Visible = !preferences.LargeKeys;
            promptsButton.Visible = !preferences.LargeKeys;
            for (int index = 1; index < widths.Length; index++)
                if (Math.Abs(toolbar.ColumnStyles[index].Width - widths[index]) > 0.5f)
                    toolbar.ColumnStyles[index].Width = widths[index];
            title.AutoEllipsis = true;
        }

        private void ForceTargetWindow()
        {
            if (!placementSaved || savedWindow != targetWindow)
            {
                savedPlacement = new WINDOWPLACEMENT();
                savedPlacement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                if (!Native.GetWindowPlacement(targetWindow, ref savedPlacement)) return;
                savedWindow = targetWindow;
                placementSaved = true;
            }

            Screen screen = Screen.FromHandle(targetWindow);
            Rectangle bounds = screen.WorkingArea;
            // Keep a very small visual seam without covering the composer.
            int targetHeight = Math.Min(bounds.Height, Top - bounds.Top - 3);
            RECT current;
            RECT visible;
            if (!Native.GetWindowRect(targetWindow, out current)) return;
            Native.GetVisibleWindowRect(targetWindow, out visible);
            int leftInset = Math.Clamp(visible.Left - current.Left, 0, 32);
            int topInset = Math.Clamp(visible.Top - current.Top, 0, 32);
            int rightInset = Math.Clamp(current.Right - visible.Right, 0, 32);
            int bottomInset = Math.Clamp(current.Bottom - visible.Bottom, 0, 32);
            int x = bounds.Left - leftInset;
            int y = bounds.Top - topInset;
            int width = bounds.Width + leftInset + rightInset;
            int height = targetHeight + topInset + bottomInset;
            if (current.Left == x && current.Top == y && current.Width == width && current.Height == height)
                return;
            Native.SetWindowPos(targetWindow, IntPtr.Zero, x, y, width, height,
                Native.SWP_KEEP_MAXIMIZED_NO_CLAMP);
        }

        private void RestoreTargetWindow()
        {
            if (placementSaved && savedWindow != IntPtr.Zero && Native.IsWindow(savedWindow))
            {
                // Some desktop frameworks report SW_HIDE for an otherwise visible window.
                // Never let a geometry restore turn an interactive target into a hidden window.
                bool minimized = Native.IsIconic(savedWindow);
                savedPlacement.showCmd = minimized ? 2 : savedPlacement.showCmd == 3 ? 3 : 1;
                Native.SetWindowPlacement(savedWindow, ref savedPlacement);
                if (!minimized)
                    Native.ShowWindow(savedWindow, savedPlacement.showCmd == 3 ? 3 : 5);
            }
            savedWindow = IntPtr.Zero;
            placementSaved = false;
        }

        internal void TogglePanel(PanelKind requested)
        {
            if (requested == PanelKind.Settings && panel != PanelKind.Settings)
                advancedSettings = false;
            panel = panel == requested ? PanelKind.Keys : requested;
            ReplaceAuxiliaryRow();
            UpdateToolbar();
        }

        private void ReplaceAuxiliaryRow()
        {
            if (auxiliaryRow != null)
            {
                root.Controls.Remove(auxiliaryRow);
                auxiliaryRow.Dispose();
            }
            if (panel == PanelKind.Settings) auxiliaryRow = BuildSettingsRow();
            else if (panel == PanelKind.Variables) auxiliaryRow = BuildVariablesRow();
            else if (panel == PanelKind.Prompts) auxiliaryRow = BuildPromptsRow();
            else if (panel == PanelKind.Tools) auxiliaryRow = BuildToolsRow();
            else auxiliaryRow = BuildNumberRow();
            root.Controls.Add(auxiliaryRow, 0, 1);
        }

        private void CycleMode()
        {
            RestoreTargetWindow();
            if (preferences.Mode == KeyboardMode.FollowForeground)
            {
                string processName = Native.ProcessName(targetWindow);
                if (String.IsNullOrWhiteSpace(processName))
                    processName = Native.ProcessName(Native.GetForegroundWindow());
                if (String.IsNullOrWhiteSpace(processName) ||
                    String.Equals(processName, "LuckyTouchInput",
                        StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus("请先点选要固定的应用");
                    return;
                }
                preferences.PinnedProcessName = processName;
                preferences.Mode = KeyboardMode.PinnedApplication;
            }
            else if (preferences.Mode == KeyboardMode.PinnedApplication)
                preferences.Mode = KeyboardMode.Floating;
            else
                preferences.Mode = KeyboardMode.FollowForeground;
            preferences.Save();
            SetStatus(preferences.Mode == KeyboardMode.PinnedApplication &&
                !String.IsNullOrWhiteSpace(preferences.PinnedProcessName)
                ? "已固定到 " + preferences.PinnedProcessName
                : "已切换为" + ModeLabel());
            UpdateToolbar();
            RefreshState();
        }

        private string ModeLabel()
        {
            if (preferences.Mode == KeyboardMode.PinnedApplication) return "固定应用";
            if (preferences.Mode == KeyboardMode.Floating) return "自由悬浮";
            return "跟随前台";
        }

        private string EnterPolicyLabel()
        {
            return preferences.CollapseAfterEnter ? "回车：收起" : "回车：常驻";
        }

        private string NativePolicyLabel()
        {
            return preferences.SuppressNativeKeyboard ? "系统键盘：抑制" : "系统键盘：共存";
        }

        private void ToggleEnterPolicy()
        {
            preferences.CollapseAfterEnter = !preferences.CollapseAfterEnter;
            preferences.Save();
            SetStatus(preferences.CollapseAfterEnter
                ? "回车后收起为右下角触发按钮" : "回车后保持键盘常驻");
            ReplaceAuxiliaryRow();
        }

        private void ToggleLargeKeys()
        {
            preferences.LargeKeys = !preferences.LargeKeys;
            preferences.HeightPercent = preferences.LargeKeys ? 48 : 42;
            preferences.Save();
            ApplyTouchSizing(root);
            ReplaceAuxiliaryRow();
            RefreshState();
            SetStatus(preferences.LargeKeys
                ? "手机大键已启用；可用高度 ± 微调，工具内有滚轮和候选翻页"
                : "已恢复标准布局");
        }

        private void ApplyTouchSizing(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                var button = child as RoundedButton;
                if (button != null)
                {
                    button.LargeLabels = preferences.LargeKeys;
                    button.Margin = new Padding(preferences.LargeKeys ? 4 : 6);
                }
                if (child.HasChildren) ApplyTouchSizing(child);
            }
        }

        private void ToggleNativePolicy()
        {
            bool requested = !preferences.SuppressNativeKeyboard;
            if (!nativeKeyboardGuard.Apply(requested))
            {
                SetStatus("系统键盘设置未更改，请查看本地日志");
                return;
            }
            preferences.SuppressNativeKeyboard = requested;
            preferences.Save();
            if (preferences.SuppressNativeKeyboard)
                Native.HideSystemTouchKeyboard(targetWindow);
            SetStatus(preferences.SuppressNativeKeyboard
                ? "已抑制 Windows 键盘自动弹出（实验）" : "已与 Windows 键盘共存");
            ReplaceAuxiliaryRow();
        }

        private void ToggleStartup()
        {
            string message;
            StartupSettings.Toggle(out message);
            SetStatus(message);
            ReplaceAuxiliaryRow();
        }

        private void ShowSettingsPage(bool advanced)
        {
            advancedSettings = advanced;
            ReplaceAuxiliaryRow();
        }

        private void RequestCollapse()
        {
            if (DateTime.UtcNow <= collapseConfirmUntilUtc)
            {
                collapseConfirmUntilUtc = DateTime.MinValue;
                CollapseKeyboard();
                return;
            }
            collapseConfirmUntilUtc = DateTime.UtcNow.AddSeconds(2);
            SetStatus("再点一次“—”收起键盘");
        }

        private void CollapseKeyboard()
        {
            if (collapsed) return;
            collapseConfirmUntilUtc = DateTime.MinValue;
            RestoreTargetWindow();
            collapsed = true;
            root.Visible = false;
            triggerPanel.Visible = true;
            triggerPanel.BringToFront();
            PositionKeyboard();
        }

        private void ExpandKeyboard()
        {
            if (!collapsed) return;
            collapsed = false;
            triggerPanel.Visible = false;
            root.Visible = true;
            root.BringToFront();
            PositionKeyboard();
            if (preferences.Mode != KeyboardMode.Floating) ForceTargetWindow();
        }

        private void ToggleDoublePinyin()
        {
            string message;
            MicrosoftPinyinSettings.Toggle(out message);
            SetStatus(message);
            UpdateToolbar();
        }

        private void AdjustHeight(int delta)
        {
            preferences.HeightPercent += delta;
            preferences.Save();
            SetStatus("键盘高度 " + preferences.HeightPercent + "%");
            PositionKeyboard();
            if (preferences.Mode != KeyboardMode.Floating) ForceTargetWindow();
        }

        private void AdjustGap(int delta)
        {
            preferences.LeftGap = Math.Min(96, Math.Max(0, preferences.LeftGap + delta));
            preferences.Save();
            SetStatus("左侧缝隙 " + preferences.LeftGap + " px");
            PositionKeyboard();
        }

        private void ResetLayout()
        {
            preferences.ResetCurrentLayout();
            SetStatus("已还原当前模式布局");
            PositionKeyboard();
            if (preferences.Mode != KeyboardMode.Floating) ForceTargetWindow();
        }

        private void OpenWindowsInputSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:regionlanguage") {
                    UseShellExecute = true
                });
                SetStatus("已打开 Windows 输入设置");
            }
            catch (Exception exception)
            {
                Diagnostics.Report("settings.open-input", exception);
                SetStatus("无法打开 Windows 输入设置");
            }
        }

        private void OpenTouchKeyboardSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:typing") {
                    UseShellExecute = true
                });
                SetStatus("已打开 Windows 触摸键盘设置");
            }
            catch (Exception exception)
            {
                Diagnostics.Report("settings.open-touch-keyboard", exception);
                SetStatus("无法打开 Windows 触摸键盘设置");
            }
        }

        private void SaveVariable(int index)
        {
            string text = ReadClipboard();
            if (String.IsNullOrEmpty(text))
            {
                SetStatus("剪贴板没有可保存的文字");
                return;
            }
            sessionVariables[index] = text;
            SetStatus("已把剪贴板保存到变量 " + (index + 1));
        }

        private void InsertVariable(int index)
        {
            if (String.IsNullOrEmpty(sessionVariables[index]))
            {
                SetStatus("变量 " + (index + 1) + " 还是空的");
                return;
            }
            InsertText(sessionVariables[index]);
        }

        private void InsertClipboard()
        {
            string text = ReadClipboard();
            if (String.IsNullOrEmpty(text)) SetStatus("剪贴板没有文字");
            else InsertText(text);
        }

        private string ReadClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText()) return String.Empty;
                string text = Clipboard.GetText();
                if (text.Length > 4000)
                {
                    SetStatus("剪贴板较长，本次只取前 4000 字");
                    return text.Substring(0, 4000);
                }
                return text;
            }
            catch (Exception exception)
            {
                Diagnostics.Report("clipboard.read", exception);
                return String.Empty;
            }
        }

        private void ClearVariables()
        {
            for (int index = 0; index < sessionVariables.Length; index++)
                sessionVariables[index] = null;
            SetStatus("会话变量已清空");
        }

        private void SetStatus(string message)
        {
            statusMessage = message ?? String.Empty;
            statusUntil = DateTime.Now.AddSeconds(4);
            UpdateToolbar();
        }

        private void UpdateToolbar()
        {
            if (title == null) return;
            string defaultTitle = "Lucky Touch Input  ·  " + ModeLabel() + "  ·  " +
                MicrosoftPinyinSettings.Label + "  ·  " +
                (preferences.SuppressNativeKeyboard ? "独占" : "共存");
            title.Text = DateTime.Now < statusUntil && !String.IsNullOrEmpty(statusMessage)
                ? statusMessage : defaultTitle;
            modeButton.Text = ModeLabel();
            toolsButton.Text = panel != PanelKind.Keys && panel != PanelKind.Settings ? "数字" : "工具";
            variablesButton.Text = panel == PanelKind.Variables ? "数字" : "变量";
            promptsButton.Text = panel == PanelKind.Prompts ? "数字" : "提示";
            settingsButton.Text = panel == PanelKind.Settings ? "数字" : "设置";
            SetButtonActive(variablesButton, panel == PanelKind.Variables);
            SetButtonActive(promptsButton, panel == PanelKind.Prompts);
            SetButtonActive(settingsButton, panel == PanelKind.Settings);
            SetButtonActive(toolsButton, panel == PanelKind.Tools);
        }

        private static void SetButtonActive(RoundedButton button, bool active)
        {
            button.NormalColor = active ? Color.FromArgb(204, 228, 252) : Color.White;
            button.BackColor = button.NormalColor;
        }
    }
}
