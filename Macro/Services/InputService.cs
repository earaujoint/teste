using System.Diagnostics;
using System.Runtime.InteropServices;
using Macro.Services.Models;

namespace Macro.Services;

/// <summary>Windows input directed to one game window.</summary>
public sealed class InputService
{
    private const int ShowMaximized = 3;
    private readonly IntPtr handle;

    public InputService(WindowTarget target)
    {
        var process = Process.GetProcessesByName(target.ProcessName)
            .FirstOrDefault(p => p.MainWindowTitle == target.WindowTitle);
        if (process is null)
            throw new InvalidOperationException($"Janela não encontrada: {target.WindowTitle}");
        handle = process.MainWindowHandle;
    }

    public IntPtr Handle => handle;

    public void Activate()
    {
        ShowWindow(handle, ShowMaximized);
        SetForegroundWindow(handle);
        Thread.Sleep(150);
    }

    public void ClickRelative(double x, double y)
    {
        IntPtr previousDpiContext = SetThreadDpiAwarenessContext(PerMonitorDpiContext);
        try
        {
        if (x > 1) x /= 100;
        if (y > 1) y /= 100;
        GetClientRect(handle, out var rect);
        var point = new Point((int)(rect.Right * x), (int)(rect.Bottom * y));
        ClientToScreen(handle, ref point);
        MoveMouseSmoothly(point, 180);
        Thread.Sleep(300);
        MouseEvent(LeftDown);
        MouseEvent(LeftUp);
        Thread.Sleep(750);
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousDpiContext);
        }
    }

    private static void MoveMouseSmoothly(Point destination, int durationMs)
    {
        GetCursorPos(out Point origin);
        int distance = Math.Abs(destination.X - origin.X) + Math.Abs(destination.Y - origin.Y);
        if (distance < 3)
        {
            SetCursorPos(destination.X, destination.Y);
            return;
        }

        int steps = Math.Clamp(distance / 12, 8, 24);
        int delay = Math.Max(1, durationMs / steps);
        for (int step = 1; step <= steps; step++)
        {
            double progress = (double)step / steps;
            // Smooth start/end: natural acceleration followed by deceleration.
            double eased = progress * progress * (3 - 2 * progress);
            int x = (int)Math.Round(origin.X + (destination.X - origin.X) * eased);
            int y = (int)Math.Round(origin.Y + (destination.Y - origin.Y) * eased);
            SetCursorPos(x, y);
            Thread.Sleep(delay);
        }
    }

    public void Drag(double startX, double startY, double endX, double endY, int durationMs = 500)
    {
        if (startX > 1) startX /= 100; if (startY > 1) startY /= 100;
        if (endX > 1) endX /= 100; if (endY > 1) endY /= 100;
        GetClientRect(handle, out var rect);
        var start = ToScreen((int)(rect.Right * startX), (int)(rect.Bottom * startY));
        var end = ToScreen((int)(rect.Right * endX), (int)(rect.Bottom * endY));
        SetCursorPos(start.X, start.Y); Thread.Sleep(100); MouseEvent(LeftDown);
        int steps = Math.Max(1, durationMs / 10);
        for (int i = 1; i <= steps; i++)
        {
            double progress = (double)i / steps;
            SetCursorPos((int)(start.X + (end.X - start.X) * progress),
                (int)(start.Y + (end.Y - start.Y) * progress));
            Thread.Sleep(5);
        }
        MouseEvent(LeftUp); Thread.Sleep(750);
    }

    public void DragUp() => Drag(50, 90, 50, 30, 600);

    private Point ToScreen(int x, int y) { var p = new Point(x, y); ClientToScreen(handle, ref p); return p; }
    private const uint LeftDown = 0x0002, LeftUp = 0x0004;
    private static readonly IntPtr PerMonitorDpiContext = new(-4);
    private static void MouseEvent(uint flags) => SendMouseEvent(flags, 0, 0, 0, UIntPtr.Zero);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref Point point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    [DllImport("user32.dll", EntryPoint = "mouse_event", SetLastError = true)]
    private static extern void SendMouseEvent(uint flags, uint x, uint y, uint data, UIntPtr extra);
}
