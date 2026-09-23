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
                                    ClickLinkedPosition(window, otherWindow, button, cancellationToken);
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

    private static void ClickLinkedPosition(IntPtr sourceWindow, IntPtr window, Rectangle sourceButton,
        CancellationToken cancellationToken)
    {
        try
        {
            FocusRewardWindow(window, cancellationToken);
            // Read geometry only after restoration/activation has finished.
            if (!TryGetBounds(window, out var targetBounds))
                throw new InvalidOperationException("Não foi possível obter a área da segunda janela para clicar em OK.");

            Point point = new(targetBounds.X + sourceButton.X + sourceButton.Width / 2,
                targetBounds.Y + sourceButton.Y + sourceButton.Height / 2);
            if (!targetBounds.Contains(point) || !SetCursorPos(point.X, point.Y))
                throw new InvalidOperationException("Não foi possível posicionar o mouse no OK da segunda janela.");

            WaitForInput(300, cancellationToken);
            if (GetForegroundWindow() != window ||
                !TryGetBounds(window, out var currentBounds) || currentBounds != targetBounds ||
                GetAncestor(WindowFromPoint(point), 2) != window)
                throw new InvalidOperationException("A segunda janela perdeu o foco ou o botão OK está encoberto. Clique interrompido.");

            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            try { Thread.Sleep(100); }
            finally { mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); }
            Debug.WriteLine($"Raid recompensa: clique enviado à segunda janela em {point}.");
            WaitForInput(700, cancellationToken);
        }
        finally
        {
            // Return even when the second click fails; do not hide that failure.
            if (IsWindow(sourceWindow))
            {
                if (IsIconic(sourceWindow)) ShowWindow(sourceWindow, 9);
                SetForegroundWindow(sourceWindow);
                Thread.Sleep(350);
            }
        }
    }

    private static void FocusRewardWindow(IntPtr window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsWindow(window)) throw new InvalidOperationException("A segunda janela da raid foi fechada.");
        // SW_RESTORE on an already maximized window can change its geometry.
        if (IsIconic(window)) ShowWindow(window, 9);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            SetForegroundWindow(window);
            WaitForInput(500, cancellationToken);
            if (GetForegroundWindow() != window) continue;
            WaitForInput(500, cancellationToken);
            if (GetForegroundWindow() == window) return;
        }
        throw new InvalidOperationException("Não foi possível ativar a segunda janela para clicar em OK.");
    }

    // Stay on the same OS thread while the caller's DPI context is active.
    private static void WaitForInput(int milliseconds, CancellationToken cancellationToken)
    {
        if (cancellationToken.WaitHandle.WaitOne(milliseconds))
            cancellationToken.ThrowIfCancellationRequested();
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
