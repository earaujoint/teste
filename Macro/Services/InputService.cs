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
        if (!IsWindow(handle))
            throw new InvalidOperationException("A janela do jogo foi fechada.");
        ShowWindow(handle, ShowMaximized);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            SetForegroundWindow(handle);
            Thread.Sleep(350);
            if (GetForegroundWindow() == handle) return;
        }
        throw new InvalidOperationException("Não foi possível colocar a janela do jogo em primeiro plano; rotina interrompida antes dos cliques.");
    }

    public void ResizeToMinimumAllowed()
    {
        ShowWindow(handle, RestoreWindow);
        Thread.Sleep(250);
        if (!GetWindowRect(handle, out var bounds))
            throw new InvalidOperationException("Não foi possível ler o tamanho atual da janela do jogo.");

        var minimum = new MinMaxInfo();
        SendMessage(handle, GetMinMaxInfo, IntPtr.Zero, ref minimum);
        int width = minimum.MinTrackSize.X;
        int height = minimum.MinTrackSize.Y;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("O jogo não informou o tamanho mínimo permitido da janela.");

        if (!SetWindowPos(handle, IntPtr.Zero, bounds.Left, bounds.Top, width, height, NoZOrder | NoActivate))
            throw new InvalidOperationException("Não foi possível reduzir a janela ao tamanho mínimo permitido.");

        Thread.Sleep(300);
        if (GetWindowRect(handle, out var resized))
            Debug.WriteLine($"Janela do jogo: mínimo solicitado={width}x{height}, aplicado={resized.Right - resized.Left}x{resized.Bottom - resized.Top}.");
    }

    public void ClickRelative(double x, double y)
    {
        IntPtr previousDpiContext = SetThreadDpiAwarenessContext(PerMonitorDpiContext);
        try
        {
            double relativeX = NormalizeRelativeCoordinate(x);
            double relativeY = NormalizeRelativeCoordinate(y);
            if (!IsWindow(handle) || !GetClientRect(handle, out var rect) || rect.Right <= 0 || rect.Bottom <= 0)
                throw new InvalidOperationException("Não foi possível obter a área clicável da janela do jogo.");

            var point = new Point(
                Math.Clamp((int)Math.Round(rect.Right * relativeX), 0, rect.Right - 1),
                Math.Clamp((int)Math.Round(rect.Bottom * relativeY), 0, rect.Bottom - 1));
            if (!ClientToScreen(handle, ref point))
                throw new InvalidOperationException("Não foi possível converter o ponto relativo para a tela.");

            MoveMouseSmoothly(point, 180);
            Thread.Sleep(300);
            if (!GetCursorPos(out var cursor) || Math.Abs(cursor.X - point.X) > 2 || Math.Abs(cursor.Y - point.Y) > 2)
                throw new InvalidOperationException("O cursor não chegou à coordenada solicitada; clique cancelado.");
            if (GetForegroundWindow() != handle || GetAncestor(WindowFromPoint(point), RootWindow) != handle)
                throw new InvalidOperationException("A janela do jogo não está em primeiro plano no ponto do clique; clique cancelado.");

            MouseEvent(LeftDown);
            try { Thread.Sleep(50); }
            finally { MouseEvent(LeftUp); }
            Thread.Sleep(450);
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousDpiContext);
        }
    }

    private static double NormalizeRelativeCoordinate(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "A coordenada precisa ser um número finito.");
        double normalized = value > 1 ? value / 100 : value;
        if (normalized is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "A coordenada relativa deve ficar entre 0% e 100%.");
        return normalized;
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

    public void SendCtrlOne()
    {
        KeyEvent(VkControl, 0, KeyDown, UIntPtr.Zero);
        KeyEvent(VkOne, 0, KeyDown, UIntPtr.Zero);
        KeyEvent(VkOne, 0, KeyUp, UIntPtr.Zero);
        KeyEvent(VkControl, 0, KeyUp, UIntPtr.Zero);
    }

    public void SendEscape()
    {
        KeyEvent(VkEscape, 0, KeyDown, UIntPtr.Zero);
        KeyEvent(VkEscape, 0, KeyUp, UIntPtr.Zero);
    }

    private Point ToScreen(int x, int y) { var p = new Point(x, y); ClientToScreen(handle, ref p); return p; }
    private const uint LeftDown = 0x0002, LeftUp = 0x0004;
    private const int RestoreWindow = 9;
    private const uint GetMinMaxInfo = 0x0024;
    private const uint NoZOrder = 0x0004, NoActivate = 0x0010;
    private const uint RootWindow = 2;
    private const uint KeyDown = 0x0000, KeyUp = 0x0002;
    private const byte VkControl = 0x11, VkOne = 0x31, VkEscape = 0x1B;
    private static readonly IntPtr PerMonitorDpiContext = new(-4);
    private static void MouseEvent(uint flags) => SendMouseEvent(flags, 0, 0, 0, UIntPtr.Zero);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo
    {
        public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint message, IntPtr wParam, ref MinMaxInfo lParam);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref Point point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void KeyEvent(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    [DllImport("user32.dll", EntryPoint = "mouse_event", SetLastError = true)]
    private static extern void SendMouseEvent(uint flags, uint x, uint y, uint data, UIntPtr extra);
}
