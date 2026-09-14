using System;
using System.Collections.Generic;
using System.Text;

namespace Macro.Models
{
    public class Boss
    {
        public string? World { get; set; }
        public string? Localization { get; set; }
        public string? Name { get; set; }
        public List<TimeSpan> SpawnTimes { get; set; } = new List<TimeSpan>();
        public TimeSpan NextSpawnTime
        {
            get
            {
                if (SpawnTimes == null || SpawnTimes.Count == 0)
                    return TimeSpan.Zero;

                var now = DateTime.Now.TimeOfDay;

                var nextSpawn = SpawnTimes
                    .Where(hour => hour > now)
                    .OrderBy(hour => hour)
                    .FirstOrDefault();

                if (nextSpawn == TimeSpan.Zero)
                {
                    return SpawnTimes
                        .OrderBy(hour => hour)
                        .First();
                }

                return nextSpawn;
            }
        }

        public TimeSpan TimeRemaining
        {
            get
            {
                var nextSpawn = NextSpawnTime;

                if (nextSpawn > DateTime.Now.TimeOfDay)
                {
                    return nextSpawn - DateTime.Now.TimeOfDay;
                }

                return (TimeSpan.FromDays(1) - DateTime.Now.TimeOfDay) + nextSpawn;
            }
        }

        public string TimeRemainingText
        {
            get
            {
                return TimeRemaining.ToString(@"hh\:mm\:ss");
            }
        }

        public string SpawnTimesText
        {
            get
            {
                return string.Join(" | ", SpawnTimes.OrderBy(horario => horario).Select(horario => horario.ToString(@"hh\:mm")));
            }
        }
    }
}
