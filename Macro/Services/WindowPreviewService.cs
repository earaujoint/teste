using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Macro.Services;

public sealed record PreviewWindow(nint Handle, int ProcessId, string ProcessName, DateTime StartedAt, string Label);

// Protótipo: copia a área cliente visível da tela. A janela precisa estar desobstruída.
public sealed class WindowPreviewService
{
    public IReadOnlyList<PreviewWindow> ListWindows()
    {
        var windows = new List<PreviewWindow>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id != Environment.ProcessId && process.MainWindowHandle != 0 &&
                        process.ProcessName.Contains("mir4", StringComparison.OrdinalIgnoreCase))
                        windows.Add(new(process.MainWindowHandle, process.Id, process.ProcessName, process.StartTime,
                            $"{process.ProcessName} ({process.Id}) — {process.MainWindowTitle}"));
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        return windows.OrderBy(w => w.Label).ToArray();
    }

    public Task<BitmapSource> CaptureAsync(PreviewWindow target, CancellationToken token) =>
        Task.Run(() => Capture(target, token), token);

    private static BitmapSource Capture(PreviewWindow target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        GetWindowThreadProcessId(target.Handle, out var processId);
        if (!IsWindow(target.Handle) || processId != target.ProcessId)
            throw new InvalidOperationException("A janela foi fechada. Atualize a lista e selecione novamente.");
        if (IsIconic(target.Handle))
            throw new InvalidOperationException("Restaure a janela antes de capturar.");

        // Coordenadas e bitmap em pixels físicos, independentemente do DPI da UI.
        var previousDpi = SetThreadDpiAwarenessContext(new nint(-4));
        nint source = 0, memory = 0, bitmap = 0, previousBitmap = 0;
        try
        {
            if (!GetClientRect(target.Handle, out var rect) || rect.Right <= 0 || rect.Bottom <= 0)
                throw new InvalidOperationException("A janela não possui área cliente disponível.");
            var origin = new NativePoint();
            if (!ClientToScreen(target.Handle, ref origin))
                throw new InvalidOperationException("Não foi possível localizar a janela na tela.");
            source = GetDC(0);
            if (source == 0) throw new InvalidOperationException("Não foi possível acessar a tela.");
            memory = CreateCompatibleDC(source);
            bitmap = CreateCompatibleBitmap(source, rect.Right, rect.Bottom);
            if (memory == 0 || bitmap == 0) throw new InvalidOperationException("Não foi possível criar a captura.");
            previousBitmap = SelectObject(memory, bitmap);
            if (previousBitmap == 0 || previousBitmap == new nint(-1))
                throw new InvalidOperationException("Não foi possível preparar a captura.");
            // Ler da tela captura o conteúdo composto pela GPU, ao contrário do DC da janela,
            // que costuma retornar preto em jogos e outras janelas aceleradas.
            if (!BitBlt(memory, 0, 0, rect.Right, rect.Bottom, source, origin.X, origin.Y, 0x00CC0020))
                throw new InvalidOperationException("A janela não permitiu a captura GDI.");
            token.ThrowIfCancellationRequested();
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            if (previousBitmap != 0 && previousBitmap != new nint(-1)) SelectObject(memory, previousBitmap);
            if (bitmap != 0) DeleteObject(bitmap);
            if (memory != 0) DeleteDC(memory);
            if (source != 0) ReleaseDC(0, source);
            if (previousDpi != 0) SetThreadDpiAwarenessContext(previousDpi);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
}
