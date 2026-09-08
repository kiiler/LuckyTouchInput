using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using Lucky.TouchInput;

internal static partial class Program
{
    private static void CheckEverydayLayouts(string[] args)
    {
        Check("explicit common preset beats legacy flag", (int)KeyboardLayout.Everyday,
            (int)UserPreferences.Parse(new[] { "Layout=Everyday", "LargeKeys=True" }, false).Layout);
        Check("explicit standard beats legacy flag", (int)KeyboardLayout.Standard,
            (int)UserPreferences.Parse(new[] { "LargeKeys=True", "Layout=Standard" }, false).Layout);
        Check("invalid preset keeps migration fallback", (int)KeyboardLayout.PhoneLarge,
            (int)UserPreferences.Parse(new[] { "Layout=999", "LargeKeys=True" }, false).Layout);
        Check("case-insensitive preset", (int)KeyboardLayout.Everyday,
            (int)UserPreferences.Parse(new[] { "Layout=everyday" }, false).Layout);
        var settings = new UserPreferences();
        settings.SelectLayout(KeyboardLayout.Everyday);
        Check("common starts at 44 percent", 44, settings.HeightPercent);
        settings.Mode = KeyboardMode.Floating;
        settings.SelectLayout(KeyboardLayout.Standard);
        Check("standard floating retains proper default", 34, settings.HeightPercent);

        foreach (Rectangle work in new[] { new Rectangle(0,0,1280,752), new Rectangle(0,0,800,1280),
            new Rectangle(0,0,2480,2152), new Rectangle(0,0,3840,2064) })
        using (var form = new KeyboardForm(new UserPreferences(), false))
        {
            string scenario = " common " + work.Width;
            form.SelectLayoutPreset(KeyboardLayout.Everyday);
            Rectangle desired = LayoutPolicy.KeyboardBounds(work, KeyboardMode.FollowForeground, 44, 0, true);
            Control surface = LayoutVirtualSurface(form, desired);
            Control[] controls = Descendants(form).ToArray();
            Control tab = controls.Single(c => c.Text == "Tab");
            Control q = controls.Single(c => c.Text == "q");
            Control back = controls.Single(c => c.Text == "退格");
            Control enter = controls.Single(c => c.Text == "回车");
            Control ctrl = controls.Single(c => c.Text == "Ctrl");
            Check("all letters" + scenario, 26, controls.Count(c => c is Button && c.Text.Length == 1 && c.Text[0] >= 'a' && c.Text[0] <= 'z'));
            Check("all digits" + scenario, 10, controls.Count(c => c is Button && c.Tag is ushort key && key >= 0x30 && key <= 0x39));
            Check("Tab at Q row left" + scenario, true, tab.Parent == q.Parent && tab.Right < q.Left);
            Control a = controls.Single(c => c.Text == "a");
            Control z = controls.Single(c => c.Text == "z");
            Check("desktop row stagger" + scenario, true, a.Left > q.Left && a.Left < q.Right && z.Left > a.Left && z.Left < a.Right);
            Check("Ctrl on bottom row" + scenario, true, ctrl.Parent == enter.Parent && ctrl.Left < enter.Left);
            Check("bottom arrows" + scenario, 2, enter.Parent.Controls.Cast<Control>().Count(c => c.Text == "←" || c.Text == "→"));
            Check("backspace first row right" + scenario, true, back.Parent.Controls.Cast<Control>().All(c => c.Right <= back.Right));
            Check("common enter rightmost" + scenario, true, enter.Parent.Controls.Cast<Control>().All(c => c.Right <= enter.Right));
            Check("common enter stays wide" + scenario, true, enter.Width >= q.Width * 1.5);
            Check("common eight bottom keys" + scenario, 8, enter.Parent.Controls.Count);
            Check("common typing targets usable" + scenario, true,
                controls.Where(c => c is RoundedButton && c.Tag is ushort).All(c => c.Width >= 24 && c.Height >= 24));
            Check("common surface leaves taskbar" + scenario, work.Bottom, desired.Top + surface.Height);
            foreach (Control button in new[] { tab, back, enter, ctrl })
            {
                Size label = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine);
                Check("label fits without wrapping " + button.Text + scenario, true,
                    label.Width <= button.Width - button.Padding.Horizontal - 10 &&
                    label.Height <= button.Height - button.Padding.Vertical - 6);
            }
            if (args.Length == 2 && args[0] == "--render-directory")
            {
                Directory.CreateDirectory(args[1]);
                form.Opacity = 0;
                form.Show();
                LayoutTree(form);
                using (var bitmap = new Bitmap(surface.Width, surface.Height))
                {
                    surface.DrawToBitmap(bitmap, surface.ClientRectangle);
                    bitmap.Save(Path.Combine(args[1], "everyday-" + work.Width + ".png"));
                }
                form.Hide();
            }
            form.TogglePanel(PanelKind.Edit);
            LayoutTree(form);
            Check("edit shortcuts visible" + scenario, true, Descendants(form).Any(c => c.Text == "复制") && Descendants(form).Any(c => c.Text == "撤销"));
            form.TogglePanel(PanelKind.Edit);
            Check("editing returns digit row" + scenario, 10,
                Descendants(form).Count(c => c is Button && c.Tag is ushort key && key >= 0x30 && key <= 0x39));
            for (int cycle = 0; cycle < 6; cycle++)
            {
                form.SelectLayoutPreset((KeyboardLayout)(cycle % 3));
                LayoutTree(form);
                Check("preset switch keeps one layout " + cycle + scenario, 2, form.Controls.Count);
                Check("preset switch keeps one Enter " + cycle + scenario, 1, Descendants(form).Count(c => c.Text == "回车"));
            }
            form.TogglePanel(PanelKind.Settings);
            var layoutButton = Descendants(form).OfType<Button>().Single(c => c.Text.StartsWith("布局：", StringComparison.Ordinal));
            // Invoking the layout action disposes/rebuilds its own button. It must not throw.
            typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(layoutButton, new object[] { EventArgs.Empty });
            Check("layout remains in settings", 1, Descendants(form).Count(c => c.Text.StartsWith("布局：", StringComparison.Ordinal)));
        }

