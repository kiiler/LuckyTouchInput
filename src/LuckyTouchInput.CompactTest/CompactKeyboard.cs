using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace Lucky.TouchInput
{
    internal sealed class CompactSettings
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; } = 520;
        public int Height { get; set; } = 360;
        public int WorkX { get; set; }
        public int WorkY { get; set; }
        public int WorkWidth { get; set; }
        public int WorkHeight { get; set; }
        public string Display { get; set; } = "";
        internal Rectangle Bounds => new Rectangle(X, Y, Width, Height);
        internal Rectangle Work => new Rectangle(WorkX, WorkY, WorkWidth, WorkHeight);
        internal static string PathName => Path.Combine(UserPreferences.ConfigDirectory, "compact.json");
        internal static CompactSettings Load(string path)
        {
            try { return JsonSerializer.Deserialize<CompactSettings>(File.ReadAllText(path)) ?? new CompactSettings(); }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            { return new CompactSettings(); }
        }
        internal void Set(Rectangle bounds, Rectangle work, string display)
        {
            X = bounds.X; Y = bounds.Y; Width = bounds.Width; Height = bounds.Height;
            WorkX = work.X; WorkY = work.Y; WorkWidth = work.Width; WorkHeight = work.Height;
            Display = display;
        }
        internal void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this));
            File.Move(path + ".tmp", path, true);
        }
    }

    internal static class CompactGeometry
    {
        internal static Rectangle Clamp(Rectangle desired, Rectangle work)
        {
            int w = Math.Clamp(desired.Width, Math.Min(400, work.Width), Math.Max(1, work.Width));
            int h = Math.Clamp(desired.Height, Math.Min(280, work.Height), Math.Max(1, work.Height));
            return new Rectangle(Math.Clamp(desired.X, work.Left, work.Right - w),
                Math.Clamp(desired.Y, work.Top, work.Bottom - h), w, h);
        }
        internal static Rectangle Restore(CompactSettings saved, Rectangle work)
        {
            if (saved.WorkWidth <= 0 || saved.WorkHeight <= 0)
                return Clamp(new Rectangle(work.Right - 536, work.Top + (work.Height - 360) / 2, 520, 360), work);
            var b = Clamp(saved.Bounds, saved.Work);
            double rx = (b.X - saved.WorkX) / (double)Math.Max(1, saved.WorkWidth - b.Width);
            double ry = (b.Y - saved.WorkY) / (double)Math.Max(1, saved.WorkHeight - b.Height);
            int w = Math.Min(b.Width, work.Width), h = Math.Min(b.Height, work.Height);
            return Clamp(new Rectangle(work.X + (int)Math.Round(rx * (work.Width - w)),
                work.Y + (int)Math.Round(ry * (work.Height - h)), w, h), work);
        }
    }

    internal sealed class CompactKeyboardForm : Form
    {
        internal const string WindowTitle = "Lucky Touch Input · Compact Test";
        private readonly CompactSettings settings;
        private readonly bool persist;
        private readonly string settingsPath;
        private readonly Timer timer = new Timer { Interval = 200 };
        private readonly TableLayoutPanel shell = new TableLayoutPanel();
        private readonly Panel keys = new Panel();
        private readonly Label drag = new Label();
        private readonly Label resize = new Label();
        private readonly Label status = new Label();
        private readonly RoundedButton bubble;
        private readonly NotifyIcon tray;
        private IntPtr target;
        private bool shifted, controlled, collapsed, manipulating, resizing;
        private Point startPointer;
        private Rectangle startBounds, expandedBounds;
        private string page = "abc";
        private DateTime statusUntil;
        private readonly ToolTip hints = new ToolTip();

        internal CompactKeyboardForm(bool persist = true, string settingsPath = null)
        {
            this.persist = persist;
            this.settingsPath = settingsPath ?? CompactSettings.PathName;
            settings = persist ? CompactSettings.Load(this.settingsPath) : new CompactSettings();
            Text = WindowTitle;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = true; AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(222, 228, 237);
            Padding = new Padding(2);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == settings.Display)
                ?? Screen.FromHandle(Native.GetForegroundWindow());
            Bounds = CompactGeometry.Restore(settings, screen.WorkingArea);
            expandedBounds = Bounds;
            settings.Set(Bounds, screen.WorkingArea, screen.DeviceName);

            shell.Dock = DockStyle.Fill; shell.RowCount = 3; shell.ColumnCount = 1;
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            Controls.Add(shell);
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 39));
            drag.Text = "⠿  拖动 · 测试版"; drag.TextAlign = ContentAlignment.MiddleLeft;
            drag.Dock = DockStyle.Fill; drag.Cursor = Cursors.SizeAll;
            drag.Font = new Font("Microsoft YaHei UI", 12, GraphicsUnit.Pixel);
            header.Controls.Add(drag, 0, 0);
            header.Controls.Add(Action("⇄", SwitchSide, "切换到屏幕另一侧"), 1, 0);
            header.Controls.Add(Action("−", () => ChangeSize(-40, -24), "缩小键盘"), 2, 0);
            header.Controls.Add(Action("+", () => ChangeSize(40, 24), "放大键盘"), 3, 0);
            header.Controls.Add(Action("—", Collapse, "收起为小按钮"), 4, 0);
            header.Controls.Add(Action("×", Close, "退出测试版"), 5, 0);
            shell.Controls.Add(header, 0, 0);
            keys.Dock = DockStyle.Fill; shell.Controls.Add(keys, 0, 1);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft;
            status.Font = new Font("Microsoft YaHei UI", 11, GraphicsUnit.Pixel);
            resize.Text = "◢"; resize.TextAlign = ContentAlignment.MiddleCenter;
            resize.Dock = DockStyle.Fill; resize.Cursor = Cursors.SizeNWSE;
            hints.SetToolTip(resize, "按住这里拖动，调整宽度和高度");
            footer.Controls.Add(status, 0, 0); footer.Controls.Add(resize, 1, 0);
            shell.Controls.Add(footer, 0, 2);
            WireGesture(drag, false); WireGesture(resize, true);
            bubble = Action("⌨", Expand, "展开紧凑键盘"); bubble.Dock = DockStyle.Fill;
            bubble.Visible = false; Controls.Add(bubble);
            if (persist)
            {
                var menu = new ContextMenuStrip();
                menu.Items.Add("显示键盘", null, (s, e) => Expand());
                menu.Items.Add("收起键盘", null, (s, e) => Collapse());
                menu.Items.Add("重置到右侧", null, (s, e) => ResetPosition());
                menu.Items.Add("退出测试版", null, (s, e) => Close());
                tray = new NotifyIcon { Icon = Icon, Text = "Lucky 紧凑悬浮测试版", Visible = true, ContextMenuStrip = menu };
                tray.DoubleClick += (s, e) => Expand();
            }
            BuildKeys();
            timer.Tick += (s, e) => RefreshState();
            Shown += (s, e) => { CaptureTarget(); timer.Start(); };
            FormClosing += (s, e) => { timer.Stop(); SaveBounds(); };
        }

        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000080; return p; }
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x21) { message.Result = new IntPtr(3); return; }
            if (message.Msg == Native.WM_SHOW_KEYBOARD) { Expand(); return; }
            base.WndProc(ref message);
        }

        private RoundedButton Action(string text, System.Action action, string tip = "")
        {
            var b = new RoundedButton { Text = text, Dock = DockStyle.Fill, Margin = new Padding(3), LargeLabels = true };
            b.Click += (s, e) => action();
            if (tip.Length > 0) hints.SetToolTip(b, tip);
            return b;
        }
        private RoundedButton Key(string text, ushort vk)
        {
            var b = Action(text, () => Send(vk)); b.Tag = vk;
            return b;
        }
        private TableLayoutPanel Row(params Control[] controls)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = controls.Length, RowCount = 1, Margin = Padding.Empty };
            for (int i = 0; i < controls.Length; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, controls[i].Text == "空格" ? 3 : 1));
                row.Controls.Add(controls[i], i, 0);
            }
            return row;
        }
        private Control LetterRow(string letters)
        {
            return Row(letters.Select(c => (Control)Key(shifted ? char.ToUpperInvariant(c).ToString() : c.ToString(),
                (ushort)char.ToUpperInvariant(c))).ToArray());
        }
        private void SetPage(string value) { page = value; ClearModifiers(); RebuildLater(); }
        private void RebuildLater() { if (IsHandleCreated) BeginInvoke((System.Action)BuildKeys); else BuildKeys(); }
        private void BuildKeys()
        {
            if (IsDisposed || Disposing) return;
            keys.SuspendLayout();
            foreach (Control old in keys.Controls.Cast<Control>().ToArray()) { keys.Controls.Remove(old); old.Dispose(); }
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Margin = Padding.Empty };
            for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            if (page == "abc")
            {
                grid.Controls.Add(LetterRow("qwertyuiop"), 0, 0);
                grid.Controls.Add(Row("asdfghjkl".Select(c => (Control)Key(shifted ? char.ToUpperInvariant(c).ToString() : c.ToString(), (ushort)char.ToUpperInvariant(c)))
                    .Append(Key("⌫", 0x08)).ToArray()), 0, 1);
                var shift = Action(shifted ? "⇧ ●" : "⇧", () => { shifted = !shifted; RebuildLater(); });
                grid.Controls.Add(Row(new Control[] { shift }.Concat("zxcvbnm".Select(c => (Control)Key(shifted ? char.ToUpperInvariant(c).ToString() : c.ToString(), (ushort)char.ToUpperInvariant(c))))
                    .Concat(new Control[] { Key(",", 0xBC), Key(".", 0xBE) }).ToArray()), 0, 2);
            }
            else if (page == "123")
            {
                grid.Controls.Add(Row("1234567890".Select(c => (Control)Key(c.ToString(), c)).ToArray()), 0, 0);
                grid.Controls.Add(Row(Key("-", 0xBD), Key("=", 0xBB), Key("[", 0xDB), Key("]", 0xDD), Key("\\", 0xDC), Key(";", 0xBA), Key("'", 0xDE), Key("⌫", 0x08)), 0, 1);
                grid.Controls.Add(Row(Action(shifted ? "Shift ●" : "Shift", () => { shifted = !shifted; RebuildLater(); }), Key("`", 0xC0), Key("/", 0xBF), Key(",", 0xBC), Key(".", 0xBE), Key("←", 0x25), Key("→", 0x27)), 0, 2);
            }
            else
            {
                grid.Controls.Add(Row(Key("Esc", 0x1B), Key("Tab", 0x09), Key("Home", 0x24), Key("End", 0x23), Key("⌫", 0x08)), 0, 0);
                grid.Controls.Add(Row(Action("全选", () => Chord(0x41)), Action("复制", () => Chord(0x43)), Action("粘贴", () => Chord(0x56)), Action("撤销", () => Chord(0x5A)), Action("换行", () => Send(0x0D, true))), 0, 1);
                grid.Controls.Add(Row(Action(controlled ? "Ctrl ●" : "Ctrl", () => { controlled = !controlled; page = "abc"; RebuildLater(); }), Key("候选‹", 0x21), Key("候选›", 0x22), Key("←", 0x25), Key("↑", 0x26), Key("↓", 0x28), Key("→", 0x27)), 0, 2);
            }
            grid.Controls.Add(Row(Action(page == "123" ? "ABC" : "123", () => SetPage(page == "123" ? "abc" : "123")),
                Action("中/英", () => { ClearModifiers(); Send(0x10); }), Key("空格", 0x20),
                Action(page == "tools" ? "ABC" : "工具", () => SetPage(page == "tools" ? "abc" : "tools")), Key("回车", 0x0D)), 0, 3);
            keys.Controls.Add(grid); keys.ResumeLayout(true);
        }

        private bool ValidTarget(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd) || Native.IsIconic(hwnd) || Native.IsSystemSurface(hwnd)) return false;
            Native.GetWindowThreadProcessId(hwnd, out uint pid);
            return pid != Environment.ProcessId;
        }
        private void CaptureTarget()
        {
            var active = Native.GetForegroundWindow();
            if (ValidTarget(active))
            {
                if (target != active) { ClearModifiers(); RebuildLater(); }
                target = active;
            }
        }
        private void Send(ushort vk, bool forceShift = false)
        {
            CaptureTarget();
            var foreground = Native.GetForegroundWindow();
            Native.GetWindowThreadProcessId(foreground, out uint pid);
            if (!ValidTarget(target) || (foreground != target && pid != Environment.ProcessId))
            { Status("请先点选需要输入的文本框"); ClearModifiers(); RebuildLater(); return; }
            if (foreground != target) Native.SetForegroundWindow(target);
            if (Native.GetForegroundWindow() != target) { Status("未能恢复输入焦点"); return; }
            bool ok = Native.Key(vk, forceShift || shifted, controlled);
            ClearModifiers(); RebuildLater();
            if (!ok) Status("输入未送达：请使用普通权限的窗口");
        }
        private void Chord(ushort vk) { CaptureTarget(); controlled = true; Send(vk); }
        private void ClearModifiers() { shifted = false; controlled = false; }
        private void Status(string text) { status.Text = text; statusUntil = DateTime.Now.AddSeconds(4); }
        private void RefreshState()
        {
            if (!manipulating) CaptureTarget();
            if (!manipulating)
            {
                var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == settings.Display) ?? Screen.FromRectangle(expandedBounds);
                if (screen.WorkingArea != settings.Work)
                {
                    expandedBounds = CompactGeometry.Restore(settings, screen.WorkingArea);
                    if (!collapsed) Bounds = expandedBounds; else PositionBubble();
                    SaveBounds();
                }
            }
            if (DateTime.Now > statusUntil)
                status.Text = (controlled ? "Ctrl · " : shifted ? "Shift · " : "") + "拖顶部移动 · 拖右下角缩放 · 自动记忆";
        }
        private void WireGesture(Control control, bool size)
        {
            control.MouseDown += (s, e) => {
                if (e.Button != MouseButtons.Left) return;
                manipulating = true; resizing = size; startPointer = Cursor.Position; startBounds = Bounds; control.Capture = true;
            };
            control.MouseMove += (s, e) => {
                if (!manipulating || !control.Capture) return;
                var delta = new Size(Cursor.Position.X - startPointer.X, Cursor.Position.Y - startPointer.Y);
                var proposed = resizing ? new Rectangle(startBounds.Location, new Size(startBounds.Width + delta.Width, startBounds.Height + delta.Height))
                    : new Rectangle(startBounds.Location + delta, startBounds.Size);
                var screen = resizing ? Screen.FromRectangle(startBounds) : Screen.FromPoint(Cursor.Position);
                Bounds = CompactGeometry.Clamp(proposed, screen.WorkingArea);
                expandedBounds = Bounds;
            };
            control.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) control.Capture = false; };
            control.MouseCaptureChanged += (s, e) => {
                if (manipulating && !control.Capture) { manipulating = false; expandedBounds = Bounds; SaveBounds(); }
            };
        }
        private void SaveBounds()
        {
            var screen = Screen.FromRectangle(expandedBounds);
            settings.Set(expandedBounds, screen.WorkingArea, screen.DeviceName);
            if (!persist) return;
            try { settings.Save(settingsPath); }
            catch (Exception e) { Diagnostics.Report("compact.save", e); Status("位置未保存，请检查配置目录权限"); }
        }
        private void ChangeSize(int dw, int dh)
        {
            Bounds = CompactGeometry.Clamp(new Rectangle(Right - Width - dw, Top, Width + dw, Height + dh), Screen.FromRectangle(Bounds).WorkingArea);
            expandedBounds = Bounds; SaveBounds();
        }
        private void SwitchSide()
        {
            var work = Screen.FromRectangle(Bounds).WorkingArea;
            int x = Left + Width / 2 > work.Left + work.Width / 2 ? work.Left + 16 : work.Right - Width - 16;
            Bounds = CompactGeometry.Clamp(new Rectangle(x, Top, Width, Height), work);
            expandedBounds = Bounds; SaveBounds();
        }
        private void ResetPosition()
        {
            var work = Screen.FromHandle(Native.GetForegroundWindow()).WorkingArea;
            expandedBounds = CompactGeometry.Restore(new CompactSettings(), work); Expand(); SaveBounds();
        }
        private void PositionBubble()
        {
            var work = Screen.FromRectangle(expandedBounds).WorkingArea;
            Bounds = new Rectangle(Math.Clamp(expandedBounds.Right - 76, work.Left, Math.Max(work.Left, work.Right - 76)),
                Math.Clamp(expandedBounds.Top, work.Top, Math.Max(work.Top, work.Bottom - 48)), Math.Min(76, work.Width), Math.Min(48, work.Height));
        }
        internal void Collapse()
        {
            if (collapsed) return;
            expandedBounds = Bounds; SaveBounds(); collapsed = true; ClearModifiers();
            shell.Visible = false; bubble.Visible = true; bubble.BringToFront(); PositionBubble();
        }
        internal void Expand()
        {
            collapsed = false; bubble.Visible = false; shell.Visible = true;
            Bounds = CompactGeometry.Clamp(expandedBounds, Screen.FromRectangle(expandedBounds).WorkingArea);
            expandedBounds = Bounds; BuildKeys(); if (!Visible) Show();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); tray?.Dispose(); hints.Dispose(); drag.Font.Dispose(); status.Font.Dispose(); }
            base.Dispose(disposing);
        }
    }

    public static class CompactHost
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using var mutex = new System.Threading.Mutex(true, "Local\\Lucky.TouchInput.CompactTest.SingleInstance", out bool first);
            if (!first) { Native.ShowExistingKeyboard(CompactKeyboardForm.WindowTitle); return; }
            Application.Run(new CompactKeyboardForm());
        }
    }
}
