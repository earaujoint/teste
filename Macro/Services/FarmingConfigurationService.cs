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
