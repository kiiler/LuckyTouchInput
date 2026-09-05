using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using Lucky.TouchInput;

internal static class Program
{
    private static int failures;
    private static int checks;

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Check("phone height", 784, LayoutPolicy.KeyboardBounds(
            new Rectangle(0, 0, 3840, 2064), KeyboardMode.FollowForeground, 38, 0).Height);
        Check("foldable height cap", 774, LayoutPolicy.KeyboardBounds(
            new Rectangle(0, 0, 2480, 2152), KeyboardMode.FollowForeground, 42, 0).Height);
        Check("floating ignores foldable cap", 903, LayoutPolicy.KeyboardBounds(
            new Rectangle(0, 0, 2480, 2152), KeyboardMode.Floating, 42, 0).Height);
        Check("negative monitor left", -1920, LayoutPolicy.KeyboardBounds(
            new Rectangle(-1920, 0, 1920, 1040), KeyboardMode.FollowForeground, 42, 0).Left);
        Check("left gap clamped", 160, LayoutPolicy.KeyboardBounds(
            new Rectangle(0, 0, 1920, 1040), KeyboardMode.FollowForeground, 42, 999).Left);
        Check("floating minimum percent", 24,
            LayoutPolicy.ClampHeightPercent(KeyboardMode.Floating, 1));
        Check("follow maximum percent", 50,
            LayoutPolicy.ClampHeightPercent(KeyboardMode.FollowForeground, 99));
        Check("tiny work area stays inside", 80, LayoutPolicy.KeyboardBounds(
            new Rectangle(0, 0, 320, 120), KeyboardMode.FollowForeground, 42, 0).Height);

        var invalid = UserPreferences.Parse(new[] { "Mode=999", "FollowHeight=-5",
            "FloatingHeight=500", "LeftGap=9999", "CollapseAfterEnter=nonsense" }, false);
        Check("invalid mode falls back", (int)KeyboardMode.FollowForeground, (int)invalid.Mode);
        Check("invalid height clamped", 28, invalid.FollowHeight);
        Check("floating height clamped", 44, invalid.FloatingHeight);
        Check("gap clamped", 96, invalid.LeftGap);
        Check("invalid enter policy", false, invalid.CollapseAfterEnter);
        Check("coexist is default", false, invalid.SuppressNativeKeyboard);
        Check("large keys is opt in", false, invalid.LargeKeys);
        Check("large keys setting parsed", true,
            UserPreferences.Parse(new[] { "LargeKeys=True" }, false).LargeKeys);
        Check("large foldable respects chosen height", 1032, LayoutPolicy.KeyboardBounds(
            new Rectangle(0, 0, 2480, 2152), KeyboardMode.FollowForeground, 48, 0, true).Height);
        Check("pin without process recovers", (int)KeyboardMode.FollowForeground,
            (int)UserPreferences.Parse(new[] { "Mode=PinnedApplication" }, false).Mode);
        var legacy = UserPreferences.Parse(new[] { "Mode=Codex", "CodexHeight=38",
            "RemoteHeight=46", "DesktopHeight=30", "SuppressNativeKeyboard=True" }, true);
        Check("legacy Codex pins application", (int)KeyboardMode.PinnedApplication, (int)legacy.Mode);
        Check("legacy process retained", true, legacy.PinnedProcessName == "Codex");
        Check("legacy height retained", 38, legacy.HeightPercent);
        Check("legacy remote height retained", 46, legacy.FollowHeight);
        Check("legacy desktop height retained", 30, legacy.FloatingHeight);
        Check("legacy opt-in suppression retained", true, legacy.SuppressNativeKeyboard);
        Check("legacy desktop maps to floating", (int)KeyboardMode.Floating,
            (int)UserPreferences.Parse(new[] { "Mode=Desktop" }, true).Mode);
        Check("legacy remote maps to follow", (int)KeyboardMode.FollowForeground,
            (int)UserPreferences.Parse(new[] { "Mode=Remote" }, true).Mode);

        string scratch = Path.Combine(Path.GetTempPath(), "lti-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string oldPath = Path.Combine(scratch, "old.ini");
            string newPath = Path.Combine(scratch, "current.ini");
            File.WriteAllLines(oldPath, new[] { "Mode=Codex", "CodexHeight=38" });
            var migrated = UserPreferences.Load(newPath, oldPath);
            Check("migration writes new settings", true, File.Exists(newPath));
            Check("migration leaves original intact", true, File.ReadAllText(oldPath).Contains("CodexHeight=38"));
            migrated.Mode = KeyboardMode.Floating;
            migrated.FloatingHeight = 32;
            migrated.LargeKeys = true;
            migrated.Save(newPath);
            var reloaded = UserPreferences.Load(newPath, oldPath);
            Check("new settings take priority over old", (int)KeyboardMode.Floating, (int)reloaded.Mode);
            Check("save/load round trip", 32, reloaded.HeightPercent);
            Check("large keys save/load round trip", true, reloaded.LargeKeys);
            Check("atomic save leaves no temporary files", 0, Directory.GetFiles(scratch, "*.tmp").Length);
        }
        finally { Directory.Delete(scratch, true); }

        foreach (var work in new[] { new Rectangle(0, 0, 1280, 752),
            new Rectangle(0, 0, 800, 1280), new Rectangle(-1920, -1080, 1920, 1040),
            new Rectangle(0, 0, 2480, 2152), new Rectangle(0, 0, 3840, 2064) })
        foreach (KeyboardMode mode in Enum.GetValues<KeyboardMode>())
        {
            Rectangle keyboard = LayoutPolicy.KeyboardBounds(work, mode, 42, 0);
            Check("keyboard inside working area " + work + mode, true, work.Contains(keyboard));
            Check("keyboard above taskbar " + work + mode, work.Bottom, keyboard.Bottom);
        }

