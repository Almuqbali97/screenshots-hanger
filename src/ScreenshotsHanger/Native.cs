using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenshotsHanger;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] public static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongW")] public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
    public static string? ScreenshotFolder()
    {
        var id = new Guid("B7BEDE81-DF94-4682-A7D8-57A52620B86F");
        int result = SHGetKnownFolderPath(ref id, 0x4000, IntPtr.Zero, out var path);
        try { return result == 0 ? Marshal.PtrToStringUni(path) : null; }
        finally { if (path != IntPtr.Zero) Marshal.FreeCoTaskMem(path); }
    }
    public static double ScaleAt(int x, int y)
    {
        var monitor = MonitorFromPoint(new Point { X = x, Y = y }, 2);
        return GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96d : 1d;
    }
    public static void ConfigureOverlay(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000000 | 0x80); // no activate, tool window
        SetWindowDisplayAffinity(hwnd, 0x11); // exclude our own UI from captures on supported Windows
    }
}
