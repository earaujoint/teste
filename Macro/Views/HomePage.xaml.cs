using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Macro.Services;

namespace Macro.Views;

public partial class HomePage : Page
{
    private readonly FarmingPage _runner;
    private readonly FarmingPresetService _store = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public HomePage(FarmingPage runner)
    {
        InitializeComponent();
        _runner = runner;
        _timer.Tick += (_, _) => UpdateStatus();
        Loaded += (_, _) => { RefreshPresets(); UpdateStatus(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void RefreshPresets()
    {
        var selectedName = (PresetSelector.SelectedItem as FarmingPreset)?.Name;
        try
        {
            var presets = _store.Load();
            PresetSelector.ItemsSource = presets;
            PresetSelector.SelectedItem = presets.FirstOrDefault(p => p.Name == selectedName) ?? presets.FirstOrDefault();
            Message.Text = presets.Count == 0 ? "Nenhum preset salvo. Configure o fluxo em Farming e clique em Salvar preset." : "Gerencie seus fluxos na página Presets.";
        }
        catch (Exception ex)
        {
            PresetSelector.ItemsSource = null;
            Message.Text = $"Não foi possível carregar os presets: {ex.Message}";
        }
    }

    private void Preset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PresetDetails.Content = PresetSelector.SelectedItem as FarmingPreset;
        if (_runner is not null) UpdateStatus();
    }

    private void UpdateStatus()
    {
        PresetSelector.IsEnabled = !_runner.IsRunning;
        StartButton.IsEnabled = !_runner.IsRunning && PresetSelector.SelectedItem is FarmingPreset;
        StopButton.IsEnabled = _runner.IsRunning;
        Status.Text = _runner.RunStatus;
        if (Log.Text != _runner.RunLog) { Log.Text = _runner.RunLog; Log.ScrollToEnd(); }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (PresetSelector.SelectedItem is not FarmingPreset selected || _runner.IsRunning) return;
        try
        {
            var preset = _store.Load().FirstOrDefault(p => p.Name == selected.Name);
            if (preset is null) { RefreshPresets(); Message.Text = "Este preset foi excluído. Selecione outro fluxo."; return; }
            var execution = _runner.StartPresetAsync(preset);
            UpdateStatus();
            await execution;
        }
        catch (Exception ex) { Message.Text = $"Não foi possível iniciar: {ex.Message}"; }
        finally { UpdateStatus(); }
    }

    private void Stop_Click(object sender, RoutedEventArgs e) { _runner.StopMacro(); UpdateStatus(); }
}
