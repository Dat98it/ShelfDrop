using System;
using System.Collections.Generic;
using ShelfDrop.Core;

namespace ShelfDrop.App.Native
{
    /// <summary>Asks Windows where the monitors are, what part of each is usable, and how sharp each one is.</summary>
    internal sealed class WindowsScreenProvider : IScreenProvider
    {
        public IReadOnlyList<ScreenInfo> Screens
        {
            get
            {
                var screens = new List<ScreenInfo>();
                NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, hdc, rect, data) =>
                {
                    var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
                    if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return true;

                    double scale = 1.0;
                    if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                        scale = dpiX / 96.0;

                    screens.Add(new ScreenInfo(
                        ToRect(info.rcMonitor),
                        ToRect(info.rcWork),
                        scale,
                        (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));
                    return true;
                }, IntPtr.Zero);
                return screens;
            }
        }

        public PixelPoint CursorPosition =>
            NativeMethods.GetCursorPos(out NativeMethods.POINT point) ? new PixelPoint(point.X, point.Y) : new PixelPoint(0, 0);

        /// <summary>The scale of the primary display, for sizes that should feel the same on any screen.</summary>
        public static double SystemScale()
        {
            uint dpi = NativeMethods.GetDpiForSystem();
            return dpi > 0 ? dpi / 96.0 : 1.0;
        }

        private static PixelRect ToRect(NativeMethods.RECT rect) =>
            new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}
