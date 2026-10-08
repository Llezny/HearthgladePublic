using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Player;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public class ItemEquipper : MonoBehaviour {

        [ SerializeField ] List<Pair<SlotType , Transform>> itemHolders;

        private readonly Dictionary<SlotType, Transform> itemHoldersDictionary = new( );

        private readonly Dictionary<SlotType, Transform> equippedItems = new( );

        private EquipmentService equipmentService;
        private PlayerToolService toolService;
        private ItemCatalog catalog;

        // The tool shown in the hand for the length of a piece of work; the item of the hand slot is hidden meanwhile and comes back after.
        private Transform temporaryTool;

        [ Inject ]
        public void Construct( EquipmentService equipmentService, PlayerToolService toolService, ItemCatalog catalog ) {
            this.equipmentService = equipmentService;
            this.toolService = toolService;
            this.catalog = catalog;
            this.equipmentService.OnEquipped += Equip;
            this.equipmentService.OnUnequipped += Unequip;
            this.toolService.UseStarted += ShowTemporaryTool;
            this.toolService.UseEnded += HideTemporaryTool;
        }

        private void Awake( ) {
            foreach ( var itemHolder in itemHolders ) {
                if ( !itemHoldersDictionary.TryAdd( itemHolder.Item1, itemHolder.Item2 ) ) {
                    UnityEngine.Debug.LogError( $"Tried to add {itemHolder.Item1} multiple times" );
                }
            }
        }

        private void OnDestroy( ) {
            equipmentService.OnEquipped -= Equip;
            equipmentService.OnUnequipped -= Unequip;
            toolService.UseStarted -= ShowTemporaryTool;
            toolService.UseEnded -= HideTemporaryTool;
        }

        private void Equip( ItemSO item, SlotType slotType ) {
            // Clothing has no model to hold: PlayerOutfit puts its part on the character. Only a model needs a socket.
            if ( item.GameObject == null ) {
                return;
            }
            if ( !itemHoldersDictionary.TryGetValue( slotType, out var itemHolder ) ) {
                UnityEngine.Debug.LogError( $"{item.itemType} slot is not defined" );
                return;
            }
            var model = Instantiate( item.GameObject, itemHolder.transform ).transform;
            equippedItems.Add( slotType, model );
            // Equipped while a tool is on show: the new item waits behind it.
            model.gameObject.SetActive( temporaryTool == null || slotType != SlotType.Hand );
        }

        private void Unequip( ItemSO item, SlotType slotType ) {
            if( equippedItems.TryGetValue( slotType, out var itemToUnequip ) ) {
               Destroy( itemToUnequip.gameObject );
               equippedItems.Remove( slotType );
            }
        }

        // A tool from the backpack does the work with the hand slot untouched, so only its look is swapped into the hand.
        private void ShowTemporaryTool( ToolChoice tool ) {
            HideTemporaryTool();
            // The tool of the hand is already on show.
            if ( tool.Slot == equipmentService.Model[ SlotType.Hand ] ) {
                return;
            }
            var asset = catalog.GetAsset( tool.Definition );
            if ( asset == null || asset.GameObject == null || !itemHoldersDictionary.TryGetValue( SlotType.Hand, out var holder ) ) {
                return;
            }
            if ( equippedItems.TryGetValue( SlotType.Hand, out var held ) ) {
                held.gameObject.SetActive( false );
            }
            temporaryTool = Instantiate( asset.GameObject, holder ).transform;
        }

        private void HideTemporaryTool( ) {
            if ( temporaryTool == null ) {
                return;
            }
            Destroy( temporaryTool.gameObject );
            temporaryTool = null;
            if ( equippedItems.TryGetValue( SlotType.Hand, out var held ) ) {
                held.gameObject.SetActive( true );
            }
        }
    }
}
