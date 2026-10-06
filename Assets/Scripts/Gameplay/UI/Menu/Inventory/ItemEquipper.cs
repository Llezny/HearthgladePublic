using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Items;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public class ItemEquipper : MonoBehaviour {

        [ SerializeField ] List<Pair<SlotType , Transform>> itemHolders;

        private readonly Dictionary<SlotType, Transform> itemHoldersDictionary = new( );

        private readonly Dictionary<SlotType, Transform> equippedItems = new( );

        private EquipmentService equipmentService;

        [ Inject ]
        public void Construct( EquipmentService equipmentService ) {
            this.equipmentService = equipmentService;
            this.equipmentService.OnEquipped += Equip;
            this.equipmentService.OnUnequipped += Unequip;
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
        }

        private void Equip( ItemSO item, SlotType slotType ) {
            if ( !itemHoldersDictionary.TryGetValue( slotType, out var itemHolder ) ) {
                UnityEngine.Debug.LogError( $"{item.itemType} slot is not defined" );
                return;
            }
            equippedItems.Add( slotType, Instantiate( item.GameObject, itemHolder.transform ).transform );
        }

        private void Unequip( ItemSO item, SlotType slotType ) {
            if ( equippedItems.TryGetValue( slotType, out var itemToUnequip ) ) {
               Destroy( itemToUnequip.gameObject );
               equippedItems.Remove( slotType );
            }
        }
    }
}