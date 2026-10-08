using System;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common.Service.Factory;
using UnityEngine;
using TMPro;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public class EquipmentView : MonoBehaviour, IUIItem {
        public RectTransform RectTransform => rectTransform; 
        
        [ SerializeField ] GameObject head;
        [ SerializeField ] GameObject chest;
        [ SerializeField ] GameObject hand;
        [ SerializeField ] GameObject feet;
        [ SerializeField ] TMP_Text protectionText;

        [ SerializeField ] Transform container;
        [ SerializeField ] GameObject playerCamera;

        private RectTransform rectTransform;
        private RenderTexture playerCameraRenderTexture;
        
        // Dependencies
        private EquipmentService equipment;
        private IInventorySlotViewFactory slotFactory;
        //

        [ Inject ]
        public void Construct( EquipmentService equipment, IInventorySlotViewFactory slotFactory ) {
            this.rectTransform = this.GetComponent<RectTransform>( );
            this.playerCameraRenderTexture = playerCamera.GetComponent<Camera>( ).targetTexture;
            this.equipment = equipment;
            this.slotFactory = slotFactory;
            SetupView( equipment.Model );
            equipment.Model.Equipped += ( _, _ ) => RefreshProtection();
            equipment.Model.Unequipped += ( _, _ ) => RefreshProtection();
            RefreshProtection();
        }

        private void SetupView( EquipmentModel slots ) {
            slotFactory.Get( head, container, slots[ SlotType.Head ] );
            slotFactory.Get( chest, container, slots[SlotType.Chest] );
            slotFactory.Get( hand, container, slots[SlotType.Hand] );
            if( feet != null ) {
                slotFactory.Get( feet, container, slots[ SlotType.Feet ] );
            }
        }

        // What the worn clothes shield from, under the figure.
        private void RefreshProtection() {
            if( protectionText != null ) {
                protectionText.text = ItemStatText.Summary( equipment.Model.Protection );
            }
        }

        public void Show() {
            RectTransform.gameObject.SetActive ( true );
            playerCamera.SetActive( true );
        }

        public void Hide() {
            RectTransform.gameObject.SetActive ( false );
            playerCamera.SetActive( false );
        }

        private void OnDestroy( ) {
            playerCameraRenderTexture.Release();
        }
    }
}
