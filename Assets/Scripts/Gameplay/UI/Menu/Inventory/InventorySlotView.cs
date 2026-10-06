using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.UI.Menu.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.Inventory
{
    // Draws one ItemSlot and forwards what the player does with it; it holds no game state of its own.
    [System.Serializable]
    public class InventorySlotView : MonoBehaviour, IPointerClickHandler {
        
        [ field:SerializeField ] public TextMeshProUGUI QuantityText { get; private set; }
        [ field:SerializeField ] public Image SlotIcon { get; private set; }
        [ field:SerializeField ] public Image SlotBackground { get; private set; }
        [ field:SerializeField ] public GameObject ContextButtonPrefab { get; private set; }
        [ field:SerializeField ] public Slider DurabilitySlider { get; private set; }
        public ItemSlot Slot { get; private set; }
        private IItemSlotActions slotActions;
        private ItemFrameService itemFrameService;
        private ItemCatalog catalog;
        private AsyncOperationHandle pendingFrameHandle;
        private bool hasPendingFrame;

        public void OnPointerClick(PointerEventData data) {
            if( ( SlotActions.Available( Slot ) & SlotAction.Use ) != 0 ) {
                var obj = Instantiate( ContextButtonPrefab ).GetComponent<ContextButton>();
                var pos = this.GetComponent<RectTransform>().position;
                obj.SetupButton( 
                    new Vector3( pos.x, pos.y + 100, pos.z ),
                    () => slotActions.UseItem( Slot ),
                    "Use"
                );
            }
        }

        // The slot this one was dropped onto (the view under the pointer), if any.
        public bool TryDropOnto( PointerEventData eventData ) {
            var target = eventData.pointerEnter != null ? eventData.pointerEnter.GetComponentInParent<InventorySlotView>() : null;
            return target != null && slotActions.TryMoveItem( Slot, target.Slot );
        }

        public void SetupSlot( ItemSlot slot, IItemSlotActions slotActions, ItemFrameService itemFrameService, ItemCatalog catalog ) {
            if( this.Slot != null ) {
                this.Slot.Changed -= HandleSlotChanged;
            }
            this.Slot = slot;
            this.slotActions = slotActions;
            this.itemFrameService = itemFrameService;
            this.catalog = catalog;
            this.Slot.Changed += HandleSlotChanged;
            RefreshSlotView();
        }

        public void RefreshSlotView() {
            Render( SlotViewState.From( Slot ) );
        }

        private void Render( SlotViewState state ) {
            if( state.IsEmpty ) {
                this.DurabilitySlider.gameObject.SetActive( false );
                DisableImage();
                SetActiveQuantityText( false );
                return;
            }
            this.DurabilitySlider.gameObject.SetActive( state.ShowDurability );
            this.DurabilitySlider.maxValue = state.MaxDurability;
            this.DurabilitySlider.value = state.Durability;
            SetImage( state );
            SetActiveQuantityText( true );
            SetQuantityText( state.Count );
        }

        private void HandleSlotChanged( ItemSlot slot ) {
            RefreshSlotView();
        }

        private void OnDestroy() {
            CancelPendingFrame();
            if( Slot != null ) {
                Slot.Changed -= HandleSlotChanged;
            }
        }

        private void SetQuantityText(int quantity){
            QuantityText.text = quantity.ToString();
        }

        private void SetActiveQuantityText( bool val ){
            QuantityText.gameObject.SetActive(val);
        }


        private void SetImage( SlotViewState state ) {
            this.SlotIcon.color = Color.white;
            this.SlotIcon.sprite = catalog.GetAsset( state.Item ).icon;
            CancelPendingFrame();
            var handle = itemFrameService.GetFrameAsync( state.Rarity );
            if( handle.IsDone ) {
                SetItemFrame( handle );
                return;
            }
            pendingFrameHandle = handle;
            hasPendingFrame = true;
            handle.Completed += SetItemFrame;
        }

        private void SetItemFrame(AsyncOperationHandle asyncOperationHandle) {
            hasPendingFrame = false;
            this.SlotBackground.sprite = asyncOperationHandle.Result as Sprite;
            this.SlotBackground.color = Color.white;
        }

        // A still-loading frame must not land after the slot was emptied or switched to another item
        // (or destroyed) - it would resurrect a stale frame on an empty slot.
        private void CancelPendingFrame() {
            if( hasPendingFrame ) {
                pendingFrameHandle.Completed -= SetItemFrame;
                hasPendingFrame = false;
            }
        }

        private void DisableImage(){
            CancelPendingFrame();
            this.SlotIcon.color = Color.clear;
            this.SlotBackground.color = Color.clear;
        }
    }
}
