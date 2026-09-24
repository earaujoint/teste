using Macro.Models;
using Macro.Services;
using Macro.Services.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using static Macro.Services.MovementService;
using static Macro.Services.ScreenCaptureService;
using SDPoint = System.Drawing.Point;

namespace Macro.Views
{
    public partial class FarmingPage : Page
    {
        private bool _mouseLoopRunning = false;
        private bool _syncingDailyLaunchers;
        private int _normalRaidImageIndex;
        private int _bossRaidImageIndex;

        private void PreviousNormalRaidImage_Click(object sender, RoutedEventArgs e) => ChangeNormalRaidImage(-1);
        private void NextNormalRaidImage_Click(object sender, RoutedEventArgs e) => ChangeNormalRaidImage(1);

        private void ChangeNormalRaidImage(int direction)
        {
            _normalRaidImageIndex = (_normalRaidImageIndex + direction + 2) % 2;
            AnimateCarouselImage(NormalRaidImage, $"pack://application:,,,/Assets/raid-slide-{_normalRaidImageIndex + 1}.png", direction);
            NormalRaidImageCounter.Text = $"{_normalRaidImageIndex + 1} / 2";
        }
        private void PreviousBossRaidImage_Click(object sender, RoutedEventArgs e) => ChangeBossRaidImage(-1);
        private void NextBossRaidImage_Click(object sender, RoutedEventArgs e) => ChangeBossRaidImage(1);
        private void ChangeBossRaidImage(int direction)
        {
            _bossRaidImageIndex = (_bossRaidImageIndex + direction + 2) % 2;
            AnimateCarouselImage(BossRaidImage, $"pack://application:,,,/Assets/boss-slide-{_bossRaidImageIndex + 1}.png", direction);
            BossRaidImageCounter.Text = $"{_bossRaidImageIndex + 1} / 2";
        }
        private static void AnimateCarouselImage(Image image, string source, int direction)
        {
            image.Source = new BitmapImage(new Uri(source));
            image.BeginAnimation(UIElement.OpacityProperty, null);
            var translation = new TranslateTransform();
            image.RenderTransform = translation;
            if (!SystemParameters.ClientAreaAnimation) return;

            var duration = TimeSpan.FromMilliseconds(240);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            image.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.25, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
            translation.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(direction * 18, 0, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
        }

        private CancellationTokenSource? _farmingCts;
        private CancellationTokenSource? _doArenaCts;
        public FarmingConfiguration Configuration { get; private set; } = new();
        public string[] Launchers { get; } = ["MIR4 Launcher 1", "MIR4 Launcher 2", "MIR4 Steam"];
        public string[] GuestLaunchers { get; } = ["Não utilizar", "MIR4 Launcher 1", "MIR4 Launcher 2", "MIR4 Steam"];
        private WindowTarget[] Guests(LauncherGroup group) => new[] { group.Guest1, group.Guest2 }
            .Where(value => value != "Não utilizar").Select(Target).ToArray();

        private void ValidateGroup(LauncherGroup group)
        {
            var starter = Target(group.Starter);
            var guests = Guests(group);
            if (guests.Length == 0)
                throw new ArgumentException("Selecione pelo menos um convidado para a raid.");
            if (guests.Contains(starter) || guests.Distinct().Count() != guests.Length)
                throw new ArgumentException("Starter e convidados devem usar launchers diferentes.");
        }
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
            Configuration.DonationLaunchers ??= ["MIR4 Launcher 1", "MIR4 Launcher 2", "MIR4 Steam"];
            DonateLauncher1.IsChecked = Configuration.DonationLaunchers.Contains("MIR4 Launcher 1");
            DonateLauncher2.IsChecked = Configuration.DonationLaunchers.Contains("MIR4 Launcher 2");
            DonateSteam.IsChecked = Configuration.DonationLaunchers.Contains("MIR4 Steam");
            DataContext = this;
            SyncDailyLauncherChoices();
            Log("Pronto. Configure as janelas antes de iniciar.");
        }

        private void SyncDailyLauncherChoices()
        {
            _syncingDailyLaunchers = true;
            try
            {
                DailyLauncher1.IsChecked = Configuration.DailyLaunchers.Contains("MIR4 Launcher 1");
                DailyLauncher2.IsChecked = Configuration.DailyLaunchers.Contains("MIR4 Launcher 2");
                DailySteam.IsChecked = Configuration.DailyLaunchers.Contains("MIR4 Steam");
            }
            finally { _syncingDailyLaunchers = false; }
        }

        private void UpdateDailyLaunchers()
        {
            if (_syncingDailyLaunchers) return;
            Configuration.DailyLaunchers = new[] { DailyLauncher1, DailyLauncher2, DailySteam }
                .Where(checkBox => checkBox.IsChecked == true)
                .Select(checkBox => (string)checkBox.Tag)
                .ToList();
        }

        private void DailyLauncher_Checked(object sender, RoutedEventArgs e) => UpdateDailyLaunchers();
        private void DailyLauncher_Unchecked(object sender, RoutedEventArgs e) => UpdateDailyLaunchers();

        private void DonationLauncher_Checked(object sender, RoutedEventArgs e) => UpdateDonationLauncher(sender, true);
        private void DonationLauncher_Unchecked(object sender, RoutedEventArgs e) => UpdateDonationLauncher(sender, false);
        private void UpdateDonationLauncher(object sender, bool selected)
        {
            if (sender is not CheckBox checkBox || Configuration.DonationLaunchers is null) return;
            var launcher = checkBox.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(launcher)) return;
            if (selected && !Configuration.DonationLaunchers.Contains(launcher))
                Configuration.DonationLaunchers.Add(launcher);
            else if (!selected)
                Configuration.DonationLaunchers.Remove(launcher);
        }

