using System;
using System.ComponentModel;
using ShelfDrop.Core;

namespace ShelfDrop.App.Native
{
    /// <summary>
    /// A system-wide mouse hook (WH_MOUSE_LL): it sees the pointer wherever it is, in any program, without taking focus
    /// and without injecting anything into other programs. The callbacks run on the thread that installed the hook
    /// (the UI thread), so they can touch the UI directly, but they must return quickly or Windows drops the hook.
    /// </summary>
    internal sealed class MouseHook : IDisposable
    {
        // Held in a field: a delegate that only native code points at would be collected, and the hook would then crash the app.
        private readonly NativeMethods.HookProc _callback;
        private IntPtr _handle;
        private bool _leftDown;

        public MouseHook()
        {
            _callback = OnHook;
        }

        public event Action<PixelPoint>? LeftDown;
        public event Action? LeftUp;

        /// <summary>Position in physical pixels and the time of the event in seconds.</summary>
        public event Action<PixelPoint, double>? Moved;

        public void Install()
        {
            if (_handle != IntPtr.Zero) return;
            _handle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _callback, NativeMethods.GetModuleHandle(null), 0);
            if (_handle == IntPtr.Zero)
                throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "Could not install the mouse hook.");
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(_handle);
            _handle = IntPtr.Zero;
        }

        private unsafe IntPtr OnHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int message = (int)wParam;
                    if (message == NativeMethods.WM_MOUSEMOVE)
                    {
                        // Moving with no button down is nearly all of the traffic; only a drag is of interest.
                        if (_leftDown)
                        {
                            NativeMethods.MSLLHOOKSTRUCT* info = (NativeMethods.MSLLHOOKSTRUCT*)lParam;
                            Moved?.Invoke(new PixelPoint(info->pt.X, info->pt.Y), info->time / 1000.0);
                        }
                    }
                    else if (message == NativeMethods.WM_LBUTTONDOWN)
                    {
                        NativeMethods.MSLLHOOKSTRUCT* info = (NativeMethods.MSLLHOOKSTRUCT*)lParam;
                        _leftDown = true;
                        LeftDown?.Invoke(new PixelPoint(info->pt.X, info->pt.Y));
                    }
                    else if (message == NativeMethods.WM_LBUTTONUP)
                    {
                        _leftDown = false;
                        LeftUp?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    // An exception thrown into native code would end the process. Log it and carry on.
                    Log.Error("mouse hook callback failed", e);
                }
            }
            return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);
        }
    }
}
