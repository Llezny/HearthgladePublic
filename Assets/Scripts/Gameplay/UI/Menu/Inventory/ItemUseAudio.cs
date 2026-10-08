using System;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Audio;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items.UsableItems;
using VContainer.Unity;

namespace Hearthglade.Gameplay.UI.Menu.Inventory
{
    // Plays an item's use sound when the inventory reports it was used, so the inventory itself stays silent.
    public sealed class ItemUseAudio : IInitializable, IDisposable {
        private readonly InventoryService inventoryService;
        private readonly ItemCatalog catalog;
        private readonly ISfxPlayer sfx;

        public ItemUseAudio( InventoryService inventoryService, ItemCatalog catalog, ISfxPlayer sfx ) {
            this.inventoryService = inventoryService;
            this.catalog = catalog;
            this.sfx = sfx;
        }

        public void Initialize() {
            inventoryService.OnItemUsed += PlaySound;
        }

        public void Dispose() {
            inventoryService.OnItemUsed -= PlaySound;
        }

        private void PlaySound( ItemDefinition item, NutritionOverride nutrition ) {
            if( catalog.GetAsset( item ) is IUsableItem usable ) {
                sfx.Play( usable.UseSound );
            }
        }
    }
}
