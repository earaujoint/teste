using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Macro.Views;

public partial class MacroRunOverlay : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint NoSize = 0x0001, NoMove = 0x0002, NoActivate = 0x0010, FrameChanged = 0x0020;

    public MacroRunOverlay()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 18;
        Top = workArea.Top + 18;
    }

    public void ShowOverlay()
    {
        if (!IsVisible) Show();
        // Reapply AFTER WPF Show(), which can update the native window styles.
        ApplyPassiveStyles(new WindowInteropHelper(this).Handle);
    }

    public void HideOverlay()
    {
        if (IsVisible) Hide();
    }

    public void SetStopShortcutAvailable(bool available)
    {
        ShortcutText.Text = available ? "Ctrl + Shift + F10 to stop" : "Atalho indisponível: use Stop no app";
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(FilterActivation);
        ApplyPassiveStyles(handle);
    }

    private static IntPtr FilterActivation(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0021) // WM_MOUSEACTIVATE
        {
            handled = true;
            return new IntPtr(3); // MA_NOACTIVATE
        }
        if (message == 0x0084) // WM_NCHITTEST
        {
            handled = true;
            return new IntPtr(-1); // HTTRANSPARENT
        }
        return IntPtr.Zero;
    }

    private static void ApplyPassiveStyles(IntPtr handle)
    {
        var extendedStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle,
            new IntPtr(extendedStyle | WsExTransparent | WsExNoActivate | WsExToolWindow));
        // WindowFromPoint (used by click validation) skips disabled windows.
        EnableWindow(handle, false);
        // Keep the overlay above other windows without activating it.
        SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, NoSize | NoMove | NoActivate | FrameChanged);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr handle, bool enable);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr newStyle);
}
