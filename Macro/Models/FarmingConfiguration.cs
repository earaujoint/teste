namespace Macro.Models;

// Dados simples usados apenas para preencher os bindings da interface.
public sealed class FarmingConfiguration
{
    public LauncherGroup NormalLaunchers { get; set; } = new();
    public LauncherGroup BossLaunchers { get; set; } = new();
    public string ArenaStarter { get; set; } = "MIR4 Steam";
    public string ArenaInviter { get; set; } = "MIR4 Launcher 1";
    public string ArenaRepeatCountText { get; set; } = "1";
    public List<string> DonationLaunchers { get; set; } = [];
    public List<string> DailyLaunchers { get; set; } = [];
    public bool DailyDonation { get; set; }
    public bool DailyScroll { get; set; }
    public bool DailyFavorites { get; set; }
    public string StartTime { get; set; } = "";
    public RaidConfiguration Normal { get; set; } = new() { Name = "Raide 1" };
    public RaidConfiguration Boss { get; set; } = new() { Name = "Boss 1" };
    public List<MissionItemConfiguration> DailyItems { get; set; } = CreateMissionItems();
    public List<MissionItemConfiguration> DominationItems { get; set; } = CreateMissionItems();

    private static List<MissionItemConfiguration> CreateMissionItems() =>
        [new() { Name = "Minério Escuro" }, new() { Name = "Barra de Ferro" }, new() { Name = "Elixir de Vida" }];
}

public sealed class RaidConfiguration
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsAvailable { get; set; } = true;
    public bool IsEnabled { get; set; }
    public string RepeatCountText { get; set; } = "1";
}

public sealed class MissionItemConfiguration
{
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; }
    public List<string> SelectedMaps { get; set; } = [];
    public string Launcher { get; set; } = "MIR4 Launcher 2";
}

public sealed class LauncherGroup
{
    public string Starter { get; set; } = "MIR4 Steam";
    public string Guest1 { get; set; } = "MIR4 Launcher 2";
    public string Guest2 { get; set; } = "Não utilizar";
}
