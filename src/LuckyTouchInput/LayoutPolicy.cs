using System;
using System.Drawing;

namespace Lucky.TouchInput
{
    internal static class LayoutPolicy
    {
        internal static int ClampHeightPercent(KeyboardMode mode, int value)
        {
            int minimum = mode == KeyboardMode.Floating ? 24 : 28;
            int maximum = mode == KeyboardMode.Floating ? 44 : 50;
            return Math.Min(maximum, Math.Max(minimum, value));
        }

        internal static int EffectiveHeightPercent(
            KeyboardMode mode, int configured, int width, int height, bool largeKeys = false)
        {
            int clamped = ClampHeightPercent(mode, configured);
            if (largeKeys || mode == KeyboardMode.Floating || width <= 0 || height <= 0)
                return clamped;
            double aspect = width / (double)height;
            return aspect < 1.30 ? Math.Min(clamped, 36) : clamped;
        }

        internal static Rectangle KeyboardBounds(
            Rectangle workingArea, KeyboardMode mode, int configuredHeightPercent,
            int requestedLeftGap, bool largeKeys = false)
        {
            int leftGap = Math.Min(Math.Max(0, requestedLeftGap),
                Math.Max(0, workingArea.Width / 12));
            int percent = EffectiveHeightPercent(mode, configuredHeightPercent,
                workingArea.Width, workingArea.Height, largeKeys);
            int requested = workingArea.Height * percent / 100;
            int topReserve = Math.Min(280, Math.Max(0, workingArea.Height / 3));
            int maximum = Math.Max(1, workingArea.Height - topReserve);
            int minimum = Math.Min(360, maximum);
            int height = Math.Min(maximum, Math.Max(minimum, requested));
            return new Rectangle(workingArea.Left + leftGap,
                workingArea.Bottom - height, Math.Max(1, workingArea.Width - leftGap), height);
        }
    }
}
