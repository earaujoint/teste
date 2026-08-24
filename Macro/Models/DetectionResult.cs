using System;
using System.Collections.Generic;
using System.Text;
using SDPoint = System.Drawing.Point;

namespace Macro.Models
{
    public class DetectionResult
    {
        public bool Found { get; set; }
        public double Confidence { get; set; }
        public SDPoint Location { get; set; }
    }
}
