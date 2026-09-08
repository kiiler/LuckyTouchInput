using System;
using System.Drawing;

namespace Lucky.TouchInput
{
    internal enum KeyboardOwnership { Inactive, YieldToWindows, Coexist, SuppressTouch }

    internal static class KeyboardInteropPolicy
    {
        internal static KeyboardOwnership Decide(bool preferLucky, bool expanded, bool hasTarget,
            bool touchVisible, bool oskVisible, bool waitingForNativeDismissal)
        {
            if (!expanded || !hasTarget) return KeyboardOwnership.Inactive;
            // Explicit accessibility input always wins; never close OSK or change its startup.
            if (oskVisible || touchVisible && (!preferLucky || waitingForNativeDismissal))
                return KeyboardOwnership.YieldToWindows;
            return preferLucky ? KeyboardOwnership.SuppressTouch : KeyboardOwnership.Coexist;
        }

        internal static bool IsTouchProcess(string name)
        {
            return String.Equals(name, "TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(name, "TabTip", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(name, "WindowsInputExperience", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsSecureSurfaceProcess(string name)
        {
            return String.Equals(name, "consent", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(name, "LogonUI", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(name, "LockApp", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsConfirmedTouchWindow(string process, string windowClass,
            Rectangle window, Rectangle pane)
        {
            if (!IsTouchProcess(process) || window.Width < 240 || window.Height < 120) return false;
            // This legacy class identifies the keyboard itself, not an IME candidate surface.
            if (String.Equals(process, "TabTip", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(windowClass, "IPTip_Main_Window", StringComparison.Ordinal)) return true;
            if (pane.Width < 240 || pane.Height < 120) return false;
            Rectangle overlap = Rectangle.Intersect(window, pane);
            return (long)overlap.Width * overlap.Height >= (long)pane.Width * pane.Height * 9 / 10;
        }
    }
}
