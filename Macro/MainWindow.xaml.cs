using Emgu.CV;
using Emgu.CV.CvEnum;
using System.IO;
using System.Windows;
using SDPoint = System.Drawing.Point;
using static Macro.Services.ScreenCaptureService;
using Macro.Views;
namespace Macro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly FarmingPage _farmingPage;
        private readonly DailyPage _dailyPage;
        public MainWindow()
        {
            InitializeComponent();

            _farmingPage = new FarmingPage();
            _dailyPage = new DailyPage();
            MainFrame.Content = _farmingPage;
        }


        private void BtnFarming_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = _farmingPage;
        }
        private void BtnDaily_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = _dailyPage;
        }

 
    }
}