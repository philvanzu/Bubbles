using System;
using System.Runtime.InteropServices;

namespace Bubbles4.Services;
internal static class Win32
{
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    public const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const int WS_CAPTION = 0x00C00000;
    public const int WS_THICKFRAME = 0x00040000;

    public const int WS_EX_DLGMODALFRAME = 0x00000001;
    public const int WS_EX_CLIENTEDGE = 0x00000200;
    public const int WS_EX_STATICEDGE = 0x00020000;
    public const int WS_EX_WINDOWEDGE = 0x00000100;

    public const int SWP_NOZORDER = 0x0004;
    public const int SWP_FRAMECHANGED = 0x0020;
    public const int SWP_SHOWWINDOW = 0x0040;

    public static readonly IntPtr HWND_TOP = IntPtr.Zero;
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}