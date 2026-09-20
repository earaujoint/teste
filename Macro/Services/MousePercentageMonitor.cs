using System.Diagnostics;
using System.Runtime.InteropServices;
using Macro.Services.Models;

namespace Macro.Services;

/// <summary>Reports the mouse position as a percentage of the window clicked.</summary>
public static class MousePercentageMonitor
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const uint GaRoot = 2;
    private static readonly HookProcedure HookCallback = ProcessMouseMessage;
    private static IntPtr hook;

    public static bool IsRunning => hook != IntPtr.Zero;

    public static void Start()
    {
        if (IsRunning) return;
        hook = SetWindowsHookEx(WhMouseLl, HookCallback, IntPtr.Zero, 0);
        if (hook == IntPtr.Zero)
            throw new InvalidOperationException("Não foi possível iniciar a captura de cliques do Windows.");
    }

    public static void Stop()
    {
        if (!IsRunning) return;
        UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
    }

    public static MousePercentage? GetAtCursor()
    {
        GetCursorPos(out var point);
        return GetPercentage(point);
    }

    private static IntPtr ProcessMouseMessage(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt32() == WmLButtonDown)
        {
            var mouse = Marshal.PtrToStructure<LowLevelMouseInfo>(data);
            MousePercentage? percentage = GetPercentage(mouse.Point);
            if (percentage is not null)
                Debug.WriteLine($"X: {percentage.X:P2} | Y: {percentage.Y:P2}");
        }
        return CallNextHookEx(hook, code, message, data);
    }

    private static MousePercentage? GetPercentage(Point screenPoint)
    {
        IntPtr clickedWindow = GetAncestor(WindowFromPoint(screenPoint), GaRoot);
        if (clickedWindow == IntPtr.Zero || !GetClientRect(clickedWindow, out var client))
            return null;

        var topLeft = new Point();
        if (!ClientToScreen(clickedWindow, ref topLeft)) return null;
        int width = client.Right - client.Left;
        int height = client.Bottom - client.Top;
        if (width <= 0 || height <= 0) return null;

        double x = (double)(screenPoint.X - topLeft.X) / width;
        double y = (double)(screenPoint.Y - topLeft.Y) / height;
        // Clicks on borders/title bars do not belong to the client's percentage area.
        return x is >= 0 and <= 1 && y is >= 0 and <= 1 ? new MousePercentage(x, y) : null;
    }

    private delegate IntPtr HookProcedure(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct LowLevelMouseInfo { public Point Point; public uint MouseData, Flags, Time; public IntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int type, HookProcedure callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
}
