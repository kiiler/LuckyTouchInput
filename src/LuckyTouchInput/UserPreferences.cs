using System;
using System.Globalization;
using System.IO;

namespace Lucky.TouchInput
{
    internal sealed class UserPreferences
    {
        internal static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lucky", "TouchInput");
        internal static readonly string ConfigPath = Path.Combine(ConfigDirectory, "settings.ini");
        internal static readonly string NativeKeyboardLeasePath = Path.Combine(
            ConfigDirectory, "native-keyboard-lease.ini");

        internal KeyboardMode Mode = KeyboardMode.FollowForeground;
        internal string PinnedProcessName = String.Empty;
        internal int PinnedHeight = 42;
        internal int FollowHeight = 42;
        internal int FloatingHeight = 34;
        internal int LeftGap;
        internal bool CollapseAfterEnter;
        internal bool SuppressNativeKeyboard;
        internal KeyboardLayout Layout = KeyboardLayout.Standard;
        // Retain the alpha.1 setting/API as a migration alias.
        internal bool LargeKeys
        {
            get { return Layout == KeyboardLayout.PhoneLarge; }
            set { if (value) Layout = KeyboardLayout.PhoneLarge;
                else if (Layout == KeyboardLayout.PhoneLarge) Layout = KeyboardLayout.Standard; }
        }
        internal bool CompactToolbar { get { return Layout != KeyboardLayout.Standard; } }

        internal static UserPreferences Load()
        {
            string legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LuckyTouchKeyboard", "settings.ini");
            return Load(ConfigPath, legacyPath);
        }

        internal static UserPreferences Load(string path, string legacyPath)
        {
            try
            {
                if (File.Exists(path)) return Parse(File.ReadAllLines(path), false);
                if (!String.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath))
                {
                    var migrated = Parse(File.ReadAllLines(legacyPath), true);
                    migrated.Save(path);
                    return migrated;
                }
            }
            catch (Exception exception) { Diagnostics.Report("settings.load", exception); }
            return new UserPreferences();
        }

        internal static UserPreferences Parse(string[] lines, bool legacy)
        {
            var preferences = new UserPreferences();
            KeyboardLayout? explicitLayout = null;
            foreach (string rawLine in lines)
            {
                string[] parts = rawLine.Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;
                string key = parts[0].Trim();
                string value = parts[1].Trim();
                int number;
                bool flag;
                KeyboardMode mode;
                if (key == "Mode")
                {
                    if (legacy)
                    {
                        if (value.Equals("Codex", StringComparison.OrdinalIgnoreCase))
                        {
                            preferences.Mode = KeyboardMode.PinnedApplication;
                            preferences.PinnedProcessName = "Codex";
                        }
                        else if (value.Equals("Desktop", StringComparison.OrdinalIgnoreCase))
                            preferences.Mode = KeyboardMode.Floating;
                    }
                    else if (Enum.TryParse(value, true, out mode) && Enum.IsDefined(mode))
                        preferences.Mode = mode;
                }
                else if (key == "PinnedProcessName" && !legacy)
                    preferences.PinnedProcessName = value;
                else if ((key == "PinnedHeight" || legacy && key == "CodexHeight") &&
                    Int32.TryParse(value, out number))
                    preferences.PinnedHeight = LayoutPolicy.ClampHeightPercent(
                        KeyboardMode.PinnedApplication, number);
                else if ((key == "FollowHeight" || legacy && key == "RemoteHeight") &&
                    Int32.TryParse(value, out number))
                    preferences.FollowHeight = LayoutPolicy.ClampHeightPercent(
                        KeyboardMode.FollowForeground, number);
                else if ((key == "FloatingHeight" || legacy && key == "DesktopHeight") &&
                    Int32.TryParse(value, out number))
                    preferences.FloatingHeight = LayoutPolicy.ClampHeightPercent(
                        KeyboardMode.Floating, number);
                else if (key == "LeftGap" && Int32.TryParse(value, out number))
                    preferences.LeftGap = Math.Clamp(number, 0, 96);
                else if (key == "CollapseAfterEnter" && Boolean.TryParse(value, out flag))
                    preferences.CollapseAfterEnter = flag;
                else if (key == "SuppressNativeKeyboard" && Boolean.TryParse(value, out flag))
                    preferences.SuppressNativeKeyboard = flag;
                else if (key == "LargeKeys" && Boolean.TryParse(value, out flag))
                    preferences.LargeKeys = flag;
                else if (key == "Layout" && Enum.TryParse(value, true, out KeyboardLayout layout) &&
                    Enum.IsDefined(layout)) explicitLayout = layout;
            }
            if (explicitLayout.HasValue) preferences.Layout = explicitLayout.Value;
            if (preferences.Mode == KeyboardMode.PinnedApplication &&
                String.IsNullOrWhiteSpace(preferences.PinnedProcessName))
                preferences.Mode = KeyboardMode.FollowForeground;
            return preferences;
        }

        internal int HeightPercent
        {
            get { return Mode == KeyboardMode.PinnedApplication ? PinnedHeight :
                Mode == KeyboardMode.Floating ? FloatingHeight : FollowHeight; }
            set
            {
                int clamped = LayoutPolicy.ClampHeightPercent(Mode, value);
                if (Mode == KeyboardMode.PinnedApplication) PinnedHeight = clamped;
                else if (Mode == KeyboardMode.Floating) FloatingHeight = clamped;
                else FollowHeight = clamped;
            }
        }

        internal void Save() { Save(ConfigPath); }

        internal void Save(string path)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllLines(temporary, new[] {
                    "SchemaVersion=2", "Mode=" + Mode,
                    "PinnedProcessName=" + PinnedProcessName,
                    "PinnedHeight=" + PinnedHeight.ToString(CultureInfo.InvariantCulture),
                    "FollowHeight=" + FollowHeight.ToString(CultureInfo.InvariantCulture),
                    "FloatingHeight=" + FloatingHeight.ToString(CultureInfo.InvariantCulture),
                    "LeftGap=" + LeftGap.ToString(CultureInfo.InvariantCulture),
                    "CollapseAfterEnter=" + CollapseAfterEnter,
                    "SuppressNativeKeyboard=" + SuppressNativeKeyboard,
                    "LargeKeys=" + LargeKeys, "Layout=" + Layout
                });
                File.Move(temporary, path, true);
            }
            catch (Exception exception) { Diagnostics.Report("settings.save", exception); }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception exception) { Diagnostics.Report("settings.cleanup", exception); }
            }
        }

        internal void ResetCurrentLayout()
        {
            HeightPercent = DefaultHeight;
            LeftGap = 0;
            Save();
        }

        internal int DefaultHeight { get { return LargeKeys ? 48 :
            Layout == KeyboardLayout.Everyday ? 44 : Mode == KeyboardMode.Floating ? 34 : 42; } }

        internal void SelectLayout(KeyboardLayout layout)
        {
            Layout = Enum.IsDefined(layout) ? layout : KeyboardLayout.Standard;
            HeightPercent = DefaultHeight;
        }
    }
}
