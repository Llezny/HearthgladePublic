using Hearthglade.Core.Items;

namespace Hearthglade.Core.Farming {

    public readonly struct HarvestResult {
        public bool Success { get; }
        public ItemId Produce { get; }
        public int Quantity { get; }
        public ItemId Seed { get; }
        public int SeedQuantity { get; }

        public HarvestResult( ItemId produce, int quantity, ItemId seed, int seedQuantity ) {
            Success = true;
            Produce = produce;
            Quantity = quantity;
            Seed = seed;
            SeedQuantity = seedQuantity;
        }

        public static HarvestResult Failed => default;
    }
}
