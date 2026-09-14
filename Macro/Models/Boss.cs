using System;
using System.Collections.Generic;
using System.Linq;

namespace Macro.Models
{
    public class Boss
    {
        public string? World { get; set; }

        public string? Localization { get; set; }

        public string? Name { get; set; }

        public List<TimeSpan> SpawnTimes { get; set; } = new List<TimeSpan>();

        public DateTime NextSpawnTime
        {
            get
            {
                if (SpawnTimes == null || SpawnTimes.Count == 0)
                    return DateTime.MaxValue;

                var now = DateTime.Now;

                // Procura o próximo spawn que ainda acontecerá hoje
                var nextSpawn = SpawnTimes
                    .Select(time => now.Date.Add(time))
                    .Where(time => time > now)
                    .OrderBy(time => time)
                    .FirstOrDefault();

                if (nextSpawn != default)
                    return nextSpawn;

                // Se não existe mais nenhum spawn hoje,
                // pega o primeiro spawn do dia seguinte
                var firstSpawn = SpawnTimes
                    .OrderBy(time => time)
                    .First();

                return now.Date
                    .AddDays(1)
                    .Add(firstSpawn);
            }
        }

        public TimeSpan TimeRemaining
        {
            get
            {
                if (NextSpawnTime == DateTime.MaxValue)
                    return TimeSpan.MaxValue;

                var remaining = NextSpawnTime - DateTime.Now;

                if (remaining < TimeSpan.Zero)
                    return TimeSpan.Zero;

                return remaining;
            }
        }

        public string TimeRemainingText
        {
            get
            {
                if (TimeRemaining == TimeSpan.MaxValue)
                    return "--:--:--";

                return TimeRemaining.ToString(@"hh\:mm\:ss");
            }
        }

        public string SpawnTimesText
        {
            get
            {
                return string.Join(
                    " | ",
                    SpawnTimes
                        .OrderBy(horario => horario)
                        .Select(horario =>
                            horario.ToString(@"hh\:mm"))
                );
            }
        }
    }
}