using System;
using System.Drawing;
using System.IO;
using Lucky.TouchInput;

internal static class Program
{
    private static int failures;
    private static int checks;

    private static int Main()
    {
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
            migrated.Save(newPath);
            var reloaded = UserPreferences.Load(newPath, oldPath);
            Check("new settings take priority over old", (int)KeyboardMode.Floating, (int)reloaded.Mode);
            Check("save/load round trip", 32, reloaded.HeightPercent);
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

        if (failures == 0)
        {
            Console.WriteLine("Lucky Touch Input checks: " + checks + " passed.");
            return 0;
        }
        Console.Error.WriteLine("Lucky Touch Input layout checks failed: " + failures);
        return 1;
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
