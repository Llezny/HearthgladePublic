using System;
using System.Collections.Generic;

namespace Hearthglade.Core.Farming {

    // Versioned save of a whole farm; plain public fields so any serializer handles it.
    [Serializable]
    public class FarmSnapshot {
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public List<PlotSnapshot> Plots = new();
    }

    [Serializable]
    public class PlotSnapshot {
        public int X;
        public int Y;
        public int Z;
        public PlotType Type;
        // Null when the plot is empty.
        public string CropId;
        public long PlantedAtMinute;
        public long FruitClockStartMinute;
        // Version 1 saves have 0 here: normal speed.
        public int SpeedPercent;
    }
}
