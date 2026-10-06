using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public class EquipmentComponent : MonoBehaviour, ISaveable {
        
        // Dependencies
        [ SerializeField ] private EquipmentService equipmentService;
        
        [ SerializeField ] private SaveManager saveManager;
        private ItemCatalog catalog;
        //
        
        [ Inject ]
        public void Construct( EquipmentService equipment, SaveManager saveManager, ItemCatalog catalog ) {
            this.equipmentService = equipment;
            this.saveManager = saveManager;
            this.catalog = catalog;
            saveManager.RegisterISavable(this);
        }

        private void Start( ) {
            if( saveManager.TryGetState<EquipmentComponent>( out var gameState) ) {
                RestoreState(gameState);
            }
        }

        public object CaptureState( ) {
            var data = new Dictionary<SlotType, ItemStackData>( );
            foreach( var pair in equipmentService.Model.Slots ) {
                var stack = ItemStackData.From( pair.Value.Stack );
                if( stack != null ) {
                    data.Add( pair.Key, stack );
                }
            }
            return data;
        }

        public void RestoreState( object state ) {
            var data = JsonConvert.DeserializeObject<Dictionary<SlotType, ItemStackData>>( state.ToString( ) );
            var model = equipmentService.Model;
            foreach( var pair in data ) {
                if( pair.Key != SlotType.Other && pair.Value.TryToStack( catalog, out var stack ) ) {
                    model[ pair.Key ].Set( stack );
                }
            }
        }
    }
}
