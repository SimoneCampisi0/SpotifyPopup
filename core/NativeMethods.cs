using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace SpotifyPopup;

internal static class NativeMethods
{
    internal const int GwlExStyle = -20, WsExTransparent = 0x20, WsExNoActivate = 0x08000000;
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(IntPtr h, int n);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] internal static extern int SetWindowLong(IntPtr h, int n, int value);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(IntPtr h, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(IntPtr h, int id);

    internal static Rect Bounds(Window window)
    {
        GetWindowRect(new WindowInteropHelper(window).Handle, out var rect); return rect;
    }
    internal static void Move(Window window, int x, int y) => SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, x, y, 0, 0, 0x0015);
    internal static void EnsureVisible(Window window, bool snap = false)
    {
        var rect = Bounds(window);
        var screen = Forms.Screen.FromRectangle(new System.Drawing.Rectangle(rect.Left, rect.Top, Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top)));
        var area = screen.WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(window);
        var margin = (int)Math.Round(10 * dpi.DpiScaleX);
        var width = rect.Right - rect.Left; var height = rect.Bottom - rect.Top;
        if (width > area.Width || height > area.Height)
        {
            window.Width = Math.Max(window.MinWidth, Math.Min(window.Width, area.Width / dpi.DpiScaleX));
            window.Height = Math.Max(window.MinHeight, Math.Min(window.Height, area.Height / dpi.DpiScaleY));
            window.UpdateLayout(); rect = Bounds(window); width = rect.Right - rect.Left; height = rect.Bottom - rect.Top;
        }
        var x = rect.Left; var y = rect.Top;
        if (snap)
        {
            var threshold = (int)(22 * dpi.DpiScaleX);
            if (Math.Abs(x - area.Left) <= threshold) x = area.Left + margin;
            else if (Math.Abs(rect.Right - area.Right) <= threshold) x = area.Right - width - margin;
            if (Math.Abs(y - area.Top) <= threshold) y = area.Top + margin;
            else if (Math.Abs(rect.Bottom - area.Bottom) <= threshold) y = area.Bottom - height - margin;
        }
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - height));
        if (x != rect.Left || y != rect.Top) Move(window, x, y);
    }
}