        private async void BtnDoArena_Click(object sender, RoutedEventArgs e)
        {
            if (_doArenaCts != null || _farmingCts != null) return;
            WindowTarget arenaStarter, arenaInviter;
            try
            {
                arenaStarter = Target(Configuration.ArenaStarter);
                arenaInviter = Target(Configuration.ArenaInviter);
                if (arenaStarter == arenaInviter) throw new ArgumentException("Escolha dois launchers diferentes para a arena.");
                FarmingConfigurationService.Save(Configuration);
            }
            catch (Exception ex) { Log(ex.Message); return; }
            using var cts = new CancellationTokenSource();
            _doArenaCts = cts;
            BtnDoArena.IsEnabled = false;
            BtnStart.IsEnabled = false;
            ConfigurationPanel.IsEnabled = false;
            StatusText.Text = "Arena em execução";
            Log("Arena iniciada.");

            try
            {
                await Task.Run(() =>
                {
                    var token = cts.Token;
                    var mir41 = arenaInviter;
                    var mir40 = arenaStarter;
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
                ConfigurationPanel.IsEnabled = true;
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
            DateTime? scheduledStart = null;
            if (!string.IsNullOrWhiteSpace(Configuration.StartTime))
            {
                if (!DateTime.TryParseExact(Configuration.StartTime.Trim(), "HH:mm", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var startTime))
                {
                    Log("Informe o horário no formato 24h HH:mm, por exemplo 21:30.");
                    return;
                }
                scheduledStart = DateTime.Today.Add(startTime.TimeOfDay);
                if (scheduledStart <= DateTime.Now) scheduledStart = scheduledStart.Value.AddDays(1);
            }
            try
            {
                if (Configuration.Normal.IsEnabled) ValidateGroup(Configuration.NormalLaunchers);
                if (Configuration.Boss.IsEnabled) ValidateGroup(Configuration.BossLaunchers);
                if ((Configuration.DailyDonation || Configuration.BuyDailyScroll) && (Configuration.DonationLaunchers is null || Configuration.DonationLaunchers.Count == 0))
                    throw new ArgumentException("Selecione pelo menos um launcher para as rotinas diárias.");
                if (Configuration.DailyDonation || Configuration.BuyDailyScroll)
                    foreach (var launcher in Configuration.DonationLaunchers) Target(launcher);
                if (Configuration.DailyFavorites)
                {
                    if (Configuration.DailyLaunchers is null || Configuration.DailyLaunchers.Count == 0)
                        throw new ArgumentException("Selecione pelo menos um launcher para as missões diárias.");
                    foreach (var launcher in Configuration.DailyLaunchers) Target(launcher);
                }
            }
            catch (ArgumentException ex) { Log(ex.Message); return; }
            catch (InvalidOperationException ex) { Log(ex.Message); return; }
            try { FarmingConfigurationService.Save(Configuration); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log("Não foi possível salvar: " + ex.Message); return; }
            using var cts = new CancellationTokenSource();
            _farmingCts = cts;
            BtnStart.IsEnabled = false;
            BtnDoArena.IsEnabled = false;
            ConfigurationPanel.IsEnabled = false;
            StatusText.Text = "Em execução";
            if (scheduledStart is DateTime plannedStart)
            {
                StatusText.Text = "Agendado";
                Log($"Macro agendado para {plannedStart:dd/MM HH:mm}. Aguardando horário.");
            }
            else
                Log("Macro iniciado.");

            try
            {
                await Task.Run(async () =>
                {
                    var token = cts.Token;
                    if (scheduledStart is DateTime runAt)
                    {
                        var delay = runAt - DateTime.Now;
                        if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
                        token.ThrowIfCancellationRequested();
                        Dispatcher.Invoke(() => StatusText.Text = "Em execução");
                        Log("Horário atingido. Iniciando macro.");
                    }
                    // Validate assets/native runtime before interacting with the game.
                    if (Configuration.Normal.IsEnabled || Configuration.Boss.IsEnabled)
                        using (var detector = new Macro.Services.RaidRewardDetector()) { }

                    if (Configuration.DailyDonation)
                    {
                        Log("Doação diária.");
                        foreach (var launcher in Configuration.DonationLaunchers)
                        {
                            token.ThrowIfCancellationRequested();
                            Log($"Doação em {launcher}.");
                            DailyDonates(Target(launcher));
                        }
                    }
                    if (Configuration.BuyDailyScroll)
                    {
                        Log("Compra de pergaminho diário.");
                        foreach (var launcher in Configuration.DonationLaunchers)
                        {
                            token.ThrowIfCancellationRequested();
                            Log($"Compra de pergaminho em {launcher}.");
                            BuyDailyScroll(Target(launcher));
                        }
                    }
                    if (Configuration.Normal.IsEnabled)
                        for (int i = 0; i < Configuration.Normal.RepeatCount; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            Log($"Raide normal {i + 1}/{Configuration.Normal.RepeatCount}.");
                            await DoNormalRaid(Target(Configuration.NormalLaunchers.Starter), Guests(Configuration.NormalLaunchers), token);
                        }
                    if (Configuration.Boss.IsEnabled)
                        for (int i = 0; i < Configuration.Boss.RepeatCount; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            Log($"Boss raid {i + 1}/{Configuration.Boss.RepeatCount}.");
                            await DoBossRaid(Target(Configuration.BossLaunchers.Starter), Guests(Configuration.BossLaunchers), token);
                        }
                    if (Configuration.DailyFavorites)
                    {
                        foreach (var launcher in Configuration.DailyLaunchers)
                        {
                            Log($"Missões favoritas em {launcher}.");
                            token.ThrowIfCancellationRequested();
                            DailyFavoriteMissions(Target(launcher));
                        }
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
