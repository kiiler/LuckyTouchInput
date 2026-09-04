using System;
using System.Drawing;
using Lucky.TouchInput;

internal static class Program
{
    private static int failures;

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

        if (failures == 0)
        {
            Console.WriteLine("Lucky Touch Input layout checks: 8 passed.");
            return 0;
        }
        Console.Error.WriteLine("Lucky Touch Input layout checks failed: " + failures);
        return 1;
    }

    private static void Check(string name, int expected, int actual)
    {
        if (expected == actual) return;
        failures++;
        Console.Error.WriteLine(name + ": expected " + expected + ", actual " + actual);
    }
}
