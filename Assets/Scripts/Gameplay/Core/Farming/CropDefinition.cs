using System;
using Hearthglade.Core.Items;

namespace Hearthglade.Core.Farming {

    // Immutable growth data of one crop, baked from its CropSO. Times are game minutes.
    public sealed class CropDefinition {
        private readonly int[] stageMinutes;

        public ItemId Id { get; }
        public CropLifecycle Lifecycle { get; }
        public PlotType PlotType { get; }

        // Every stage but the last has a duration; the last (mature) stage is open-ended.
        public int StageCount => stageMinutes.Length + 1;
        public int MatureAfterMinutes { get; }

        // Annual: produce per harvest. Perennial: fruit the plant can hold at once.
        public int MaxYield { get; }
        // Perennial only: minutes for one more fruit to ripen.
        public int FruitRegrowMinutes { get; }

        public ItemId Produce { get; }
        public ItemId Seed { get; }
        public float SeedDropChance { get; }

        // Which animals go for it when it is ripe (see ForageBrain).
        public ForageKind ForageKind { get; }

        // Where it grows at full speed; elsewhere it is slower (see CropClimate).
        public ClimateRange Climate { get; }

        public CropDefinition(
            ItemId id,
            CropLifecycle lifecycle,
            PlotType plotType,
            int[] stageMinutes,
            int maxYield,
            int fruitRegrowMinutes,
            ItemId produce,
            ItemId seed,
            float seedDropChance,
            ForageKind forageKind = ForageKind.None,
            ClimateRange? climate = null
        ) {
            if( id.IsEmpty ) {
                throw new ArgumentException( "crop id is empty", nameof( id ) );
            }
            if( stageMinutes == null ) {
                throw new ArgumentNullException( nameof( stageMinutes ) );
            }
            if( maxYield < 1 ) {
                throw new ArgumentOutOfRangeException( nameof( maxYield ) );
            }
            if( lifecycle == CropLifecycle.Perennial && fruitRegrowMinutes < 1 ) {
                throw new ArgumentOutOfRangeException( nameof( fruitRegrowMinutes ), "a perennial needs a regrow time" );
            }

            this.stageMinutes = ( int[] ) stageMinutes.Clone();
            foreach( int minutes in this.stageMinutes ) {
                if( minutes < 0 ) {
                    throw new ArgumentOutOfRangeException( nameof( stageMinutes ), "negative stage duration" );
                }
                MatureAfterMinutes += minutes;
            }

            Id = id;
            Lifecycle = lifecycle;
            PlotType = plotType;
            MaxYield = maxYield;
            FruitRegrowMinutes = fruitRegrowMinutes;
            Produce = produce;
            Seed = seed;
            SeedDropChance = seedDropChance;
            ForageKind = forageKind;
            Climate = climate ?? ClimateRange.Anywhere;
        }

        // Minutes growth stage `stage` lasts (0-based, below StageCount - 1).
        public int StageMinutes( int stage ) => stageMinutes[ stage ];
    }
}
