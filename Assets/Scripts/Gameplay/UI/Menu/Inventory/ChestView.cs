using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common.Extension;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Database;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public class ChestView : ItemContainerView {
        private Transform slotsHolder = null;
        public GameObject itemSlotPrefab = null;
        
        // Dependencies
        private IInventorySlotViewFactory inventorySlotViewFactory;
        private IInventoryGridView inventoryGridView;
        private SlotViewGroup slotViews;
        //

        [ Inject ]
        public void Construct( IInventorySlotViewFactory inventorySlotViewFactory, IInventoryGridView inventoryGridView ) {
            this.inventorySlotViewFactory = inventorySlotViewFactory;
            this.inventoryGridView = inventoryGridView;
        }   

        protected override void Start() {
            base.Start();
            slotsHolder = content.Find( "Slots" );
        }

        private void OnEnable() {
            OnClose += CloseCallback;
            OnOpen += OpenCallback;
        }

        private void OnDisable() {
            OnClose -= CloseCallback;
            OnOpen -= OpenCallback;
        }

        public override void Setup( ItemContainer container ) {
            base.Setup( container );
            slotsHolder.DestroyChildren(  );
            slotViews?.Dispose();
            slotViews = inventorySlotViewFactory.Populate( Container, itemSlotPrefab, slotsHolder, inventoryGridView.ItemListScroll, refreshPrefilledSlots: true );
        }

        public void OpenCallback() {
            inventoryGridView.Show();
        }

        private void CloseCallback() {
            inventoryGridView.Hide();
        }
    }
} 
