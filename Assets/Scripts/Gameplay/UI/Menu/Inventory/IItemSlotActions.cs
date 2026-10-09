using Hearthglade.Core.Items;

namespace Hearthglade.Gameplay.UI.Menu.Inventory
{
    // What a slot view can ask for; the views know nothing else about the inventory.
    public interface IItemSlotActions {
        bool TryMoveItem( ItemSlot from, ItemSlot to );
        void UseItem( ItemSlot slot );
        void PlaceItem( ItemSlot slot );
    }
}
