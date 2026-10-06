namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    [System.Serializable]
    public class Recipe
    {
        public const float DefaultCraftTime = 2f;

        public Requirement[] requirements;
        public string craftedItemName;
        public Hearthglade.Core.Items.ItemId CraftedItem => new Hearthglade.Core.Items.ItemId( craftedItemName );
        public int craftedItemQuantity;
        public bool isUnlocked;
        // Seconds needed to craft ONE batch (craftedItemQuantity items).
        public float craftTime = DefaultCraftTime;
        // XP shown on the recipe card; nothing awards it yet - the game has no XP system.
        public int xp;
        // Optional short label shown on the recipe card (e.g. "Kluczowe").
        public string tag;
        // Buildings only: placing it removes the grass under its footprint (floor, walls, campfire - yes; a fence - no).
        public bool clearsGrass;
        // Buildings only: how far past its footprint the grass is cleared, in cells (0 = just what the footprint touches).
        public float grassClearRadius;
        public Recipe(Requirement[] _requirements, string _craftedItemName, bool _isUnlocked, int _craftedItemQuantity = 1){
            requirements   = _requirements;
            craftedItemName  = _craftedItemName;
            isUnlocked     = _isUnlocked;
            craftedItemQuantity = _craftedItemQuantity;
        }
    }
}
