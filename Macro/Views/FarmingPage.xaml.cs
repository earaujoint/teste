using Macro.Models;
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
        public FarmingConfiguration Configuration { get; private set; } = new();
        public string[] Launchers { get; } = ["MIR4 Launcher 1", "MIR4 Launcher 2", "MIR4 Steam"];
        public IReadOnlyList<RaidConfiguration> NormalRaids { get; private set; } = [];
        public IReadOnlyList<RaidConfiguration> BossRaids { get; private set; } = [];
        private static WindowTarget Target(string launcher) => launcher switch
        {
            "MIR4 Launcher 1" => new("Mir4G", "Mir4G[1]"),
            "MIR4 Launcher 2" => new("Mir4G", "Mir4G[2]"),
            "MIR4 Steam" => new("Mir4S", "Mir4G[0]"),
            _ => throw new InvalidOperationException("Selecione um launcher válido.")
        };
        private static RaidConfiguration Pending(string name) => new() { Name = name, Description = "Seleção desta raide ainda não implementada", IsAvailable = false };
        private void Log(string message) => Dispatcher.Invoke(() =>
        {
            if (ExecutionLog.LineCount > 200) ExecutionLog.Clear();
            ExecutionLog.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
            ExecutionLog.ScrollToEnd();
        });
        private static bool HasErrors(DependencyObject element)
        {
            if (Validation.GetHasError(element)) return true;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
                if (HasErrors(System.Windows.Media.VisualTreeHelper.GetChild(element, i))) return true;
            return false;
        }
        public FarmingPage()
        {
            InitializeComponent();
            try { Configuration = FarmingConfigurationService.Load(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            { Log("Não foi possível carregar as configurações: " + ex.Message); }
            NormalRaids = [Configuration.Normal, Pending("Raide 2"), Pending("Raide 3")];
            BossRaids = [Configuration.Boss, Pending("Boss 2"), Pending("Boss 3")];
            DataContext = this;
            Log("Pronto. Configure as janelas antes de iniciar.");
        }

        private async void BtnDoArena_Click(object sender, RoutedEventArgs e)
        {
            if (_doArenaCts != null || _farmingCts != null) return;
            using var cts = new CancellationTokenSource();
            _doArenaCts = cts;
            BtnDoArena.IsEnabled = false;
            BtnStart.IsEnabled = false;
            StatusText.Text = "Arena em execução";
            Log("Arena iniciada.");

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
            catch (OperationCanceledException) { Log("Execução cancelada."); }
            catch (Exception ex)
            {
                Log(ex.Message);
                MessageBox.Show(ex.Message, "DoArena interrompido", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _doArenaCts = null;
                BtnDoArena.IsEnabled = true;
                BtnStart.IsEnabled = true;
                StatusText.Text = "Pronto";
                Log("Arena encerrada.");
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _farmingCts?.Cancel();
            _doArenaCts?.Cancel();
            Log("Parada solicitada. Aguardando a rotina atual liberar o controle.");
        }
        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_farmingCts != null || _doArenaCts != null) return;
            if (HasErrors(ConfigurationPanel) || !Configuration.Normal.HasValidCount || !Configuration.Boss.HasValidCount) { Log("Corrija a quantidade: use um número inteiro de 1 a 99."); return; }
            if (!Launchers.Contains(Configuration.Starter) || !Launchers.Contains(Configuration.Partner) || Configuration.Starter == Configuration.Partner)
            { Log("Selecione duas janelas diferentes e válidas."); return; }
            try { FarmingConfigurationService.Save(Configuration); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log("Não foi possível salvar: " + ex.Message); return; }
            using var cts = new CancellationTokenSource();
            _farmingCts = cts;
            BtnStart.IsEnabled = false;
            BtnDoArena.IsEnabled = false;
            ConfigurationPanel.IsEnabled = false;
            StatusText.Text = "Em execução";
            Log("Macro iniciado.");

            try
            {
                await Task.Run(async () =>
                {
                    var token = cts.Token;
                    var mir42 = Target(Configuration.Partner);
                    var mir40 = Target(Configuration.Starter);
                    // Validate assets/native runtime before interacting with the game.
                    using (var detector = new Macro.Services.RaidRewardDetector()) { }

                    RemoveEnergySave(mir42);
                    RemoveEnergySave(mir40);
                    DailyDonates(mir42);
                    DailyDonates(mir40);
                    if (Configuration.Normal.IsEnabled)
                        for (int i = 0; i < Configuration.Normal.RepeatCount; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            Log($"Raide normal {i + 1}/{Configuration.Normal.RepeatCount}.");
                            await DoNormalRaid(mir40, mir42, token);
                        }
                    if (Configuration.Boss.IsEnabled)
                        for (int i = 0; i < Configuration.Boss.RepeatCount; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            Log($"Boss raid {i + 1}/{Configuration.Boss.RepeatCount}.");
                            await DoBossRaid(mir40, mir42, token);
                        }
                    if (Configuration.DailyFavorites)
                    {
                        Log("Missões favoritas.");
                        token.ThrowIfCancellationRequested();
                        DailyFavoriteMissions(mir42);
                        token.ThrowIfCancellationRequested();
                        DailyFavoriteMissions(mir40);
                    }
                }, cts.Token);
            }
            catch (OperationCanceledException) { Log("Execução cancelada."); }
            catch (Exception ex)
            {
                Log(ex.Message);
                MessageBox.Show(ex.Message, "Automação interrompida", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _farmingCts = null;
                ConfigurationPanel.IsEnabled = true;
                BtnDoArena.IsEnabled = true;
                StatusText.Text = "Pronto";
                Log("Macro encerrado.");
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
