using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Lucky.TouchInput
{
    internal sealed class NativeKeyboardGuard : IDisposable
    {
        private readonly NativeKeyboardLease lease = new NativeKeyboardLease(
            NativeKeyboardLease.TabletTipPath, UserPreferences.NativeKeyboardLeasePath);
        private readonly Native.WinEventDelegate callback;
        private readonly ManualResetEvent hookReady = new ManualResetEvent(false);
        private readonly Thread hookThread;
        private IntPtr foregroundHook, showHook;
        private uint hookThreadId;
        private volatile bool suppressing;
        private bool disposed;
        private DateTime retryAfterUtc = DateTime.MinValue;
        internal bool IsSuppressing { get { return suppressing; } }
        internal IntPtr OwnerWindow { get; set; }

        internal NativeKeyboardGuard()
        {
            bool hadLease = File.Exists(UserPreferences.NativeKeyboardLeasePath);
            if (lease.Recover() && hadLease) Native.BroadcastTabletTipSettings();
            callback = HandleWinEvent;
            hookThread = new Thread(RunWinEventLoop) { IsBackground = true,
                Name = "LuckyTouchInput.NativeGuard" };
            hookThread.SetApartmentState(ApartmentState.STA);
            hookThread.Start();
            hookReady.WaitOne(1000);
        }

        internal bool Apply(bool suppress)
        {
            if (disposed) return false;
            if (!suppress) suppressing = false; // Stop hooks even if settings restoration fails.
            if (suppress && suppressing || !suppress && !lease.Owned) return true;
            if (DateTime.UtcNow < retryAfterUtc) return false;
            bool success = suppress ? lease.Acquire() : lease.Release();
            if (success)
            {
                suppressing = suppress;
                Native.BroadcastTabletTipSettings();
            }
            else retryAfterUtc = DateTime.UtcNow.AddSeconds(3);
            return success;
        }

        public void Dispose()
        {
            if (disposed) return;
            suppressing = false;
            if (hookThreadId != 0) Native.PostThreadMessage(hookThreadId, Native.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
            if (hookThread.IsAlive) hookThread.Join(1000);
            if (!hookThread.IsAlive) hookReady.Dispose();
            bool owned = lease.Owned;
            if (lease.Release() && owned) Native.BroadcastTabletTipSettings();
            disposed = true;
        }

        private void RunWinEventLoop()
        {
            try
            {
                hookThreadId = Native.GetCurrentThreadId();
                foregroundHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND,
                    Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, callback, 0, 0,
                    Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
                showHook = Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW,
                    IntPtr.Zero, callback, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
                hookReady.Set();
                Application.Run();
            }
            finally
            {
                if (foregroundHook != IntPtr.Zero) Native.UnhookWinEvent(foregroundHook);
                if (showHook != IntPtr.Zero) Native.UnhookWinEvent(showHook);
                hookThreadId = 0;
                hookReady.Set();
            }
        }

        private void HandleWinEvent(IntPtr hook, uint eventType, IntPtr hwnd,
            int objectId, int childId, uint threadId, uint eventTime)
        {
            if (!suppressing || hwnd == IntPtr.Zero || objectId != 0 || childId != 0) return;
            try
            {
                if (KeyboardInteropPolicy.IsTouchProcess(Native.ProcessName(hwnd)) && suppressing &&
                    OwnerWindow != IntPtr.Zero) Native.HideSystemTouchKeyboard(OwnerWindow);
            }
            catch (Exception exception) { Diagnostics.Report("native-keyboard.event", exception); }
        }
    }
}
