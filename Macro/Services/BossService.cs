using Macro.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

public static class BossService
{
    public static async Task<List<Boss>> LoadBossesAsync()
    {
        string? json = null;

        var path1 = Path.Combine(AppContext.BaseDirectory, "Data", "bosses.json");
        if (File.Exists(path1))
        {
            json = await File.ReadAllTextAsync(path1);
        }
        else
        {
            var path2 = Path.Combine(AppContext.BaseDirectory, "bosses.json");
            if (File.Exists(path2))
            {
                json = await File.ReadAllTextAsync(path2);
            }
        }

        if (string.IsNullOrEmpty(json))
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Data.bosses.json", StringComparison.OrdinalIgnoreCase)
                                     || n.EndsWith("bosses.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                }
            }
        }

        var bossesJson = JsonSerializer.Deserialize<List<BossJson>>(json);

        return (bossesJson ?? new List<BossJson>())
            .Select(b => new Boss
            {
                World = b.World,
                Localization = b.Localization,
                Name = b.Name,
                SpawnTimes = (b.SpawnTimes ?? new List<string>())
                    .Select(TimeSpan.Parse)
                    .OrderBy(t => t)
                    .ToList()
            })
            .OrderBy(b => b.NextSpawnTime)
            .ThenBy(b => b.World)
            .ToList();
    }
}
