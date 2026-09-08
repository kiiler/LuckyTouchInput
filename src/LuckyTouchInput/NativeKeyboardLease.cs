using System;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace Lucky.TouchInput
{
    // Kept separate from window hooks so recovery can be tested against a disposable HKCU key.
    internal sealed class NativeKeyboardLease
    {
        internal const string TabletTipPath = @"Software\Microsoft\TabletTip\1.7";
        internal const string TapInvoke = "TouchKeyboardTapInvoke";
        internal const string DesktopInvoke = "EnableDesktopModeAutoInvoke";
        private readonly string registryPath;
        private readonly string leasePath;
        private readonly Action<string, Exception> report;
        private Snapshot tap, desktop;
        private bool applied;
        internal bool Owned { get; private set; }

        internal NativeKeyboardLease(string registryPath, string leasePath, Action<string, Exception> report = null)
        {
            this.registryPath = registryPath;
            this.leasePath = leasePath;
            this.report = report ?? Diagnostics.Report;
        }

        internal bool Recover()
        {
            if (!File.Exists(leasePath)) return true;
            try
            {
                Snapshot recoveredTap = null, recoveredDesktop = null;
                foreach (string line in File.ReadAllLines(leasePath))
                {
                    string[] parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    if (parts[0] == "TapInvoke") recoveredTap = Snapshot.Parse(parts[1]);
                    if (parts[0] == "DesktopInvoke") recoveredDesktop = Snapshot.Parse(parts[1]);
                }
                if (recoveredTap == null || recoveredDesktop == null)
                    throw new InvalidDataException("Native keyboard lease is incomplete.");
                tap = recoveredTap;
                desktop = recoveredDesktop;
                Owned = true;
                return Release();
            }
            catch (Exception exception) { report("native-keyboard.recover", exception); return false; }
        }

        internal bool Acquire()
        {
            if (Owned && applied) return true;
            if (Owned && !Release()) return false;
            // Never overwrite an unresolved recovery lease with a new baseline.
            if (File.Exists(leasePath) && !Recover()) return false;
            string temporary = leasePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    tap = Snapshot.Capture(key, TapInvoke);
                    desktop = Snapshot.Capture(key, DesktopInvoke);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(leasePath)));
                File.WriteAllLines(temporary, new[] { "SchemaVersion=2",
                    "TapInvoke=" + tap.Serialize(), "DesktopInvoke=" + desktop.Serialize() });
                File.Move(temporary, leasePath, false);
                Owned = true;
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    key.SetValue(TapInvoke, 0, RegistryValueKind.DWord);
                    key.SetValue(DesktopInvoke, 0, RegistryValueKind.DWord);
                }
                applied = true;
                return true;
            }
            catch (Exception exception)
            {
                report("native-keyboard.acquire", exception);
                Release();
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception exception) { report("native-keyboard.lease-cleanup", exception); }
            }
        }

        internal bool Release()
        {
            applied = false;
            if (!Owned) return true;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    tap.RestoreIfOwned(key, TapInvoke);
                    desktop.RestoreIfOwned(key, DesktopInvoke);
                }
                File.Delete(leasePath);
                Owned = false;
                return true;
            }
            catch (Exception exception) { report("native-keyboard.restore", exception); return false; }
        }

        private sealed class Snapshot
        {
            private readonly bool existed;
            private readonly int value;
            private Snapshot(bool existed, int value) { this.existed = existed; this.value = value; }

            internal static Snapshot Capture(RegistryKey key, string name)
            {
                object current = key == null ? null : key.GetValue(name);
                if (current == null) return new Snapshot(false, 0);
                if (key.GetValueKind(name) != RegistryValueKind.DWord)
                    throw new InvalidDataException("Native keyboard preference must be a DWORD.");
                return new Snapshot(true, (int)current);
            }

            internal void RestoreIfOwned(RegistryKey key, string name)
            {
                // A user/Windows change during our lease wins, including deletion/type changes.
                if (key == null || !(key.GetValue(name) is int current) || current != 0 ||
                    key.GetValueKind(name) != RegistryValueKind.DWord) return;
                if (existed) key.SetValue(name, value, RegistryValueKind.DWord);
                else key.DeleteValue(name, false);
            }

            internal string Serialize() { return (existed ? "1" : "0") + "," + value.ToString(CultureInfo.InvariantCulture); }
            internal static Snapshot Parse(string text)
            {
                string[] parts = text.Split(',');
                if (parts.Length != 2 || (parts[0] != "0" && parts[0] != "1") ||
                    !Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    throw new InvalidDataException("Native keyboard lease value is invalid.");
                return new Snapshot(parts[0] == "1", value);
            }
        }
    }
}
