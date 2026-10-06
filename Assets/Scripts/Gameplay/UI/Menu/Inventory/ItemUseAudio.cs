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
        private readonly AudioManager audioManager;

        public ItemUseAudio( InventoryService inventoryService, ItemCatalog catalog, AudioManager audioManager ) {
            this.inventoryService = inventoryService;
            this.catalog = catalog;
            this.audioManager = audioManager;
        }

        public void Initialize() {
            inventoryService.OnItemUsed += PlaySound;
        }

        public void Dispose() {
            inventoryService.OnItemUsed -= PlaySound;
        }

        private void PlaySound( ItemDefinition item, NutritionOverride nutrition ) {
            if( catalog.GetAsset( item ) is IUsableItem usable ) {
                audioManager.Play( usable.UseSound );
            }
        }
    }
}
