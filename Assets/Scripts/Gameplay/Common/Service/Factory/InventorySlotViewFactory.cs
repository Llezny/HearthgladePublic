using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.Common.Service.Factory {

    public interface IInventorySlotViewFactory {
        InventorySlotView Get( GameObject prefab, Transform parent, ItemSlot slot );
        InventorySlotView Get( GameObject prefab, Transform parent, ItemSlot slot, ScrollRect inventoryScroll );
        // One view per slot of the container, named by slot index. Dispose the group to remove them again.
        SlotViewGroup Populate( ItemContainer container, GameObject prefab, Transform parent, ScrollRect scroll, bool refreshPrefilledSlots = false );
    }

    public class InventorySlotViewFactory : IInventorySlotViewFactory {

        private readonly IItemSlotActions slotActions;
        private readonly ItemFrameService itemFrameService;
        private readonly CanvasService canvasService;
        private readonly ItemCatalog catalog;

        [ Inject ]
        public InventorySlotViewFactory( IItemSlotActions slotActions, CanvasService canvasService, ItemFrameService itemFrameService, ItemCatalog catalog ) {
            this.slotActions = slotActions;
            this.canvasService = canvasService;
            this.itemFrameService = itemFrameService;
            this.catalog = catalog;
        }

        public InventorySlotView Get( GameObject prefab, Transform parent, ItemSlot slot ) {
            var slotView = Object.Instantiate( prefab, parent ).GetComponent<InventorySlotView>();
            slotView.SetupSlot( slot, slotActions, itemFrameService, catalog );
            return slotView;
        }

        public InventorySlotView Get( GameObject prefab, Transform parent, ItemSlot slot, ScrollRect inventoryScroll ) {
            var slotView = Get( prefab, parent, slot );
            ScrollableDragDrop.Create( slotView, inventoryScroll, canvasService.TopUICanvas.transform );
            return slotView;
        }

        public SlotViewGroup Populate( ItemContainer container, GameObject prefab, Transform parent, ScrollRect scroll, bool refreshPrefilledSlots = false ) {
            var group = new SlotViewGroup();
            for( int i = 0; i < container.Size; i++ ) {
                var view = Get( prefab, parent, container[ i ], scroll );
                view.gameObject.name = i.ToString();
                group.Add( view );
                if( refreshPrefilledSlots && !container[ i ].IsEmpty ) {
                    view.RefreshSlotView();
                }
            }
            return group;
        }
    }
}
