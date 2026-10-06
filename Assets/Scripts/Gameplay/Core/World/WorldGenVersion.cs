namespace Hearthglade.Core.World {

    /// <summary>
    /// Version of the world generation algorithm. Saved with every map, so a change that would move things
    /// for a map that is re-rolled later (scene objects of a save without them) can be told apart from the
    /// generation the map was made with. Bump it whenever the output for a given seed changes.
    /// </summary>
    public static class WorldGenVersion {

        /// <summary>Saves that predate the field.</summary>
        public const int Legacy = 0;

        /// <summary>1: equalised height/temperature/humidity layers with a per-layer scale.</summary>
        public const int EqualisedLayers = 1;

        /// <summary>2: a single island with a winding coast, hills and a start on the west coast.</summary>
        public const int Island = 2;

        /// <summary>3: resources come from the biome (density field, conditions, deposits, blended borders).</summary>
        public const int BiomeResources = 3;

        /// <summary>4: resources are planned per chunk (spacing, chunk budget, cleared start and reserved cells).</summary>
        public const int PlannedResources = 4;

        /// <summary>5: points of interest (camps with loot) are placed before resources and reserve their sites.</summary>
        public const int Current = 5;
    }
}