        var chord = Native.KeySequence(0x41, true, true);
        Check("Ctrl Shift chord has six input events", 6, chord.Count);
        Check("Ctrl starts sequence", 0x11, chord[0].data.keyboard.virtualKey);
        Check("Shift starts before letter", 0x10, chord[1].data.keyboard.virtualKey);
        Check("letter follows modifiers", 0x41, chord[2].data.keyboard.virtualKey);
        Check("letter is released", (int)Native.KEYEVENTF_KEYUP, (int)chord[3].data.keyboard.flags);
        Check("Shift released before Ctrl", 0x10, chord[4].data.keyboard.virtualKey);
        Check("Ctrl is released", (int)Native.KEYEVENTF_KEYUP, (int)chord[5].data.keyboard.flags);
        Check("Shift Tab supported", 4, Native.KeySequence(0x09, true, false).Count);
    }

    private static void CheckNativeInterop()
    {
        foreach (bool prefer in new[] { false, true })
        foreach (bool expanded in new[] { false, true })
        foreach (bool target in new[] { false, true })
        foreach (bool touch in new[] { false, true })
        foreach (bool osk in new[] { false, true })
        foreach (bool waiting in new[] { false, true })
        {
            KeyboardOwnership actual = KeyboardInteropPolicy.Decide(prefer, expanded, target, touch, osk, waiting);
            if (!expanded || !target) Check("inactive never suppresses", (int)KeyboardOwnership.Inactive, (int)actual);
            else if (osk) Check("OSK always receives ownership", (int)KeyboardOwnership.YieldToWindows, (int)actual);
            else if (touch && (waiting || !prefer)) Check("native visible or failed takeover yields", (int)KeyboardOwnership.YieldToWindows, (int)actual);
            else Check("active ownership", (int)(prefer ? KeyboardOwnership.SuppressTouch : KeyboardOwnership.Coexist), (int)actual);
        }
        var pane = new Rectangle(0,600,1280,400);
        foreach (string name in new[] { "consent", "LogonUI", "LockApp" })
            Check("secure system surface is ineligible " + name, true, KeyboardInteropPolicy.IsSecureSurfaceProcess(name));
        Check("ordinary foreground remains eligible", false, KeyboardInteropPolicy.IsSecureSurfaceProcess("notepad"));
        Check("confirmed modern touch pane", true, KeyboardInteropPolicy.IsConfirmedTouchWindow("TextInputHost", "Any", pane, pane));
        Check("process alone is not a keyboard", false, KeyboardInteropPolicy.IsConfirmedTouchWindow("TextInputHost", "Any", pane, Rectangle.Empty));
        Check("small candidate is not a keyboard", false, KeyboardInteropPolicy.IsConfirmedTouchWindow("TextInputHost", "Any", new Rectangle(0,600,400,80), pane));
        Check("nonoverlapping native popup ignored", false, KeyboardInteropPolicy.IsConfirmedTouchWindow("TextInputHost", "Any", new Rectangle(0,0,800,400), pane));
        Check("OSK never targeted for hiding", false, KeyboardInteropPolicy.IsConfirmedTouchWindow("osk", "Any", pane, pane));
        Check("legacy keyboard class fallback", true, KeyboardInteropPolicy.IsConfirmedTouchWindow("TabTip", "IPTip_Main_Window", pane, Rectangle.Empty));
        Check("legacy unrelated window ignored", false, KeyboardInteropPolicy.IsConfirmedTouchWindow("TabTip", "Other", pane, Rectangle.Empty));

        string scratch = Path.Combine(Path.GetTempPath(), "lti-lease-test-" + Guid.NewGuid().ToString("N"));
        string keyPath = @"Software\LuckyTouchInput.Tests\" + Guid.NewGuid().ToString("N");
        string path = Path.Combine(scratch, "lease.ini");
        Directory.CreateDirectory(scratch);
        var lease = new NativeKeyboardLease(keyPath, path, delegate { });
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                key.SetValue(NativeKeyboardLease.TapInvoke, 1, RegistryValueKind.DWord);
                Check("acquire native lease", true, lease.Acquire());
                Check("suppression writes zero", 0, (int)key.GetValue(NativeKeyboardLease.TapInvoke));
                Check("lease persisted before use", true, File.Exists(path));
                Check("idempotent acquire", true, lease.Acquire());
                Check("release succeeds", true, lease.Release());
                Check("original restored", 1, (int)key.GetValue(NativeKeyboardLease.TapInvoke));
                Check("missing value restored by deletion", true, key.GetValue(NativeKeyboardLease.DesktopInvoke) == null);
                Check("lease removed after restore", false, File.Exists(path));
                key.SetValue(NativeKeyboardLease.TapInvoke, 2, RegistryValueKind.DWord);
                Check("coexist no writes", true, lease.Release());
                Check("coexist retains new setting", 2, (int)key.GetValue(NativeKeyboardLease.TapInvoke));
                lease.Acquire();
                key.SetValue(NativeKeyboardLease.TapInvoke, 1, RegistryValueKind.DWord);
                key.DeleteValue(NativeKeyboardLease.DesktopInvoke, false);
                lease.Release();
                Check("user change during suppression retained", 1, (int)key.GetValue(NativeKeyboardLease.TapInvoke));
                Check("user deletion during suppression retained", true, key.GetValue(NativeKeyboardLease.DesktopInvoke) == null);
                lease.Acquire();
                key.SetValue(NativeKeyboardLease.TapInvoke, "user-value", RegistryValueKind.String);
                lease.Release();
                Check("external value type change retained", true, (string)key.GetValue(NativeKeyboardLease.TapInvoke) == "user-value");
                Check("unexpected original type rejected", false, lease.Acquire());
                Check("failed capture leaves no lease", false, File.Exists(path));
                key.SetValue(NativeKeyboardLease.TapInvoke, 1, RegistryValueKind.DWord);
                lease.Acquire();
                var recovery = new NativeKeyboardLease(keyPath, path, delegate { });
                Check("simulated crash recovers", true, recovery.Recover());
                Check("crash baseline restored", 1, (int)key.GetValue(NativeKeyboardLease.TapInvoke));
                // Retire the abandoned test instance before starting another lease.
                lease.Release();
                File.WriteAllLines(path, new[] { "SchemaVersion=1", "TapInvoke=1,2", "DesktopInvoke=0,0" });
                key.SetValue(NativeKeyboardLease.TapInvoke, 0, RegistryValueKind.DWord);
                key.SetValue(NativeKeyboardLease.DesktopInvoke, 0, RegistryValueKind.DWord);
                Check("alpha lease format recovers", true, recovery.Recover());
                Check("alpha lease value restored", 2, (int)key.GetValue(NativeKeyboardLease.TapInvoke));
                File.WriteAllLines(path, new[] { "SchemaVersion=2", "TapInvoke=1,1" });
                Check("incomplete lease blocks acquisition", false, recovery.Acquire());
                Check("incomplete lease preserved", true, File.Exists(path));
                Check("incomplete lease leaves settings intact", 2, (int)key.GetValue(NativeKeyboardLease.TapInvoke));
                File.Delete(path);
                string prefsPath = Path.Combine(scratch, "settings.ini");
                var prefs = new UserPreferences { Layout = KeyboardLayout.Everyday };
                prefs.Save(prefsPath);
                Check("common preset save/load", (int)KeyboardLayout.Everyday, (int)UserPreferences.Load(prefsPath, null).Layout);
                Check("atomic lease leaves no temporary files", 0, Directory.GetFiles(scratch, "*.tmp").Length);
            }
        }
        finally
        {
            // The random, test-owned subkey and directory are the only cleanup targets.
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
            Directory.Delete(scratch, true);
        }
    }
}
