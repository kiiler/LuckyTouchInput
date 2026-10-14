using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Lucky.TouchInput
{
    internal static class CompactTests
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--probe") { Probe(args[1]); return 0; }
            bool interactive = args.Contains("--interactive");
            int outputIndex = Array.IndexOf(args, "--output");
            string output = outputIndex >= 0 && outputIndex + 1 < args.Length ? args[outputIndex + 1]
                : Path.Combine("artifacts", "compact-tests", Guid.NewGuid().ToString("N"));
            Run(Path.GetFullPath(output), interactive);
            foreach (string result in results) Console.WriteLine(result);
            Console.WriteLine("Compact checks: " + results.Count(s => s.StartsWith("PASS ")) + " passed.");
            return Environment.ExitCode;
        }
        private static readonly List<string> results = new List<string>();
        private static void Check(string name, bool ok)
        {
            results.Add((ok ? "PASS " : "FAIL ") + name);
            if (!ok) throw new InvalidOperationException(name);
        }
        internal static void Probe(string dir)
        {
            using var form = new Form { Text = "Lucky Compact Input Test", StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(30, 60, 430, 240) };
            var box = new TextBox { Multiline = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 20), ImeMode = ImeMode.Disable };
            form.Controls.Add(box);
            form.Shown += (s, e) => {
                var english = InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().FirstOrDefault(l => l.Culture.TwoLetterISOLanguageName == "en");
                if (english != null) InputLanguage.CurrentInputLanguage = english;
                box.Focus(); File.WriteAllText(Path.Combine(dir, "probe-hwnd.txt"), form.Handle.ToInt64().ToString());
            };
            box.TextChanged += (s, e) => File.WriteAllText(Path.Combine(dir, "probe-text.txt"), box.Text);
            Application.Run(form);
        }
        private static void Pump(int milliseconds)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
        }
        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (Control d in Descendants(c)) yield return d;
            }
        }
        private static void LayoutTree(Control control)
        {
            control.PerformLayout();
            foreach (Control child in control.Controls) LayoutTree(child);
        }
        private static Rectangle BoundsInForm(Control control)
        {
            Point origin = Point.Empty;
            for (Control current = control; current != null && current is not Form; current = current.Parent)
                origin.Offset(current.Location);
            return new Rectangle(origin, control.Size);
        }
        private static void ClickAt(Point point)
        {
            Cursor.Position = point;
            var down = new INPUT { type = 0 }; down.data.mouse.flags = 0x0002;
            var up = new INPUT { type = 0 }; up.data.mouse.flags = 0x0004;
            Check("mouse input delivered", Native.SendInput(2, new[] { down, up }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>()) == 2);
            Pump(160);
        }
        private static void Click(CompactKeyboardForm form, string text)
        {
            var b = Descendants(form).OfType<Button>().First(c => c.Visible && c.Text == text);
            ClickAt(b.PointToScreen(new Point(b.Width / 2, b.Height / 2)));
        }
        private static void Gesture(Control handle, int dx, int dy)
        {
            var start = handle.PointToScreen(new Point(handle.Width / 2, handle.Height / 2));
            Cursor.Position = start;
            var input = new INPUT { type = 0 }; input.data.mouse.flags = 0x0002;
            Native.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>()); Pump(70);
            Cursor.Position = new Point(start.X + dx, start.Y + dy); Pump(100);
            input.data.mouse.flags = 0x0004;
            Native.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>()); Pump(300);
        }
        internal static void Run(string dir, bool interactive)
        {
            Directory.CreateDirectory(dir);
            if (Directory.EnumerateFileSystemEntries(dir).Any())
                throw new InvalidOperationException("Use a new, empty test output directory.");
            Process probe = null;
            Point pointer = Cursor.Position;
            IntPtr previous = Native.GetForegroundWindow();
            try
            {
                var work = new Rectangle(0, 0, 1280, 720);
                Check("default right-hand geometry", CompactGeometry.Restore(new CompactSettings(), work) == new Rectangle(744, 180, 520, 360));
                foreach (var area in new[] { work, new Rectangle(-1920, -100, 1920, 1040), new Rectangle(0, 0, 320, 240) })
                    foreach (var desired in new[] { new Rectangle(-9000, -9000, 20, 20), new Rectangle(9000, 9000, 9000, 9000), new Rectangle(100, 100, 520, 360) })
                        Check("clamp inside " + area + " / " + desired, area.Contains(CompactGeometry.Clamp(desired, area)));
                var saved = new CompactSettings(); saved.Set(new Rectangle(700, 200, 500, 350), work, "test");
                string path = Path.Combine(dir, "settings-roundtrip.json"); saved.Save(path);
                Check("position and size survive disk roundtrip", CompactSettings.Load(path).Bounds == saved.Bounds);
                Check("same resolution restores exactly", CompactGeometry.Restore(saved, work) == saved.Bounds);
                Check("smaller monitor recovers onscreen", new Rectangle(0, 0, 640, 480).Contains(CompactGeometry.Restore(saved, new Rectangle(0, 0, 640, 480))));
                File.WriteAllText(path, "{broken"); Check("corrupt settings recover default", CompactSettings.Load(path).Width == 520);
                string profilePath = Path.Combine(dir, "ui-session.json");
                using var keyboard = new CompactKeyboardForm(interactive, profilePath);
                if (interactive) keyboard.Show();
                keyboard.CreateControl(); Pump(150);
                foreach (var size in new[] { new Size(400, 280), new Size(520, 360), new Size(680, 440) })
                {
                    keyboard.Size = size; LayoutTree(keyboard); Pump(100);
                    foreach (var b in Descendants(keyboard).OfType<Button>().Where(b => b.Text != "⌨"))
                        Check("visible key fits " + size + " " + b.Text, b.Width >= 20 && b.Height >= 20 && keyboard.ClientRectangle.Contains(BoundsInForm(b)));
                    using var bitmap = new Bitmap(size.Width, size.Height);
                    keyboard.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
                    bitmap.Save(Path.Combine(dir, "layout-" + size.Width + ".png"));
                }
                if (!interactive) return;
                keyboard.Bounds = CompactGeometry.Restore(new CompactSettings(), Screen.PrimaryScreen.WorkingArea); Pump(100);
                var psi = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false };
                psi.ArgumentList.Add("--probe"); psi.ArgumentList.Add(dir); probe = Process.Start(psi);
                string hwndPath = Path.Combine(dir, "probe-hwnd.txt");
                for (int i = 0; i < 100 && !File.Exists(hwndPath); i++) Pump(50);
                var hwnd = new IntPtr(long.Parse(File.ReadAllText(hwndPath)));
                Native.SetForegroundWindow(hwnd); Pump(350);
                Check("probe has foreground", Native.GetForegroundWindow() == hwnd);
                Check("keyboard is visible to Windows", Native.IsWindowVisible(keyboard.Handle));
                Native.GetWindowRect(hwnd, out RECT original);
                Click(keyboard, "a"); Click(keyboard, "b"); Click(keyboard, "c");
                Check("real clicks type abc in separate process", File.ReadAllText(Path.Combine(dir, "probe-text.txt")) == "abc");
                Check("keyboard does not steal focus", Native.GetForegroundWindow() == hwnd);
                Click(keyboard, "⌫"); Check("backspace edits target", File.ReadAllText(Path.Combine(dir, "probe-text.txt")) == "ab");
                Click(keyboard, "⇧"); Click(keyboard, "C");
                Check("one-shot shift types uppercase", File.ReadAllText(Path.Combine(dir, "probe-text.txt")) == "abC");
                Click(keyboard, "123"); Click(keyboard, "1"); Click(keyboard, "2"); Click(keyboard, "ABC");
                Check("number page types digits", File.ReadAllText(Path.Combine(dir, "probe-text.txt")) == "abC12");
                var before = keyboard.Bounds;
                Gesture(Descendants(keyboard).OfType<Label>().First(l => l.Text.Contains("拖动 ·")), -60, 20);
                Check("real header drag persists beyond refresh", keyboard.Left == before.Left - 60 && keyboard.Top == before.Top + 20);
                before = keyboard.Bounds;
                Gesture(Descendants(keyboard).OfType<Label>().First(l => l.Text == "◢"), 50, 30);
                Check("real grip resize persists beyond refresh", keyboard.Width == before.Width + 50 && keyboard.Height == before.Height + 30);
                before = keyboard.Bounds;
                Click(keyboard, "—"); Check("collapse makes small bubble", keyboard.Width == 76);
                Click(keyboard, "⌨"); Check("expand restores geometry", keyboard.Bounds == before);
                Click(keyboard, "a"); Check("typing after drag and resize", File.ReadAllText(Path.Combine(dir, "probe-text.txt")) == "abC12a");
                Click(keyboard, "x"); Click(keyboard, "x"); Click(keyboard, "x");
                Check("rapid repeated key remains input", File.ReadAllText(Path.Combine(dir, "probe-text.txt")) == "abC12axxx");
                Native.GetWindowRect(hwnd, out RECT after);
                Check("target window never resized", original.Left == after.Left && original.Top == after.Top && original.Width == after.Width && original.Height == after.Height);
                keyboard.Close();
                using var reopened = new CompactKeyboardForm(true, profilePath);
                Check("fresh keyboard restores saved drag and resize", reopened.Bounds == before);
            }
            catch (Exception e) { results.Add("ERROR " + e); Environment.ExitCode = 1; }
            finally
            {
                if (probe != null && !probe.HasExited) { probe.CloseMainWindow(); if (!probe.WaitForExit(1500)) probe.Kill(); }
                if (interactive) { Cursor.Position = pointer; if (Native.IsWindow(previous)) Native.SetForegroundWindow(previous); }
                File.WriteAllLines(Path.Combine(dir, "results.txt"), results);
            }
        }
    }
}
