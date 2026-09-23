using System.IO;
using System.Text.Json;
using Macro.Models;
namespace Macro.Services;
public static class FarmingConfigurationService
{
    private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacroMIR4", "farming.json");
    public static FarmingConfiguration Load()
    {
        if (!File.Exists(ConfigPath)) return new();
        var result = JsonSerializer.Deserialize<FarmingConfiguration>(File.ReadAllText(ConfigPath)) ?? throw new InvalidDataException("Configuração vazia.");
        if (result.Normal is null || result.Boss is null || result.DailyItems is null || result.DominationItems is null) throw new InvalidDataException("Configuração incompleta.");
        using var document = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        if (!document.RootElement.TryGetProperty(nameof(FarmingConfiguration.NormalLaunchers), out _))
            result.NormalLaunchers = new() { Starter = result.Starter, Guest1 = result.Partner };
        if (!document.RootElement.TryGetProperty(nameof(FarmingConfiguration.BossLaunchers), out _))
            result.BossLaunchers = new() { Starter = result.Starter, Guest1 = result.Partner };
        if (result.NormalLaunchers is null || result.BossLaunchers is null)
            throw new InvalidDataException("Configuração de launchers incompleta.");
        result.DonationLaunchers ??= ["MIR4 Launcher 1", "MIR4 Launcher 2", "MIR4 Steam"];
        return result;
    }
    public static void Save(FarmingConfiguration configuration)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var temporary = ConfigPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, ConfigPath, true);
    }
}
