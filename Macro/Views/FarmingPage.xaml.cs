using Macro.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Macro.Services;
using System.Globalization;

namespace Macro.Views;

public partial class FarmingPage : Page
{
    private int _normalRaidImageIndex;
    private int _bossRaidImageIndex;
    private CancellationTokenSource? _runCancellation;
    private readonly WindowClickService _windowClickService = new();
    private readonly WindowPreviewService _windowCaptureService = new();
    private readonly TemplateSearchService _templateSearchService = new();

    public FarmingConfiguration Configuration { get; } = new();
    public string[] Launchers { get; } = ["MIR4 Launcher 1", "MIR4 Launcher 2", "MIR4 Steam"];
    public string[] GuestLaunchers { get; } = ["Não utilizar", "MIR4 Launcher 1", "MIR4 Launcher 2", "MIR4 Steam"];
    public IReadOnlyList<RaidConfiguration> NormalRaids { get; }
    public IReadOnlyList<RaidConfiguration> BossRaids { get; }

    public FarmingPage()
    {
        InitializeComponent();
        NormalRaids = [Configuration.Normal, new() { Name = "Raide 2" }, new() { Name = "Raide 3" }];
        BossRaids = [Configuration.Boss, new() { Name = "Boss 2" }, new() { Name = "Boss 3" }];
        DataContext = this;
    }

    private void PreviousNormalRaidImage_Click(object sender, RoutedEventArgs e) => ChangeNormalRaidImage(-1);
    private void NextNormalRaidImage_Click(object sender, RoutedEventArgs e) => ChangeNormalRaidImage(1);
    private void PreviousBossRaidImage_Click(object sender, RoutedEventArgs e) => ChangeBossRaidImage(-1);
    private void NextBossRaidImage_Click(object sender, RoutedEventArgs e) => ChangeBossRaidImage(1);

    private void ChangeNormalRaidImage(int direction)
    {
        _normalRaidImageIndex = (_normalRaidImageIndex + direction + 2) % 2;
        AnimateCarouselImage(NormalRaidImage, $"pack://application:,,,/Assets/raid-slide-{_normalRaidImageIndex + 1}.png", direction);
        NormalRaidImageCounter.Text = $"{_normalRaidImageIndex + 1} / 2";
    }

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

    private void PreviewWindow_Click(object sender, RoutedEventArgs e)
    {
        new WindowPreview { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    // A interface permanece pronta para receber uma nova implementação.
    private void DonationLauncher_Checked(object sender, RoutedEventArgs e) => UpdateLauncherSelection(Configuration.DonationLaunchers, sender, true);
    private void DonationLauncher_Unchecked(object sender, RoutedEventArgs e) => UpdateLauncherSelection(Configuration.DonationLaunchers, sender, false);
    private void DailyLauncher_Checked(object sender, RoutedEventArgs e) => UpdateLauncherSelection(Configuration.DailyLaunchers, sender, true);
    private void DailyLauncher_Unchecked(object sender, RoutedEventArgs e) => UpdateLauncherSelection(Configuration.DailyLaunchers, sender, false);

    private static void UpdateLauncherSelection(List<string> selection, object sender, bool enabled)
    {
        if (sender is not CheckBox { Tag: string launcher }) return;
        if (enabled && !selection.Contains(launcher, StringComparer.OrdinalIgnoreCase)) selection.Add(launcher);
        else if (!enabled) selection.RemoveAll(item => string.Equals(item, launcher, StringComparison.OrdinalIgnoreCase));
    }

    private void DailyDonation_Toggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is not null)
            AppendLog(Configuration.DailyDonation
                ? "Doação diária ativada. Clique em Start para executar nos launchers selecionados."
                : "Doação diária desativada.");
    }

