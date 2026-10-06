using System;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    // The player's worn items. The rules live in EquipmentModel; this hands its events to the game with the item assets
    // (stat modifiers, in-hand model) attached.
    public class EquipmentService {
        public EquipmentModel Model { get; } = new();
        public event Action<ItemSO, SlotType> OnEquipped;
        public event Action<ItemSO, SlotType> OnUnequipped;

        [ Inject ]
        public EquipmentService( ItemCatalog catalog ) {
            Model.Equipped += ( slotType, item ) => OnEquipped?.Invoke( catalog.GetAsset( item ), slotType );
            Model.Unequipped += ( slotType, item ) => OnUnequipped?.Invoke( catalog.GetAsset( item ), slotType );
        }
    }
}
