using Emgu.CV;
using Emgu.CV.CvEnum;
using Macro.Services;
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
        private CancellationTokenSource? _farmingCts;
        private CancellationTokenSource? _doArenaCts;
        public FarmingPage()
        {
            InitializeComponent();
        }

        private async void BtnDoArena_Click(object sender, RoutedEventArgs e)
        {
            if (_doArenaCts != null) return;
            var cts = new CancellationTokenSource();
            _doArenaCts = cts;
            BtnDoArena.IsEnabled = false;

            try
            {
                await Task.Run(() =>
                {
                    var token = cts.Token;
                    var mir41 = new WindowTarget("Mir4G", "Mir4G[1]");
                    var mir40 = new WindowTarget("Mir4S", "Mir4G[0]");
                    RemoveEnergySave(mir41);
                    RemoveEnergySave(mir40);
                    for (int i = 0; i < 10; i++)
                    {
                        DoArena(mir41, mir40, token);
                    }
                }, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "DoArena interrompido", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _doArenaCts = null;
                BtnDoArena.IsEnabled = true;
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _farmingCts?.Cancel();
        }
        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_farmingCts != null) return;
            using var cts = new CancellationTokenSource();
            _farmingCts = cts;
            BtnStart.IsEnabled = false;

            try
            {
                await Task.Run(async () =>
                {
                    var token = cts.Token;
                    var mir42 = new WindowTarget("Mir4G", "Mir4G[2]");
                    var mir40 = new WindowTarget("Mir4S", "Mir4G[0]");
                    // Validate assets/native runtime before interacting with the game.
                    using (var detector = new Macro.Services.RaidRewardDetector()) { }

                    RemoveEnergySave(mir42);
                    RemoveEnergySave(mir40);
                    DailyDonates(mir42);
                    DailyDonates(mir40);

                    for (int i = 0; i < 2; i++)
                    {
                        await DoNormalRaid(mir40, mir42, token);
                    }
                    await DoBossRaid(mir40, mir42, token);

                    DailyFavoriteMissions(mir42);
                    DailyFavoriteMissions(mir40);

                    //DomiMissions(mir40);
                }, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Automação interrompida", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _farmingCts = null;
                BtnStart.IsEnabled = true;
            }
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