        string settingsBefore = File.Exists(UserPreferences.ConfigPath) ? File.ReadAllText(UserPreferences.ConfigPath) : null;
        string leaseBefore = File.Exists(UserPreferences.NativeKeyboardLeasePath) ? File.ReadAllText(UserPreferences.NativeKeyboardLeasePath) : null;
        foreach (Rectangle work in new[] { new Rectangle(0, 0, 1280, 752),
            new Rectangle(0, 0, 3840, 2064), new Rectangle(0, 0, 2480, 2152),
            new Rectangle(0, 0, 800, 1280) })
        {
            int standardKeyHeight;
            using (var standard = new KeyboardForm(new UserPreferences(), false))
            {
                standard.ApplyKeyboardBounds(LayoutPolicy.KeyboardBounds(work, KeyboardMode.FollowForeground, 38, 0));
                LayoutTree(standard);
                standardKeyHeight = Descendants(standard).Single(c => c.Text == "q").Height;
            }
            var prefs = new UserPreferences { LargeKeys = true, FollowHeight = 48 };
            using (var form = new KeyboardForm(prefs, false))
            {
                form.ApplyKeyboardBounds(LayoutPolicy.KeyboardBounds(work, prefs.Mode, 48, 0, true));
                LayoutTree(form);
                Control[] controls = Descendants(form).ToArray();
                Control enter = controls.Single(c => c.Text == "回车");
                Control q = controls.Single(c => c.Text == "q");
                Control space = controls.Single(c => c.Text == "空格");
                string scenario = " " + work.Width + "x" + work.Height;
                Check("all letters remain" + scenario, 26, controls.Count(c => c is Button && c.Text.Length == 1 && c.Text[0] >= 'a' && c.Text[0] <= 'z'));
                Check("all digits remain" + scenario, 10, controls.Count(c => c is Button && c.Text.Length == 1 && c.Text[0] >= '0' && c.Text[0] <= '9'));
                Check("five bottom buttons" + scenario, 5, enter.Parent.Controls.Count);
                Check("enter is rightmost" + scenario, true, enter.Parent.Controls.Cast<Control>().All(c => c.Right <= enter.Right));
                Check("enter is wide" + scenario, true, enter.Width >= q.Width * 1.5);
                Check("space left of enter" + scenario, true, space.Right < enter.Left);
                Check("letter keys taller" + scenario, true, q.Height > standardKeyHeight);
                if (work.Width == 3840)
                    Check("phone keys at least 25 percent taller", true, q.Height >= standardKeyHeight * 1.25);
                Check("actual keyboard bounds above taskbar" + scenario, work.Bottom, form.Bottom);
                Check("all typing buttons usable" + scenario, true,
                    controls.Where(c => c is RoundedButton && c.Tag is ushort).All(c => c.Width >= 24 && c.Height >= 24));
                if (args.Length == 2 && args[0] == "--render-directory")
                {
                    Directory.CreateDirectory(args[1]);
                    // Real WinForms painting needs visible child controls. This test form is
                    // transparent, no-activate, and has no runtime hooks or tray entry.
                    form.Opacity = 0;
                    form.Show();
                    LayoutTree(form);
                    using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                    {
                        form.DrawToBitmap(bitmap, form.ClientRectangle);
                        bitmap.Save(Path.Combine(args[1], "large-keys-" + work.Width + ".png"));
                    }
                    form.Hide();
                }
                form.TogglePanel(PanelKind.Tools);
                LayoutTree(form);
                controls = Descendants(form).ToArray();
                Check("tools contain candidate paging" + scenario, true, controls.Any(c => c.Text == "候选›" && c.Tag is ushort key && key == 0x22));
                Check("tools contain wheel" + scenario, true, controls.Any(c => c.Text == "滚↓"));
                form.TogglePanel(PanelKind.Tools);
                Check("tools restore ten digits" + scenario, 10,
                    Descendants(form).Count(c => c is Button && c.Text.Length == 1 && c.Text[0] >= '0' && c.Text[0] <= '9'));
            }
        }
        Check("layout checks leave user settings intact", true,
            settingsBefore == (File.Exists(UserPreferences.ConfigPath) ? File.ReadAllText(UserPreferences.ConfigPath) : null));
        Check("layout checks leave native lease intact", true,
            leaseBefore == (File.Exists(UserPreferences.NativeKeyboardLeasePath) ? File.ReadAllText(UserPreferences.NativeKeyboardLeasePath) : null));

        if (failures == 0)
        {
            Console.WriteLine("Lucky Touch Input checks: " + checks + " passed.");
            return 0;
        }
        Console.Error.WriteLine("Lucky Touch Input layout checks failed: " + failures);
        return 1;
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void LayoutTree(Control parent)
    {
        parent.PerformLayout();
        foreach (Control child in parent.Controls) LayoutTree(child);
    }

    private static void Check(string name, int expected, int actual)
    {
        checks++;
        if (expected == actual) return;
        failures++;
        Console.Error.WriteLine(name + ": expected " + expected + ", actual " + actual);
    }

    private static void Check(string name, bool expected, bool actual)
    {
        Check(name, expected ? 1 : 0, actual ? 1 : 0);
    }
}
