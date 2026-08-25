using System;
using System.Collections.Generic;
using System.Text;

namespace Macro.Models
{
    public class BossJson
    {
        public string? World { get; set; }
        public string? Localization { get; set; }
        public string? Name { get; set; }
        public List<string> SpawnTimes { get; set; } = new();
    }
}
