using Macro.Views;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using static Macro.Services.ScreenCaptureService;
using SDPoint = System.Drawing.Point;
namespace Macro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly FarmingPage _farmingPage;
        private readonly DailyPage _dailyPage;
        private readonly TimerPage _timerPage;
        private readonly MacroRunOverlay _macroRunOverlay;
        private const int StopMacroHotkeyId = 0x4D52;
        private const uint ModControl = 0x0002, ModShift = 0x0004, ModNoRepeat = 0x4000;
        private const uint VkF10 = 0x79, WmHotkey = 0x0312;
        private bool _stopHotkeyRegistered;
        public MainWindow()
        {
            InitializeComponent();
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri("pack://application:,,,/Assets/app-v2.ico", UriKind.Absolute));

            _farmingPage = new FarmingPage();
            _macroRunOverlay = new MacroRunOverlay();
            _farmingPage.MacroRunStateChanged += OnMacroRunStateChanged;
            _dailyPage = new DailyPage();
            _timerPage = new TimerPage();
            MainFrame.Navigate(_timerPage);
            SourceInitialized += MainWindow_SourceInitialized;
            Closed += MainWindow_Closed;
        }

        private void OnMacroRunStateChanged(bool isRunning)
        {
            if (isRunning) _macroRunOverlay.ShowOverlay();
            else _macroRunOverlay.HideOverlay();
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            _stopHotkeyRegistered = RegisterHotKey(handle, StopMacroHotkeyId, ModControl | ModShift | ModNoRepeat, VkF10);
            _macroRunOverlay.SetStopShortcutAvailable(_stopHotkeyRegistered);
            if (_stopHotkeyRegistered)
                HwndSource.FromHwnd(handle)?.AddHook(HandleHotkeyMessage);
            else
                Debug.WriteLine("Não foi possível registrar o atalho global Ctrl+Shift+F10.");
        }

        private IntPtr HandleHotkeyMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WmHotkey && wParam.ToInt32() == StopMacroHotkeyId)
            {
                _farmingPage.StopCurrentRoutine();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _farmingPage.MacroRunStateChanged -= OnMacroRunStateChanged;
            _farmingPage.StopCurrentRoutine();
            if (_stopHotkeyRegistered)
                UnregisterHotKey(new WindowInteropHelper(this).Handle, StopMacroHotkeyId);
            _macroRunOverlay.Close();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private void BtnFarming_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(_farmingPage);
        }

        private void BtnTimer_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(_timerPage);
        }

        private void BtnDaily_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(_dailyPage);
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            UpdateActiveButton(e.Content);
        }

        private void UpdateActiveButton(object? pagina)
        {
            DeactivateButton(BtnFarming);
            DeactivateButton(BtnTimer);
            DeactivateButton(BtnDaily);

            if (pagina is FarmingPage)
            {
                ActivateButton(BtnFarming);
            }
            else if (pagina is TimerPage)
            {
                ActivateButton(BtnTimer);
            }
            else if (pagina is DailyPage)
            {
                ActivateButton(BtnDaily);
            }
        }

        private static void ActivateButton(Button botao)
        {
            botao.Background = new SolidColorBrush(Color.FromRgb(0x49, 0x3E, 0x30));
            botao.BorderThickness = new Thickness(3, 0, 0, 0);
            botao.BorderBrush = new SolidColorBrush(
                Color.FromRgb(0xE4, 0xC1, 0x8A)
            );
        }

        private static void DeactivateButton(Button botao)
        {
            botao.Background = Brushes.Transparent;
            botao.BorderThickness = new Thickness(0);
            botao.BorderBrush = Brushes.Transparent;
        }
    }
}
