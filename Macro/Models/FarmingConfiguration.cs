using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace Macro.Models;
public class RaidConfiguration : INotifyPropertyChanged, IDataErrorInfo
{
    private bool _enabled;
    private int _count = 1;
    private string _repeatCountText = "1";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsAvailable { get; set; } = true;
    public bool IsEnabled { get => _enabled; set { _enabled = value; Changed(); } }
    public int RepeatCount { get => _count; set { if (value is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(value), "Use de 1 a 99 repetições."); _count = value; _repeatCountText = value.ToString(); Changed(); Changed(nameof(RepeatCountText)); } }
    [System.Text.Json.Serialization.JsonIgnore]
    public string RepeatCountText
    {
        get => _repeatCountText;
        set { _repeatCountText = value; if (int.TryParse(value, out int count) && count is >= 1 and <= 99) _count = count; Changed(); }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasValidCount => int.TryParse(_repeatCountText, out int count) && count is >= 1 and <= 99;
    [System.Text.Json.Serialization.JsonIgnore]
    public string Error => HasValidCount ? "" : "Use um número inteiro de 1 a 99.";
    public string this[string name] => name == nameof(RepeatCountText) ? Error : "";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public class MissionItemConfiguration
{
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; }
    public List<string> SelectedMaps { get; set; } = [];
    public string Launcher { get; set; } = "MIR4 Launcher 2";
}
public class FarmingConfiguration
{
    public LauncherGroup NormalLaunchers { get; set; } = new();
    public LauncherGroup BossLaunchers { get; set; } = new();
    public string ArenaStarter { get; set; } = "MIR4 Steam";
    public string ArenaInviter { get; set; } = "MIR4 Launcher 1";
    public string DonationLauncher { get; set; } = "MIR4 Steam";
    public List<string> DonationLaunchers { get; set; } = ["MIR4 Launcher 1", "MIR4 Launcher 2", "MIR4 Steam"];
    public string DailyLauncher { get; set; } = "MIR4 Launcher 2";
    public bool DailyDonation { get; set; } = true;
    public bool DailyFavorites { get; set; } = true;
    public string Starter { get; set; } = "MIR4 Steam";
    public string Partner { get; set; } = "MIR4 Launcher 2";
    public RaidConfiguration Normal { get; set; } = new() { Name = "Raide 1", Description = "Raide normal configurada no macro", IsEnabled = true, RepeatCount = 2 };
    public RaidConfiguration Boss { get; set; } = new() { Name = "Boss 1", Description = "Boss raid configurada no macro", IsEnabled = true };
    public List<MissionItemConfiguration> DailyItems { get; set; } = CreateItems();
    public List<MissionItemConfiguration> DominationItems { get; set; } = CreateItems();
    private static List<MissionItemConfiguration> CreateItems() => [new() { Name = "Minério Escuro" }, new() { Name = "Barra de Ferro" }, new() { Name = "Elixir de Vida" }];
}

public class LauncherGroup
{
    public string Starter { get; set; } = "MIR4 Steam";
    public string Guest1 { get; set; } = "MIR4 Launcher 2";
    public string Guest2 { get; set; } = "Não utilizar";
}

