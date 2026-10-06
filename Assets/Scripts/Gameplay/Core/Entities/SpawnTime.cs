namespace Hearthglade.Core.Entities {

    /// <summary>When an entity may be around: butterflies by day, fireflies by night, the rest whenever.</summary>
    public enum SpawnTime { Always, Day, Night }

    public static class SpawnTimeRules {

        public static bool Allows( SpawnTime time, bool isDay ) {
            return time == SpawnTime.Always || ( time == SpawnTime.Day ) == isDay;
        }
    }
}
