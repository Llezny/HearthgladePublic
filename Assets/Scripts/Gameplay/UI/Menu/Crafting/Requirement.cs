namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    [System.Serializable]
    public class Requirement {
        // Stays a string so existing recipes, costs and prefabs keep their data; logic reads it as an ItemId.
        public string requiredItemName;
        public Hearthglade.Core.Items.ItemId Item => new Hearthglade.Core.Items.ItemId( requiredItemName );
        public int requiredQuantity;    
        public Requirement( string requiredItemName, int requiredQuantity ){
            this.requiredItemName = requiredItemName;
            this.requiredQuantity = requiredQuantity;
        }
    }
}
