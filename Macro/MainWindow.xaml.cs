using Emgu.CV;
using Emgu.CV.CvEnum;
using Macro.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        public MainWindow()
        {
            InitializeComponent();

            _farmingPage = new FarmingPage();
            _dailyPage = new DailyPage();
            _timerPage = new TimerPage();
            MainFrame.Navigate(_farmingPage);
        }

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
            botao.BorderThickness = new Thickness(3, 0, 0, 0);
            botao.BorderBrush = new SolidColorBrush(
                Color.FromRgb(0xEB, 0xEA, 0xDC)
            );
        }

        private static void DeactivateButton(Button botao)
        {
            botao.BorderThickness = new Thickness(0);
            botao.BorderBrush = Brushes.Transparent;
        }
    }
}