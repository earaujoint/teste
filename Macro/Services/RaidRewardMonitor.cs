using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Macro.Utils;

namespace Macro.Services;

public static class RaidRewardMonitor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    public static Task WaitAndDismissLinkedAsync(IntPtr starterWindow, IntPtr linkedWindow,
        CancellationToken cancellationToken)
    {
        return WaitAndDismissAsync(starterWindow, cancellationToken, linkedWindow: linkedWindow);
    }

    public static async Task WaitAndDismissAsync(IntPtr window, CancellationToken cancellationToken,
        TimeSpan? timeout = null, TimeSpan? checkInterval = null, IntPtr? linkedWindow = null)
    {
        using var detector = new RaidRewardDetector();
        var timer = Stopwatch.StartNew();
        Rectangle? previous = null;
        int confirmations = 0, attempts = 0, absentFrames = 0;
        bool awaitingDismissal = false;
        TimeSpan lastClick = TimeSpan.Zero;
        while (timer.Elapsed < (timeout ?? TimeSpan.FromMinutes(20)))
        {
            await RaidWindowCoordinator.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
            if (!IsWindow(window)) throw new InvalidOperationException("A janela da raid foi fechada.");
            if (IsIconic(window)) ShowWindow(window, 9);
            if (GetForegroundWindow() != window)
            {
                SetForegroundWindow(window);
                await Task.Delay(350, cancellationToken).ConfigureAwait(false);
            }
            // No await inside the DPI scope: context is local to the current OS thread.
            IntPtr oldDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try
            {
                if (!TryGetBounds(window, out Rectangle bounds))
                {
                    confirmations = 0;
                    absentFrames = 0;
                    previous = null;
                }
                else
                {
                    using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                    using (var graphics = Graphics.FromImage(bitmap))
                        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    using var frame = ImagesUtils.BitmapToMat(bitmap);
                    Rectangle? match = detector.Detect(frame, out double confidence);
                    Debug.WriteLine($"Raid recompensa: confiança={confidence:F3}, confirmações={confirmations}");
                    if (match is Rectangle button)
                    {
                        absentFrames = 0;
                        confirmations = previous is Rectangle last &&
                            Math.Abs(last.X - button.X) <= 6 && Math.Abs(last.Y - button.Y) <= 6 &&
                            Math.Abs(last.Width - button.Width) <= 6 ? confirmations + 1 : 1;
                        previous = button;
                        if (confirmations >= 3 && (!awaitingDismissal || timer.Elapsed - lastClick > TimeSpan.FromSeconds(3)))
                        {
                            if (attempts >= 3) throw new InvalidOperationException("O botão OK continua visível após 3 tentativas.");
                            Point point = new(bounds.X + button.X + button.Width / 2, bounds.Y + button.Y + button.Height / 2);
                            if (TryGetBounds(window, out var current) && current == bounds &&
                                GetAncestor(WindowFromPoint(point), 2) == window && SetCursorPos(point.X, point.Y))
                            {
                                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                                mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                                if (linkedWindow is IntPtr otherWindow)
                                {
                                    ClickLinkedPosition(otherWindow, bounds, button, cancellationToken);
                                }
                                attempts++;
                                awaitingDismissal = true;
                                lastClick = timer.Elapsed;
                                confirmations = 0;
                            }
                        }
                    }
                    else
                    {
                        previous = null;
                        confirmations = 0;
                        if (awaitingDismissal && ++absentFrames >= 3) return;
                    }
                }
            }
            finally { SetThreadDpiAwarenessContext(oldDpi); }
            }
            finally { RaidWindowCoordinator.Gate.Release(); }
            await Task.Delay(checkInterval ?? TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("A tela de recompensa não foi confirmada em 20 minutos. Automação interrompida.");
    }

    private static void ClickLinkedPosition(IntPtr window, Rectangle sourceBounds,
        Rectangle sourceButton, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetBounds(window, out var targetBounds)) return;

        int x = targetBounds.X + (int)Math.Round(
            targetBounds.Width * ((sourceButton.X + sourceButton.Width / 2.0) / sourceBounds.Width));
        int y = targetBounds.Y + (int)Math.Round(
            targetBounds.Height * ((sourceButton.Y + sourceButton.Height / 2.0) / sourceBounds.Height));

        ShowWindow(window, 9);
        SetForegroundWindow(window);
        Thread.Sleep(150);
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    private static bool TryGetBounds(IntPtr window, out Rectangle bounds)
    {
        bounds = default;
        Point origin = default;
        if (IsIconic(window) || !GetClientRect(window, out var client) || !ClientToScreen(window, ref origin)) return false;
        bounds = new Rectangle(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
        return bounds.Width > 0 && bounds.Height > 0;
    }
}
