using Emgu.CV;
using Emgu.CV.CvEnum;
using Macro.Services.Models;
using Macro.Views;
using Macro.Services.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Macro.Services.MovementService;
using static Macro.Services.ScreenCaptureService;
using SDPoint = System.Drawing.Point;

namespace Macro.Views
{
    public partial class FarmingPage : Page
    {
        private bool _mouseLoopRunning = false;
        public FarmingPage()
        {
            InitializeComponent();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Parado");
        }
        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var mir42 = new WindowTarget("Mir4G", "Mir4G[2]");
            var mir40 = new WindowTarget("Mir4S", "Mir4G[0]");

            await Task.Run(() =>
            {
                RemoveEnergySave(mir42);
                for (int i = 0; i < 3; i++)
                {
                    MacroRaids(mir42);
                }
                MacroBossRaids(mir42);
                DailyFavoriteMissions(mir42);
            });

            await Task.Delay(2000);

            await Task.Run(() =>
            {
                RemoveEnergySave(mir40);
                for (int i = 0; i < 3; i++)
                {
                    MacroRaids(mir40);
                    Thread.Sleep(TimeSpan.FromMinutes(5));
                }
                MacroBossRaids(mir40);
                DailyFavoriteMissions(mir40);
            });
        }

        private void BtnMousePercent_Click(object sender, RoutedEventArgs e)
        {
            if (!_mouseLoopRunning)
            {
                // Inicia loop contínuo
                StartMousePercentageLoop();
                _mouseLoopRunning = true;
                BtnMousePercent.Content = "Stop MousePercent";
            }
            else
            {
                // Para loop contínuo
                StopMousePercentageLoop();
                _mouseLoopRunning = false;
                BtnMousePercent.Content = "MousePercent";
            }
        }
    }
}