    private void DailyScroll_Toggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is not null)
            AppendLog(Configuration.DailyScroll
                ? "DailyScroll ativado. Clique em Start para executar nos launchers selecionados."
                : "DailyScroll desativado.");
    }

    private void DailyFavorites_Toggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is not null)
            AppendLog(Configuration.DailyFavorites
                ? "Missões favoritas ativadas. Clique em Start para executar nos launchers diários selecionados."
                : "Missões favoritas desativadas.");
    }

    private async void BtnDoArena_Click(object sender, RoutedEventArgs e)
    {
        if (_runCancellation is not null)
        {
            AppendLog("Já existe uma rotina em execução.");
            return;
        }

        _runCancellation = new CancellationTokenSource();
        var cancellationToken = _runCancellation.Token;
        BtnDoArena.IsEnabled = false;
        BtnStart.IsEnabled = false;
        StatusText.Text = "Em execução";
        try
        {
            await DailyArena(cancellationToken);
            AppendLog("DailyArena finalizada.");
        }
        catch (OperationCanceledException) { AppendLog("DailyArena cancelada."); }
        catch (Exception ex) { AppendLog($"DailyArena: falha — {ex.Message}"); }
        finally
        {
            _runCancellation.Dispose();
            _runCancellation = null;
            BtnDoArena.IsEnabled = true;
            BtnStart.IsEnabled = true;
            StatusText.Text = "Pronto";
        }
    }

    private async Task DailyArena(CancellationToken cancellationToken)
    {
        var windows = _windowCaptureService.ListWindows();
        var gameClients = windows.Where(window => window.ProcessName.StartsWith("Mir4G", StringComparison.OrdinalIgnoreCase))
            .OrderBy(window => window.StartedAt).ToArray();
        var targets = new Dictionary<string, PreviewWindow>(StringComparer.OrdinalIgnoreCase);
        if (gameClients.Length > 0) targets["MIR4 Launcher 1"] = gameClients[0];
        if (gameClients.Length > 1) targets["MIR4 Launcher 2"] = gameClients[1];
        var steamWindow = windows.FirstOrDefault(window => window.ProcessName.StartsWith("Mir4S", StringComparison.OrdinalIgnoreCase));
        if (steamWindow is not null) targets["MIR4 Steam"] = steamWindow;

        if (!targets.TryGetValue(Configuration.ArenaStarter, out var starter))
            throw new InvalidOperationException($"starter {Configuration.ArenaStarter} não está aberto.");
        if (!targets.TryGetValue(Configuration.ArenaInviter, out var guest))
            throw new InvalidOperationException($"convidado {Configuration.ArenaInviter} não está aberto.");
        if (ReferenceEquals(starter, guest))
            throw new InvalidOperationException("Selecione launchers diferentes para o starter e o convidado da Arena.");

        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates");
        var templates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ícone +"] = Path.Combine(directory, "daily-arena-plus.png"),
            ["ícone Guerra"] = Path.Combine(directory, "daily-arena-war-icon.png"),
            ["Arena"] = Path.Combine(directory, "daily-arena-label.png"),
            ["Criar Arena (menu)"] = Path.Combine(directory, "daily-arena-create-menu.png"),
            ["Privado"] = Path.Combine(directory, "daily-arena-private.png"),
            ["campo da senha"] = Path.Combine(directory, "daily-arena-password.png"),
            ["botão 2"] = Path.Combine(directory, "daily-arena-two.png"),
            ["Entrada completa"] = Path.Combine(directory, "daily-arena-entry-complete.png"),
            ["Criar Arena"] = Path.Combine(directory, "daily-arena-create.png"),
            ["adicionar convidados"] = Path.Combine(directory, "daily-arena-add.png"),
            ["Convidar todos"] = Path.Combine(directory, "daily-arena-invite-all.png"),
            ["Aceitar convite"] = Path.Combine(directory, "daily-raid-accept.png"),
            ["fechar janela da Arena"] = Path.Combine(directory, "daily-raid-close.png"),
            ["Iniciar Arena"] = Path.Combine(directory, "daily-arena-start.png")
        };
        if (templates.Values.Any(path => !File.Exists(path)))
            throw new InvalidOperationException("Faltam imagens de referência da Arena em Assets\\Templates.");

        var regions = new Dictionary<string, RelativeSearchRegion>(StringComparer.Ordinal)
        {
            ["ícone +"] = new(0.7432, 0.0080, 0.2568, 0.0895),
            ["ícone Guerra"] = new(0.6989, 0.4861, 0.2983, 0.1841),
            ["Arena"] = new(0.6935, 0.6574, 0.3037, 0.1202),
            ["Criar Arena (menu)"] = new(0.5967, 0.8440, 0.4033, 0.1560),
            ["Privado"] = new(0.2622, 0.4631, 0.2580, 0.1815),
            ["campo da senha"] = new(0.6653, 0.5270, 0.0712, 0.0767),
            ["botão 2"] = new(0.3979, 0.2739, 0.2056, 0.3835),
            ["Entrada completa"] = new(0.3724, 0.7673, 0.2553, 0.1662),
            ["Criar Arena"] = new(0.4059, 0.7494, 0.1774, 0.1534),
            ["adicionar convidados"] = new(0.5000, 0.2176, 0.0900, 0.6545),
            ["Convidar todos"] = new(0.4785, 0.1793, 0.1666, 0.1278),
            ["Aceitar convite"] = new(0.0459, 0.4043, 0.1693, 0.1330),
            ["fechar janela da Arena"] = new(0.8534, 0.1051, 0.0873, 0.0946),
            ["Iniciar Arena"] = new(0.4046, 0.8619, 0.2002, 0.1355)
        };

        AppendLog($"DailyArena: starter {Configuration.ArenaStarter}; convidado {Configuration.ArenaInviter}.");
        await _windowClickService.Prepare720pAsync(starter, cancellationToken);
        await _windowClickService.PressCtrlNumberAsync(starter, 0x31, cancellationToken, AppendLog);
        var steps = new[] { "ícone +", "ícone Guerra", "Arena", "Criar Arena (menu)", "Privado", "campo da senha", "botão 2", "Entrada completa", "Criar Arena", "adicionar convidados", "Convidar todos" };
        foreach (var name in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendLog($"DailyArena — starter: procurando {name}.");
            var clickCount = name == "botão 2" ? 4 : 1;
            var threshold = name is "ícone +" or "adicionar convidados" ? 0.50 : name == "botão 2" ? 0.72 : 0.78;
            var found = await FindAndClickTemplateAsync(starter, templates[name], regions[name], null,
                name, threshold, cancellationToken, clickCount: clickCount);
            if (!found) throw new InvalidOperationException($"não foi possível concluir a etapa {name}.");
            if (name == "campo da senha")
                await _windowClickService.ReplaceTextAsync(starter, "0000", cancellationToken, AppendLog);
            await Task.Delay(350, cancellationToken);
        }

        AppendLog($"DailyArena: convite enviado; mudando para {Configuration.ArenaInviter} para aceitar o pedido.");
        await _windowClickService.Prepare720pAsync(guest, cancellationToken);
        await _windowClickService.PressCtrlNumberAsync(guest, 0x31, cancellationToken, AppendLog);
        var accepted = await FindAndClickTemplateAsync(guest, templates["Aceitar convite"],
            regions["Aceitar convite"], null, "Aceitar pedido da Arena", 0.78, cancellationToken);
        if (!accepted) throw new InvalidOperationException($"{Configuration.ArenaInviter}: o pedido da Arena não apareceu.");
        await _windowClickService.ActivateAsync(starter, cancellationToken);
        AppendLog($"DailyArena: pedido aceito em {Configuration.ArenaInviter}; retornando ao starter para fechar a janela da Arena.");
        var closed = await FindAndClickTemplateAsync(starter, templates["fechar janela da Arena"],
            regions["fechar janela da Arena"], null, "fechar janela da Arena", 0.78, cancellationToken);
        if (!closed) throw new InvalidOperationException("não foi possível fechar a janela da Arena pelo X.");
        await Task.Delay(350, cancellationToken);
        var started = await FindAndClickTemplateAsync(starter, templates["Iniciar Arena"],
            regions["Iniciar Arena"], null, "Iniciar Arena", 0.78, cancellationToken);
        if (!started) throw new InvalidOperationException("não foi possível clicar em Iniciar Arena.");
        AppendLog("DailyArena: X clicado e Arena iniciada pelo starter.");
    }
    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _runCancellation?.Cancel();
        AppendLog("Parada solicitada.");
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_runCancellation is not null)
        {
            AppendLog("A rotina já está em execução.");
            return;
        }

        DateTime? scheduledStart = null;
        var requestedTime = Configuration.StartTime?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedTime))
        {
            if (!TimeOnly.TryParseExact(requestedTime, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var startTime))
            {
                AppendLog("Horário inválido. Informe no formato 24 horas HH:mm, por exemplo 21:30.");
                MessageBox.Show("Informe o horário no formato HH:mm (24 horas), por exemplo 21:30.",
                    "Horário inválido", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            scheduledStart = DateTime.Today.Add(startTime.ToTimeSpan());
            if (scheduledStart <= DateTime.Now)
                scheduledStart = scheduledStart.Value.AddDays(1);
        }

        _runCancellation = new CancellationTokenSource();
        var cancellationToken = _runCancellation.Token;
        BtnStart.IsEnabled = false;
        try
        {
            if (scheduledStart is DateTime startAt)
            {
                var delay = startAt - DateTime.Now;
                if (delay > TimeSpan.Zero)
                {
                    StatusText.Text = "Aguardando horário";
                    AppendLog($"Início agendado para {startAt:dd/MM/yyyy HH:mm}. Use Stop para cancelar a espera.");
                    await Task.Delay(delay, cancellationToken);
                }
                AppendLog("Horário atingido; iniciando as rotinas selecionadas.");
            }
            else
                AppendLog("Macro iniciado imediatamente.");

            StatusText.Text = "Em execução";
            if (Configuration.DailyDonation)
                await DailyDonate(cancellationToken);
            if (Configuration.DailyScroll)
                await DailyScroll(cancellationToken);
            if (Configuration.DailyFavorites)
                await DailyFavoriteMissions(cancellationToken);
            if (Configuration.Normal.IsEnabled)
                await DailyFavoriteRaid(cancellationToken);
            if (Configuration.Boss.IsEnabled)
                await DailyRaidBoss(cancellationToken);
            if (!Configuration.DailyDonation && !Configuration.DailyScroll && !Configuration.DailyFavorites &&
                !Configuration.Normal.IsEnabled && !Configuration.Boss.IsEnabled)
                AppendLog("Nenhuma rotina diária está ativada; nenhuma ação executada.");
            AppendLog("Macro finalizado.");
        }
        catch (OperationCanceledException) { AppendLog("Macro cancelado."); }
        catch (Exception ex) { AppendLog($"Erro no macro: {ex.Message}"); }
        finally
        {
            _runCancellation.Dispose();
            _runCancellation = null;
            BtnStart.IsEnabled = true;
            StatusText.Text = "Pronto";
        }
    }

    private async Task DailyDonate(CancellationToken cancellationToken)
    {
        if (Configuration.DonationLaunchers.Count == 0)
        {
            AppendLog("Doação diária: selecione pelo menos um launcher.");
            return;
        }

        var windows = new WindowPreviewService().ListWindows();
        var gameClients = windows.Where(window => window.ProcessName.StartsWith("Mir4G", StringComparison.OrdinalIgnoreCase))
            .OrderBy(window => window.StartedAt).ToArray();
        var steamWindow = windows.FirstOrDefault(window => window.ProcessName.StartsWith("Mir4S", StringComparison.OrdinalIgnoreCase));

        var targets = new Dictionary<string, PreviewWindow>(StringComparer.OrdinalIgnoreCase);
        if (gameClients.Length > 0) targets["MIR4 Launcher 1"] = gameClients[0];
        if (gameClients.Length > 1) targets["MIR4 Launcher 2"] = gameClients[1];
        if (steamWindow is not null) targets["MIR4 Steam"] = steamWindow;

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates", "daily-donate.png");
        var warehouseTemplatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates", "daily-donate-warehouse.png");
        var donateButtonTemplatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates", "daily-donate-button.png");
        var templatesDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates");
        var flowTemplates = Enumerable.Range(1, 12)
            .Select(index => Path.Combine(templatesDirectory, $"daily-flow-{index:D2}-" + new[]
            {
                "cobre", "max", "doar", "doar", "aco-negro", "max", "doar", "doar", "energia", "max", "doar", "doar"
            }[index - 1] + ".png"))
            .ToArray();
        if (new[] { templatePath, warehouseTemplatePath, donateButtonTemplatePath }.Any(path => !File.Exists(path)) ||
            flowTemplates.Any(path => !File.Exists(path)))
        {
            AppendLog("Uma das imagens da sequência diária não foi encontrada na pasta Assets\\Templates.");
            return;
        }

        // Sequência: ícone de doação e depois a opção Armazém nas regiões selecionadas.
        var donationRegion = new RelativeSearchRegion(0.7172, 0.0131, 0.2819, 0.0793);
        var warehouseRegion = new RelativeSearchRegion(0.5578, 0.7367, 0.3910, 0.2097);
        var donateButtonRegion = new RelativeSearchRegion(0.4033, 0.8849, 0.1478, 0.0972);
        const double confidenceThreshold = 0.82;
        CaptureExpander.IsExpanded = true;
        FarmingScroll.ScrollToBottom();
        foreach (var launcher in Launchers.Where(Configuration.DonationLaunchers.Contains))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!targets.TryGetValue(launcher, out var target))
            {
                AppendLog($"{launcher}: processo MIR4 correspondente não está aberto.");
                continue;
            }

            try
            {
                AppendLog($"{launcher}: ajustando a área para 1280×720 e ativando {target.ProcessName} (PID {target.ProcessId}).");
                await _windowClickService.Prepare720pAsync(target, cancellationToken);
                AppendLog($"{launcher}: procurando o ícone na janela {target.ProcessName} (PID {target.ProcessId}).");
                var donationClicked = await FindAndClickTemplateAsync(target, templatePath, donationRegion,
                    new Int32Rect(10, 20, 34, 43), "ícone de doação", confidenceThreshold, cancellationToken);
                if (!donationClicked) continue;

                AppendLog($"{launcher}: doação aberta; procurando Armazém na segunda região.");
                await Task.Delay(350, cancellationToken);
                var warehouseClicked = await FindAndClickTemplateAsync(target, warehouseTemplatePath, warehouseRegion,
                    new Int32Rect(34, 48, 102, 103), "Armazém", confidenceThreshold, cancellationToken);
                if (!warehouseClicked) continue;

                AppendLog($"{launcher}: Armazém selecionado; procurando o botão Doar na terceira região.");
                await Task.Delay(350, cancellationToken);
                var donationMenuOpened = await FindAndClickTemplateAsync(target, donateButtonTemplatePath, donateButtonRegion,
                    new Int32Rect(0, 0, 277, 82), "botão Doar", confidenceThreshold, cancellationToken);
                if (!donationMenuOpened) continue;

                // As três buscas de recurso compartilham a faixa selecionada; depois:
                // recurso → MAX → Doar 1 → Doar 2.
                var resourceListRegion = new RelativeSearchRegion(0.2326, 0.1128, 0.1491, 0.7747);
                var maxRegion = new RelativeSearchRegion(0.3885, 0.6114, 0.3708, 0.1406);
                var donateOneRegion = new RelativeSearchRegion(0.2326, 0.7264, 0.5361, 0.1585);
                var donateTwoRegion = new RelativeSearchRegion(0.2890, 0.2867, 0.4206, 0.4295);
                var flowSteps = new (int Template, RelativeSearchRegion Region, Int32Rect Crop, string Name)[]
                {
                    (0, resourceListRegion, new(0, 0, 220, 125), "Cobre"),
                    (1, maxRegion, new(0, 0, 95, 72), "MAX após Cobre"),
                    (2, donateOneRegion, new(0, 0, 241, 80), "Doar 1 após Cobre"),
                    (3, donateTwoRegion, new(0, 0, 241, 80), "Doar 2 após Cobre"),
                    (4, resourceListRegion, new(0, 0, 214, 115), "Aço Negro"),
                    (5, maxRegion, new(0, 0, 95, 72), "MAX após Aço Negro"),
                    (6, donateOneRegion, new(0, 0, 241, 80), "Doar 1 após Aço Negro"),
                    (7, donateTwoRegion, new(0, 0, 241, 80), "Doar 2 após Aço Negro"),
                    (8, resourceListRegion, new(0, 0, 213, 118), "Energia"),
                    (9, maxRegion, new(0, 0, 95, 72), "MAX após Energia"),
                    (10, donateOneRegion, new(0, 0, 241, 80), "Doar 1 após Energia"),
                    (11, donateTwoRegion, new(0, 0, 241, 80), "Doar 2 após Energia")
                };
                foreach (var step in flowSteps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendLog($"{launcher}: procurando {step.Name} na região X={step.Region.X:P2}, Y={step.Region.Y:P2}, {step.Region.Width:P2} × {step.Region.Height:P2}.");
                    var stepThreshold = step.Name == "Cobre" ? 0.48 : confidenceThreshold;
                    var completed = await FindAndClickTemplateAsync(target, flowTemplates[step.Template], step.Region, step.Crop,
                        step.Name, stepThreshold, cancellationToken);
                    if (!completed)
                    {
                        AppendLog($"{launcher}: sequência interrompida em {step.Name}; os passos seguintes foram ignorados.");
                        break;
                    }
                    await Task.Delay(250, cancellationToken);
                }

                AppendLog($"{launcher}: encerrando a rotina de doação com três pressionamentos de Esc.");
                await _windowClickService.PressEscapeThreeTimesAsync(target, cancellationToken, AppendLog);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { AppendLog($"{launcher}: falha ao clicar — {ex.Message}"); }
        }
    }

    private async Task DailyScroll(CancellationToken cancellationToken)
    {
        if (Configuration.DonationLaunchers.Count == 0)
        {
            AppendLog("DailyScroll: selecione pelo menos um launcher.");
            return;
        }

        var windows = _windowCaptureService.ListWindows();
        var gameClients = windows.Where(window => window.ProcessName.StartsWith("Mir4G", StringComparison.OrdinalIgnoreCase))
            .OrderBy(window => window.StartedAt).ToArray();
        var steamWindow = windows.FirstOrDefault(window => window.ProcessName.StartsWith("Mir4S", StringComparison.OrdinalIgnoreCase));
        var targets = new Dictionary<string, PreviewWindow>(StringComparer.OrdinalIgnoreCase);
        if (gameClients.Length > 0) targets["MIR4 Launcher 1"] = gameClients[0];
        if (gameClients.Length > 1) targets["MIR4 Launcher 2"] = gameClients[1];
        if (steamWindow is not null) targets["MIR4 Steam"] = steamWindow;

        var templatesDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates");
        var mapTemplate = Path.Combine(templatesDirectory, "daily-scroll-map.png");
        var currencyTemplate = Path.Combine(templatesDirectory, "daily-scroll-currency.png");
        var itemTemplate = Path.Combine(templatesDirectory, "daily-scroll-item.png");
        var buyTemplate = Path.Combine(templatesDirectory, "daily-scroll-buy.png");
        var targetTemplate = Path.Combine(templatesDirectory, "daily-scroll-target.png");
        var fiveTemplate = Path.Combine(templatesDirectory, "daily-scroll-five.png");
        var buy150kTemplate = Path.Combine(templatesDirectory, "daily-scroll-150k-buy.png");
        if (new[] { mapTemplate, currencyTemplate, itemTemplate, buyTemplate, targetTemplate, fiveTemplate, buy150kTemplate }.Any(path => !File.Exists(path)))
        {
            AppendLog("DailyScroll: uma das imagens de referência não foi encontrada em Assets\\Templates.");
            return;
        }

        var firstRegion = new RelativeSearchRegion(0.5000, 0.0872, 0.2459, 0.2097);
        var secondRegion = new RelativeSearchRegion(0.3132, 0.0284, 0.3762, 0.2480);
        var thirdRegion = new RelativeSearchRegion(0.3092, 0.8261, 0.3856, 0.1585);
        var buyButtonRegion = new RelativeSearchRegion(0.1574, 0.1512, 0.3265, 0.6187);
        var scrollListRegion = new RelativeSearchRegion(0.6303, 0.1665, 0.3574, 0.8130);
        var quantityFiveRegion = new RelativeSearchRegion(0.2985, 0.4708, 0.4152, 0.1585);
        var buy150kRegion = new RelativeSearchRegion(0.2810, 0.6190, 0.4394, 0.1508);
        var steps = new (string Template, RelativeSearchRegion Region, string Name, double? ClickX, double? ClickY)[]
        {
            (mapTemplate, firstRegion, "ícone de mapa", null, null),
            (currencyTemplate, secondRegion, "ícone dourado", null, null),
            (itemTemplate, thirdRegion, "pergaminho de deslocamento rápido", null, null),
            (buyTemplate, buyButtonRegion, "Comprar", null, null)
        };

        foreach (var launcher in Launchers.Where(Configuration.DonationLaunchers.Contains))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!targets.TryGetValue(launcher, out var target))
            {
                AppendLog($"DailyScroll — {launcher}: processo MIR4 correspondente não está aberto.");
                continue;
            }

            try
            {
                AppendLog($"DailyScroll — {launcher}: ajustando para 1280×720 e iniciando com F10.");
                await _windowClickService.Prepare720pAsync(target, cancellationToken);
                await _windowClickService.PressKeyAsync(target, 0x79, "F10", cancellationToken, AppendLog);
                await Task.Delay(500, cancellationToken);

                var sequenceCompleted = true;
                foreach (var step in steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendLog($"DailyScroll — {launcher}: procurando {step.Name} em X={step.Region.X:P2}, Y={step.Region.Y:P2}, {step.Region.Width:P2} × {step.Region.Height:P2}.");
                    // Recorta apenas o texto fixo do cartão para ignorar a quantidade variável.
                    Int32Rect? crop = step.Name == "pergaminho de deslocamento rápido"
                        ? new Int32Rect(136, 17, 174, 50)
                        : null;
                    var found = await FindAndClickTemplateAsync(target, step.Template, step.Region, crop,
                        step.Name, 0.78, cancellationToken, step.ClickX, step.ClickY);
                    if (!found)
                    {
                        AppendLog($"DailyScroll — {launcher}: sequência interrompida em {step.Name}; os passos seguintes foram ignorados.");
                        sequenceCompleted = false;
                        break;
                    }
                    await Task.Delay(350, cancellationToken);
                }

                if (sequenceCompleted)
                {
                    AppendLog($"DailyScroll — {launcher}: procurando o item por rolagem na região X={scrollListRegion.X:P2}, Y={scrollListRegion.Y:P2}, {scrollListRegion.Width:P2} × {scrollListRegion.Height:P2}.");
                    var targetFound = await FindWhileScrollingAsync(target, targetTemplate, scrollListRegion,
                        "item DailyScroll", 0.78, cancellationToken);
                    if (targetFound)
                    {
                        await Task.Delay(350, cancellationToken);
                        AppendLog($"DailyScroll — {launcher}: selecionando quantidade 5 em X={quantityFiveRegion.X:P2}, Y={quantityFiveRegion.Y:P2}, {quantityFiveRegion.Width:P2} × {quantityFiveRegion.Height:P2}.");
                        var fiveSelected = await FindAndClickTemplateAsync(target, fiveTemplate, quantityFiveRegion, null,
                            "quantidade 5", 0.78, cancellationToken);
                        if (fiveSelected)
                        {
                            await Task.Delay(300, cancellationToken);
                            AppendLog($"DailyScroll — {launcher}: procurando 150K Comprar em X={buy150kRegion.X:P2}, Y={buy150kRegion.Y:P2}, {buy150kRegion.Width:P2} × {buy150kRegion.Height:P2}.");
                            await FindAndClickTemplateAsync(target, buy150kTemplate, buy150kRegion, null,
                                "150K Comprar", 0.78, cancellationToken);
                        }
                        else
                            AppendLog($"DailyScroll — {launcher}: compra interrompida porque a opção 5 não foi encontrada.");
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { AppendLog($"DailyScroll — {launcher}: falha — {ex.Message}"); }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        AppendLog($"DailyScroll — {launcher}: encerrando a rotina com um pressionamento de Esc.");
                        await _windowClickService.PressKeyAsync(target, 0x1B, "Esc", CancellationToken.None, AppendLog);
                    }
                    catch (Exception ex) { AppendLog($"DailyScroll — {launcher}: não foi possível enviar Esc — {ex.Message}"); }
                }
            }
        }
    }

    private async Task DailyFavoriteMissions(CancellationToken cancellationToken)
    {
        if (Configuration.DailyLaunchers.Count == 0)
        {
            AppendLog("Missões favoritas: selecione ao menos um launcher na aba Missões Diárias.");
            return;
        }

        var windows = _windowCaptureService.ListWindows();
        var gameClients = windows.Where(window => window.ProcessName.StartsWith("Mir4G", StringComparison.OrdinalIgnoreCase))
            .OrderBy(window => window.StartedAt).ToArray();
        var steamWindow = windows.FirstOrDefault(window => window.ProcessName.StartsWith("Mir4S", StringComparison.OrdinalIgnoreCase));
        var targets = new Dictionary<string, PreviewWindow>(StringComparer.OrdinalIgnoreCase);
        if (gameClients.Length > 0) targets["MIR4 Launcher 1"] = gameClients[0];
        if (gameClients.Length > 1) targets["MIR4 Launcher 2"] = gameClients[1];
        if (steamWindow is not null) targets["MIR4 Steam"] = steamWindow;

        var templatesDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates");
        var questTemplate = Path.Combine(templatesDirectory, "daily-favorite-quest.png");
        var fieldTemplate = Path.Combine(templatesDirectory, "daily-favorite-field.png");
        var acceptTemplate = Path.Combine(templatesDirectory, "daily-favorite-accept.png");
        var autoTemplate = Path.Combine(templatesDirectory, "daily-favorite-auto.png");
        var checkTemplate = Path.Combine(templatesDirectory, "daily-favorite-check.png");
        var startTemplate = Path.Combine(templatesDirectory, "daily-favorite-start.png");
        var travelTemplate = Path.Combine(templatesDirectory, "daily-favorite-travel.png");
        var travelItemTemplate = Path.Combine(templatesDirectory, "daily-favorite-travel-item.png");
        var plusTemplate = Path.Combine(templatesDirectory, "daily-favorite-plus.png");
        var energyTemplate = Path.Combine(templatesDirectory, "daily-favorite-energy.png");
        if (new[] { questTemplate, fieldTemplate, acceptTemplate, autoTemplate, checkTemplate, startTemplate,
                travelTemplate, travelItemTemplate, plusTemplate, energyTemplate }
            .Any(path => !File.Exists(path)))
        {
            AppendLog("Missões favoritas: não foi encontrada uma das imagens em Assets\\Templates.");
            return;
        }

        var questRegion = new RelativeSearchRegion(0.7432, 0.0003, 0.2568, 0.0997);
        var fieldRegion = new RelativeSearchRegion(0.0002, 0.0412, 0.8384, 0.1457);
        var acceptRegion = new RelativeSearchRegion(0.8279, 0.2841, 0.1721, 0.5778);
        var autoRegion = new RelativeSearchRegion(0.6653, 0.1614, 0.3346, 0.1253);
        var checkRegion = new RelativeSearchRegion(0.1050, 0.0617, 0.2096, 0.2148);
        var startRegion = new RelativeSearchRegion(0.6868, 0.7622, 0.1989, 0.1687);
        var travelRegion = new RelativeSearchRegion(0.6653, 0.4503, 0.2257, 0.2097);
        var travelItemRegion = new RelativeSearchRegion(0.3670, 0.6702, 0.2660, 0.1253);
        var plusRegion = new RelativeSearchRegion(0.7311, 0.0003, 0.2689, 0.1099);
        var energyRegion = new RelativeSearchRegion(0.0109, 0.6676, 0.2996, 0.1662);

        foreach (var launcher in Launchers.Where(Configuration.DailyLaunchers.Contains))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!targets.TryGetValue(launcher, out var target))
            {
                AppendLog($"Missões favoritas — {launcher}: processo MIR4 correspondente não está aberto.");
                continue;
            }

            try
            {
                AppendLog($"Missões favoritas — {launcher}: ajustando a área para 1280×720 e ativando a janela.");
                await _windowClickService.Prepare720pAsync(target, cancellationToken);
                var questOpened = await FindAndClickTemplateAsync(target, questTemplate, questRegion, null,
                    "ícone de missões", 0.78, cancellationToken);
                if (!questOpened) continue;

                await Task.Delay(350, cancellationToken);
                var fieldSelected = await FindAndClickTemplateAsync(target, fieldTemplate, fieldRegion, null,
                    "Campo", 0.78, cancellationToken);
                if (!fieldSelected) continue;

                AppendLog($"Missões favoritas — {launcher}: rolando quinze vezes antes de aceitar as missões.");
                for (var scroll = 1; scroll <= 15; scroll++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _windowClickService.ScrollDownAtRelativeAsync(target, 0.50, 0.55,
                        cancellationToken, AppendLog);
                    await Task.Delay(150, cancellationToken);
                }

                var acceptedCount = 0;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(350, cancellationToken);
                    var attemptName = $"Aceitar missão ({acceptedCount + 1})";
                    AppendLog($"Missões favoritas — {launcher}: procurando outro botão Aceitar missão.");
                    var accepted = await FindAndClickTemplateAsync(target, acceptTemplate, acceptRegion,
                        new Int32Rect(8, 14, 158, 52),
                        attemptName, 0.78, cancellationToken, timeoutOverride: TimeSpan.FromSeconds(2));
                    if (!accepted)
                    {
                        AppendLog($"Missões favoritas — {launcher}: não há mais botão Aceitar missão após {acceptedCount} clique(s); continuando a sequência.");
                        break;
                    }
                    acceptedCount++;
                    AppendLog($"Missões favoritas — {launcher}: clique {acceptedCount} em Aceitar missão concluído.");
                }

                await Task.Delay(300, cancellationToken);
                var favoriteSteps = new (string Template, RelativeSearchRegion Region, string Name)[]
                {
                    (autoTemplate, autoRegion, "Jogar autom."),
                    (checkTemplate, checkRegion, "confirmação da missão"),
                    (startTemplate, startRegion, "Iniciar"),
                    (travelTemplate, travelRegion, "Deslocamento rápido"),
                    (travelItemTemplate, travelItemRegion, "item Deslocamento rápido")
                };
                foreach (var step in favoriteSteps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendLog($"Missões favoritas — {launcher}: procurando {step.Name} em X={step.Region.X:P2}, Y={step.Region.Y:P2}, {step.Region.Width:P2} × {step.Region.Height:P2}.");
                    Int32Rect? crop = step.Name == "item Deslocamento rápido"
                        ? new Int32Rect(113, 18, 145, 40)
                        : null;
                    var completed = await FindAndClickTemplateAsync(target, step.Template, step.Region, crop,
                        step.Name, 0.78, cancellationToken);
                    if (!completed)
                    {
                        AppendLog($"Missões favoritas — {launcher}: sequência interrompida em {step.Name}.");
                        break;
                    }
                    await Task.Delay(300, cancellationToken);
                }

            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { AppendLog($"Missões favoritas — {launcher}: falha — {ex.Message}"); }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        AppendLog($"Missões favoritas — {launcher}: iniciando encerramento com + e Poupança de energia.");
                        var finishSteps = new (string Template, RelativeSearchRegion Region, string Name)[]
                        {
                            (plusTemplate, plusRegion, "ícone +"),
                            (energyTemplate, energyRegion, "Poupança de energia")
                        };
                        var finishStepsCompleted = true;
                        foreach (var step in finishSteps)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            AppendLog($"Missões favoritas — {launcher}: etapa final {step.Name} em X={step.Region.X:P2}, Y={step.Region.Y:P2}, {step.Region.Width:P2} × {step.Region.Height:P2}.");
                            var isPlusButton = step.Name == "ícone +";
                            // A imagem de referência do + inclui um selo N variável; comparar só o
                            // círculo evita que o selo determine o ponto de clique.
                            Int32Rect? finishCrop = isPlusButton ? new Int32Rect(8, 34, 80, 53) : null;
                            var completed = await FindAndClickTemplateAsync(target, step.Template, step.Region, finishCrop,
                                step.Name, isPlusButton ? 0.50 : 0.78, cancellationToken);
                            if (!completed)
                            {
                                AppendLog($"Missões favoritas — {launcher}: etapa final interrompida em {step.Name}.");
                                finishStepsCompleted = false;
                                break;
                            }
                            await Task.Delay(300, cancellationToken);
                        }

                        if (finishStepsCompleted)
                            await _windowClickService.Prepare720pAsync(target, cancellationToken, AppendLog);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { AppendLog($"Missões favoritas — {launcher}: falha no encerramento — {ex.Message}"); }
                }
            }
        }
    }

    private async Task DailyFavoriteRaid(CancellationToken cancellationToken)
    {
        if (!int.TryParse(Configuration.Normal.RepeatCountText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var raidCount) || raidCount is < 1 or > 100)
        {
            AppendLog("DailyFavoriteRaid: informe entre 1 e 100 repetições na configuração da Raide Normal.");
            return;
        }

        var launcherGroup = Configuration.NormalLaunchers;
        var windows = _windowCaptureService.ListWindows();
        var gameClients = windows.Where(window => window.ProcessName.StartsWith("Mir4G", StringComparison.OrdinalIgnoreCase))
            .OrderBy(window => window.StartedAt).ToArray();
        var targets = new Dictionary<string, PreviewWindow>(StringComparer.OrdinalIgnoreCase);
        if (gameClients.Length > 0) targets["MIR4 Launcher 1"] = gameClients[0];
        if (gameClients.Length > 1) targets["MIR4 Launcher 2"] = gameClients[1];
        var steamWindow = windows.FirstOrDefault(window => window.ProcessName.StartsWith("Mir4S", StringComparison.OrdinalIgnoreCase));
        if (steamWindow is not null) targets["MIR4 Steam"] = steamWindow;

        if (!targets.TryGetValue(launcherGroup.Starter, out var starter))
        {
            AppendLog($"DailyFavoriteRaid: starter {launcherGroup.Starter} não está aberto.");
            return;
        }

        var guests = new[] { launcherGroup.Guest1, launcherGroup.Guest2 }
            .Where(name => !string.IsNullOrWhiteSpace(name) && name != "Não utilizar" &&
                !string.Equals(name, launcherGroup.Starter, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var guest in guests)
        {
            if (targets.ContainsKey(guest))
                AppendLog($"DailyFavoriteRaid: {guest} está aberto e será incluído pelo comando Convidar todos.");
            else
                AppendLog($"DailyFavoriteRaid: launcher convidado {guest} não está aberto; continuando com os que estiverem disponíveis.");
        }

        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates");
        var templates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ícone +"] = Path.Combine(directory, "daily-raid-plus.png"),
            ["ícone Raide"] = Path.Combine(directory, "daily-raid-icon.png"),
            ["opção Raide"] = Path.Combine(directory, "daily-raid-label.png"),
            ["Criar um Raide (menu)"] = Path.Combine(directory, "daily-raid-create-banner.png"),
            ["Privado"] = Path.Combine(directory, "daily-raid-private.png"),
            ["campo da senha"] = Path.Combine(directory, "daily-raid-password.png"),
            ["botão 2"] = Path.Combine(directory, "daily-raid-two.png"),
            ["Entrada completa"] = Path.Combine(directory, "daily-raid-entry-complete.png"),
            ["Criar um Raide"] = Path.Combine(directory, "daily-raid-create.png"),
            ["adicionar convidados"] = Path.Combine(directory, "daily-raid-add.png"),
            ["Convidar todos"] = Path.Combine(directory, "daily-raid-invite-all.png"),
            ["Aceitar convite"] = Path.Combine(directory, "daily-raid-accept.png"),
            ["Entrar na raide"] = Path.Combine(directory, "daily-raid-enter.png"),
            ["Iniciar Raide"] = Path.Combine(directory, "daily-raid-start.png"),
            ["fechar janela da raide"] = Path.Combine(directory, "daily-raid-close.png"),
            ["OK"] = Path.Combine(directory, "daily-raid-ok.png")
        };
        if (templates.Values.Any(path => !File.Exists(path)))
        {
            AppendLog("DailyFavoriteRaid: faltam imagens em Assets\\Templates\\daily-raid-*.png.");
            return;
        }

        var regions = new Dictionary<string, RelativeSearchRegion>(StringComparer.Ordinal)
        {
            ["ícone +"] = new(0.6500, 0.0000, 0.3500, 0.2000),
            ["ícone Raide"] = new(0.7110, 0.5040, 0.1962, 0.1508),
            ["opção Raide"] = new(0.6841, 0.6446, 0.3159, 0.1253),
            ["Criar um Raide (menu)"] = new(0.5363, 0.8440, 0.4488, 0.1483),
            ["Privado"] = new(0.2581, 0.4503, 0.2580, 0.1841),
            ["campo da senha"] = new(0.6088, 0.4912, 0.1384, 0.1227),
            ["botão 2"] = new(0.3992, 0.2637, 0.2002, 0.3861),
            ["Entrada completa"] = new(0.4019, 0.7852, 0.2042, 0.1253),
            ["Criar um Raide"] = new(0.4046, 0.7597, 0.1962, 0.1432),
            ["adicionar convidados"] = new(0.1560, 0.3148, 0.3467, 0.1330),
            ["Convidar todos"] = new(0.4624, 0.1946, 0.1666, 0.0946),
            ["Aceitar convite"] = new(0.0459, 0.4043, 0.1693, 0.1330),
            ["Entrar na raide"] = new(0.3603, 0.5756, 0.2929, 0.1585),
            ["Iniciar Raide"] = new(0.5054, 0.7699, 0.3561, 0.1508),
            ["fechar janela da raide"] = new(0.8184, 0.1230, 0.1209, 0.0767),
            ["OK"] = new(0.2850, 0.8338, 0.4488, 0.1355)
        };

        AppendLog($"DailyFavoriteRaid: criando {raidCount} raide(s) pelo starter {launcherGroup.Starter}.");
        try
        {
            await _windowClickService.Prepare720pAsync(starter, cancellationToken);
            for (var raid = 1; raid <= raidCount; raid++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendLog($"DailyFavoriteRaid — {launcherGroup.Starter}: iniciando raide {raid}/{raidCount}.");
                await _windowClickService.PressCtrlNumberAsync(starter, 0x31, cancellationToken, AppendLog);
                var steps = new[] { "ícone +", "ícone Raide", "opção Raide", "Criar um Raide (menu)", "Privado", "campo da senha", "botão 2", "Entrada completa", "Criar um Raide", "adicionar convidados", "Convidar todos" };
                foreach (var name in steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendLog($"DailyFavoriteRaid — raide {raid}/{raidCount}: procurando {name}.");
                    var clicks = name == "botão 2" ? 4 : 1;
                    var threshold = name switch
                    {
                        "botão 2" => 0.72,
                        "ícone +" or "adicionar convidados" => 0.50,
                        _ => 0.78
                    };
                    var found = await FindAndClickTemplateAsync(starter, templates[name], regions[name], null,
                        name, threshold, cancellationToken, clickCount: clicks);
                    if (!found)
                        throw new InvalidOperationException($"sequência interrompida em {name} (raide {raid}/{raidCount}).");
                    if (name == "campo da senha")
                        await _windowClickService.ReplaceTextAsync(starter, "0000", cancellationToken, AppendLog);
                    await Task.Delay(350, cancellationToken);
                }
                AppendLog($"DailyFavoriteRaid — raide {raid}/{raidCount}: convites enviados pelo comando Convidar todos.");

                foreach (var guestName in guests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!targets.TryGetValue(guestName, out var guest)) continue;
                    AppendLog($"DailyFavoriteRaid — raide {raid}/{raidCount}: ativando launcher convidado {guestName}.");
                    await _windowClickService.Prepare720pAsync(guest, cancellationToken);
                    await _windowClickService.PressCtrlNumberAsync(guest, 0x31, cancellationToken, AppendLog);
                    foreach (var guestStep in new[] { "Aceitar convite", "Entrar na raide" })
                    {
                        var guestStepFound = await FindAndClickTemplateAsync(guest, templates[guestStep], regions[guestStep],
                            null, guestStep, 0.78, cancellationToken);
                        if (!guestStepFound)
                            throw new InvalidOperationException($"{guestName}: não foi possível concluir {guestStep}.");
                        await Task.Delay(350, cancellationToken);
                    }
                }

                await _windowClickService.ActivateAsync(starter, cancellationToken);
                var closed = await FindAndClickTemplateAsync(starter, templates["fechar janela da raide"],
                    regions["fechar janela da raide"], null, "fechar janela da raide", 0.78, cancellationToken);
                if (!closed)
                    throw new InvalidOperationException($"não foi possível localizar o X antes de iniciar a raide {raid}/{raidCount}.");
                await Task.Delay(350, cancellationToken);

                var started = await FindAndClickTemplateAsync(starter, templates["Iniciar Raide"],
                    regions["Iniciar Raide"], null, "Iniciar Raide", 0.78, cancellationToken);
                if (!started)
                    throw new InvalidOperationException($"não foi possível iniciar a raide {raid}/{raidCount} no starter.");
                AppendLog($"DailyFavoriteRaid — raide {raid}/{raidCount}: iniciada pelo starter {launcherGroup.Starter}.");
                await WaitAndClickTemplateIndefinitelyAsync(starter, templates["OK"], regions["OK"],
                    cancellationToken);

                foreach (var guestName in guests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!targets.TryGetValue(guestName, out var guest)) continue;
                    AppendLog($"DailyFavoriteRaid — OK do starter clicado; mudando para {guestName} para procurar o OK na mesma região.");
                    await _windowClickService.Prepare720pAsync(guest, cancellationToken);
                    await WaitAndClickTemplateIndefinitelyAsync(guest, templates["OK"], regions["OK"],
                        cancellationToken);
                    AppendLog($"DailyFavoriteRaid — OK clicado em {guestName}.");
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { AppendLog($"DailyFavoriteRaid — {launcherGroup.Starter}: {ex.Message}"); }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    AppendLog($"DailyFavoriteRaid — {launcherGroup.Starter}: rotina encerrada.");
                }
                catch (Exception ex) { AppendLog($"DailyFavoriteRaid — falha ao enviar Esc 2x: {ex.Message}"); }
            }
        }
    }

    private async Task DailyRaidBoss(CancellationToken cancellationToken)
    {
        if (!int.TryParse(Configuration.Boss.RepeatCountText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var raidCount) || raidCount is < 1 or > 100)
        {
            AppendLog("DailyRaidBoss: informe entre 1 e 100 repetições na configuração de Raids Boss.");
            return;
        }

        var launcherGroup = Configuration.BossLaunchers;
        var windows = _windowCaptureService.ListWindows();
        var gameClients = windows.Where(window => window.ProcessName.StartsWith("Mir4G", StringComparison.OrdinalIgnoreCase))
            .OrderBy(window => window.StartedAt).ToArray();
        var targets = new Dictionary<string, PreviewWindow>(StringComparer.OrdinalIgnoreCase);
        if (gameClients.Length > 0) targets["MIR4 Launcher 1"] = gameClients[0];
        if (gameClients.Length > 1) targets["MIR4 Launcher 2"] = gameClients[1];
        var steamWindow = windows.FirstOrDefault(window => window.ProcessName.StartsWith("Mir4S", StringComparison.OrdinalIgnoreCase));
        if (steamWindow is not null) targets["MIR4 Steam"] = steamWindow;

        if (!targets.TryGetValue(launcherGroup.Starter, out var starter))
        {
            AppendLog($"DailyRaidBoss: starter {launcherGroup.Starter} não está aberto.");
            return;
        }

        var guests = new[] { launcherGroup.Guest1, launcherGroup.Guest2 }
            .Where(name => !string.IsNullOrWhiteSpace(name) && name != "Não utilizar" &&
                !string.Equals(name, launcherGroup.Starter, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates");
        var templates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ícone +"] = Path.Combine(directory, "daily-raid-boss-plus.png"),
            ["ícone Raide"] = Path.Combine(directory, "daily-raid-boss-icon.png"),
            ["Raide de Boss"] = Path.Combine(directory, "daily-raid-boss-label.png"),
            ["Criar um Raide (menu)"] = Path.Combine(directory, "daily-raid-boss-menu-create.png"),
            ["Criar um Raide"] = Path.Combine(directory, "daily-raid-boss-create.png"),
            ["adicionar convidados"] = Path.Combine(directory, "daily-raid-add.png"),
            ["Convidar todos"] = Path.Combine(directory, "daily-raid-invite-all.png"),
            ["Aceitar convite"] = Path.Combine(directory, "daily-raid-accept.png"),
            ["Entrar na raide"] = Path.Combine(directory, "daily-raid-enter.png"),
            ["OK"] = Path.Combine(directory, "daily-raid-ok.png")
        };
        if (templates.Values.Any(path => !File.Exists(path)))
        {
            AppendLog("DailyRaidBoss: faltam imagens em Assets\\Templates.");
            return;
        }

        var regions = new Dictionary<string, RelativeSearchRegion>(StringComparer.Ordinal)
        {
            ["ícone +"] = new(0.7392, 0.0029, 0.2608, 0.0946),
            ["ícone Raide"] = new(0.6868, 0.4989, 0.2969, 0.1739),
            ["Raide de Boss"] = new(0.6935, 0.6600, 0.2996, 0.1227),
            ["Criar um Raide (menu)"] = new(0.5282, 0.8134, 0.4703, 0.1866),
            ["Criar um Raide"] = new(0.3535, 0.7418, 0.2916, 0.1636),
            ["adicionar convidados"] = new(0.1560, 0.3148, 0.3467, 0.1330),
            ["Convidar todos"] = new(0.4624, 0.1946, 0.1666, 0.0946),
            ["Aceitar convite"] = new(0.0459, 0.4043, 0.1693, 0.1330),
            ["Entrar na raide"] = new(0.3603, 0.5756, 0.2929, 0.1585),
            ["OK"] = new(0.2850, 0.8338, 0.4488, 0.1355)
        };

        AppendLog($"DailyRaidBoss: criando {raidCount} raide(s) pelo starter {launcherGroup.Starter}.");
        try
        {
            await _windowClickService.Prepare720pAsync(starter, cancellationToken);
            for (var raid = 1; raid <= raidCount; raid++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendLog($"DailyRaidBoss — {launcherGroup.Starter}: iniciando raide {raid}/{raidCount}.");
                await _windowClickService.PressCtrlNumberAsync(starter, 0x31, cancellationToken, AppendLog);
                foreach (var name in new[] { "ícone +", "ícone Raide", "Raide de Boss", "Criar um Raide (menu)", "Criar um Raide", "adicionar convidados", "Convidar todos" })
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendLog($"DailyRaidBoss — raide {raid}/{raidCount}: procurando {name}.");
                    var threshold = name is "ícone +" or "adicionar convidados" ? 0.50 : 0.78;
                    var found = await FindAndClickTemplateAsync(starter, templates[name], regions[name], null,
                        name, threshold, cancellationToken);
                    if (!found)
                        throw new InvalidOperationException($"sequência interrompida em {name} (raide {raid}/{raidCount}).");
                    await Task.Delay(350, cancellationToken);
                }

                foreach (var guestName in guests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!targets.TryGetValue(guestName, out var guest))
                    {
                        AppendLog($"DailyRaidBoss — launcher convidado {guestName} não está aberto; pulando esse launcher.");
                        continue;
                    }

                    AppendLog($"DailyRaidBoss — raide {raid}/{raidCount}: aceitando convite em {guestName}.");
                    await _windowClickService.Prepare720pAsync(guest, cancellationToken);
                    await _windowClickService.PressCtrlNumberAsync(guest, 0x31, cancellationToken, AppendLog);
                    foreach (var guestStep in new[] { "Aceitar convite", "Entrar na raide" })
                    {
                        var guestStepFound = await FindAndClickTemplateAsync(guest, templates[guestStep],
                            regions[guestStep], null, guestStep, 0.78, cancellationToken);
                        if (!guestStepFound)
                            throw new InvalidOperationException($"{guestName}: não foi possível concluir {guestStep}.");
                        await Task.Delay(350, cancellationToken);
                    }
                }

                await _windowClickService.ActivateAsync(starter, cancellationToken);
                AppendLog($"DailyRaidBoss — convidados processados; retornando ao starter {launcherGroup.Starter} para aguardar o OK.");
                await WaitAndClickTemplateIndefinitelyAsync(starter, templates["OK"], regions["OK"],
                    cancellationToken, "DailyRaidBoss");
                foreach (var guestName in guests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!targets.TryGetValue(guestName, out var guest)) continue;
                    AppendLog($"DailyRaidBoss — starter confirmou o OK; aguardando o mesmo OK em {guestName}.");
                    await _windowClickService.Prepare720pAsync(guest, cancellationToken);
                    await WaitAndClickTemplateIndefinitelyAsync(guest, templates["OK"], regions["OK"],
                        cancellationToken, "DailyRaidBoss");
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { AppendLog($"DailyRaidBoss — {launcherGroup.Starter}: {ex.Message}"); }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
                AppendLog($"DailyRaidBoss — {launcherGroup.Starter}: rotina encerrada.");
        }
    }

    private async Task WaitAndClickTemplateIndefinitelyAsync(PreviewWindow target, string templatePath,
        RelativeSearchRegion region, CancellationToken cancellationToken, string routineName = "DailyFavoriteRaid")
    {
        AppendLog($"{routineName} — aguardando o botão OK sem limite de tempo na região X={region.X:P2}, Y={region.Y:P2}, {region.Width:P2} × {region.Height:P2}. Use Stop para cancelar.");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        var consecutiveMatches = 0;
        OpenCvSharp.Rect? previousMatch = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = await _windowCaptureService.CaptureAsync(target, cancellationToken);
            var result = await _templateSearchService.FindAsync(frame, templatePath, region, 0.78,
                cancellationToken);
            ScreenshotImage.Source = result.AnnotatedFrame;
            var stablePosition = previousMatch is { } previous &&
                Math.Abs(previous.X + previous.Width / 2d - result.Bounds.X - result.Bounds.Width / 2d) <= Math.Max(4, result.Bounds.Width * 0.2) &&
                Math.Abs(previous.Y + previous.Height / 2d - result.Bounds.Y - result.Bounds.Height / 2d) <= Math.Max(4, result.Bounds.Height * 0.2);
            consecutiveMatches = result.Found ? (stablePosition ? consecutiveMatches + 1 : 1) : 0;
            previousMatch = result.Found ? result.Bounds : null;

            if (consecutiveMatches >= 3)
            {
                var clickX = result.Bounds.X + result.Bounds.Width / 2d;
                var clickY = result.Bounds.Y + result.Bounds.Height / 2d;
                AppendLog($"{routineName}: OK confirmado ({result.Confidence:0.00}); movendo o mouse e clicando.");
                var click = await _windowClickService.ClickRelativeAsync(target, clickX / frame.PixelWidth,
                    clickY / frame.PixelHeight, cancellationToken, AppendLog);
                AppendLog($"{routineName}: clique em OK enviado em X={click.X}, Y={click.Y}; Windows aceitou a entrada.");
                return;
            }

            if (watch.Elapsed - lastReport >= TimeSpan.FromSeconds(10))
            {
                AppendLog($"{routineName}: OK ainda não apareceu ({watch.Elapsed.TotalMinutes:0} min; melhor confiança recente {result.Confidence:0.00}/0.78).");
                lastReport = watch.Elapsed;
            }
            await Task.Delay(250, cancellationToken);
        }
    }

    private async Task<bool> FindWhileScrollingAsync(PreviewWindow target, string templatePath,
        RelativeSearchRegion region, string targetName, double threshold, CancellationToken cancellationToken)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var consecutiveMatches = 0;
        var scrollCount = 0;
        var bestConfidence = 0d;
        OpenCvSharp.Rect? previousMatch = null;
        var scrollX = region.X + region.Width / 2;
        var scrollY = region.Y + region.Height / 2;

        while (timer.Elapsed < TimeSpan.FromSeconds(35) && scrollCount <= 50)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = await _windowCaptureService.CaptureAsync(target, cancellationToken);
            var result = await _templateSearchService.FindAsync(frame, templatePath, region, threshold, cancellationToken);
            ScreenshotImage.Source = result.AnnotatedFrame;
            bestConfidence = Math.Max(bestConfidence, result.Confidence);
            var stablePosition = previousMatch is { } previous &&
                Math.Abs(previous.X + previous.Width / 2d - result.Bounds.X - result.Bounds.Width / 2d) <= Math.Max(4, result.Bounds.Width * 0.2) &&
                Math.Abs(previous.Y + previous.Height / 2d - result.Bounds.Y - result.Bounds.Height / 2d) <= Math.Max(4, result.Bounds.Height * 0.2);
            consecutiveMatches = result.Found ? (stablePosition ? consecutiveMatches + 1 : 1) : 0;
            previousMatch = result.Found ? result.Bounds : null;
            AppendLog($"DailyScroll — PID {target.ProcessId}, {targetName}: confiança {result.Confidence:0.00}/{threshold:0.00}, confirmação {consecutiveMatches}/3, rolagens {scrollCount}.");

            if (consecutiveMatches >= 3)
            {
                var clickX = Math.Min(result.Bounds.X + result.Bounds.Width * 1.20, frame.PixelWidth - 1);
                var clickY = Math.Clamp(result.Bounds.Y + result.Bounds.Height / 2d, 0, frame.PixelHeight - 1);
                AppendLog($"{targetName} encontrado após {scrollCount} rolagens; clicando cerca de 20% da largura além da borda direita do item.");
                var click = await _windowClickService.ClickRelativeAsync(target, clickX / frame.PixelWidth,
                    clickY / frame.PixelHeight, cancellationToken, AppendLog);
                AppendLog($"Windows aceitou o clique do item em X={click.X}, Y={click.Y}.");
                return true;
            }

            if (!result.Found)
            {
                var nearTarget = result.Confidence >= threshold - 0.18;
                var notches = nearTarget ? 1 : 2;
                await _windowClickService.ScrollDownAtRelativeAsync(target, scrollX, scrollY,
                    cancellationToken, AppendLog, notches);
                scrollCount++;
            }
            var nearTargetNow = result.Confidence >= threshold - 0.18;
            await Task.Delay(result.Found ? 100 : nearTargetNow ? 90 : 36, cancellationToken);
        }

        AppendLog($"DailyScroll: item não encontrado na região após {scrollCount} rolagens. Melhor confiança: {bestConfidence:0.00}; mínimo: {threshold:0.00}. Nenhum clique foi enviado.");
        return false;
    }

    private async Task<bool> FindAndClickTemplateAsync(PreviewWindow target, string templatePath,
        RelativeSearchRegion region, Int32Rect? templateCrop, string targetName, double threshold,
        CancellationToken cancellationToken, double? clickAnchorX = null, double? clickAnchorY = null,
        TimeSpan? timeoutOverride = null, int clickCount = 1)
    {
        var searchTime = System.Diagnostics.Stopwatch.StartNew();
        var consecutiveMatches = 0;
        var bestConfidence = 0d;
        OpenCvSharp.Rect? previousMatch = null;

        var timeout = timeoutOverride ?? (targetName == "Cobre" ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(12));
        while (searchTime.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = await _windowCaptureService.CaptureAsync(target, cancellationToken);
            var result = await _templateSearchService.FindAsync(frame, templatePath, region, threshold,
                cancellationToken, templateCrop);
            ScreenshotImage.Source = result.AnnotatedFrame;
            bestConfidence = Math.Max(bestConfidence, result.Confidence);
            var stablePosition = previousMatch is { } previous &&
                Math.Abs(previous.X + previous.Width / 2d - result.Bounds.X - result.Bounds.Width / 2d) <= Math.Max(4, result.Bounds.Width * 0.2) &&
                Math.Abs(previous.Y + previous.Height / 2d - result.Bounds.Y - result.Bounds.Height / 2d) <= Math.Max(4, result.Bounds.Height * 0.2);
            consecutiveMatches = result.Found ? (stablePosition ? consecutiveMatches + 1 : 1) : 0;
            previousMatch = result.Found ? result.Bounds : null;
            AppendLog($"PID {target.ProcessId}, {targetName}: confiança {result.Confidence:0.00}/{threshold:0.00}, confirmação {consecutiveMatches}/3 ({searchTime.Elapsed.TotalSeconds:0.0}s).");

            if (consecutiveMatches >= 3)
            {
                var clickX = result.Bounds.X + result.Bounds.Width * (clickAnchorX ?? 0.5);
                var clickY = result.Bounds.Y + result.Bounds.Height * (clickAnchorY ?? 0.5);
                AppendLog($"{targetName} confirmado ({result.Confidence:0.00}). Preparando {clickCount} clique(s) em X={clickX:0}, Y={clickY:0} da captura.");
                (int X, int Y) click = default;
                for (var press = 1; press <= clickCount; press++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    click = await _windowClickService.ClickRelativeAsync(target, clickX / frame.PixelWidth,
                        clickY / frame.PixelHeight, cancellationToken, AppendLog);
                    AppendLog($"{target.ProcessName} (PID {target.ProcessId}): Windows aceitou clique {press}/{clickCount} em X={click.X}, Y={click.Y} da tela.");
                    if (press < clickCount) await Task.Delay(180, cancellationToken);
                }
                return true;
            }

            await Task.Delay(100, cancellationToken);
        }

        AppendLog($"{target.ProcessName} (PID {target.ProcessId}), {targetName}: busca encerrada após {searchTime.Elapsed.TotalSeconds:0.0}s sem 3 confirmações consecutivas. Melhor confiança: {bestConfidence:0.00}; mínimo: {threshold:0.00}. Região pesquisada: X={region.X:P2}, Y={region.Y:P2}, {region.Width:P2} × {region.Height:P2}. Nenhum clique foi enviado.");
        return false;
    }

    private void AppendLog(string message)
    {
        if (ExecutionLog is null) return;
        if (ExecutionLog.LineCount > 100) ExecutionLog.Clear();
        ExecutionLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        ExecutionLog.ScrollToEnd();
    }

    private void BtnMousePercent_Click(object sender, RoutedEventArgs e) { }
}
