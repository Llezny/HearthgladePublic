using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Resource
{
    public class ChestSceneObject : SceneObject, ISceneObjectWithData {
        [SerializeField] string tooltipTitle = "";
        [SerializeField] List<ItemSO> possibleTreasureList = null;
        [SerializeField] int maxNumberOfTreasures = 10;
        [SerializeField] ChestAnimation chestAnimation;
        
        // Dependencies
        private IUIItem inventoryGridView;
        private MapManager mapManager;
        private ItemCatalog catalog;
        //
        [ Inject ]
        public void Construct( IUIItem inventoryGridView, MapManager mapManager, ChestView chestView, ItemCatalog catalog ) {
            this.inventoryGridView = inventoryGridView;
            this.mapManager = mapManager;
            this.catalog = catalog;
            // ChestSceneObject is spawned dynamically (GameObjectFactory), so Awake() already ran
            // before this DI injection — anything needing the catalog has to be set up here instead.
            ItemContainerView = chestView;
            Container = new ItemContainer( CHEST_SIZE );
        }

        public ItemContainer Container { get; private set; }
        public ItemContainerView ItemContainerView { get; private set; }

        private const int CHEST_SIZE = 6;

        private void SetupChest( List<ItemStackData> slots ) {
            Container = new ItemContainer( CHEST_SIZE );
            foreach( var slot in slots ) {
                if( slot.TryToStack( catalog, out var stack ) ) {
                    Container.Add( stack );
                }
            }
        }

        public override void InteractionStart() {
            ItemContainerView.Setup( Container );
            ItemContainerView.OpenMenu();
            chestAnimation.ChangeState();
            ItemContainerView.OnClose += CloseAndSaveData;
        }
        
        private void CloseAndSaveData() {
            chestAnimation.ChangeState();
            if (SceneObjectModel != null)
            {
                // Spawned from the map's data (a camp chest, a saved chest): write straight into the model it belongs to. Looking it up
                // by a key computed from the transform would miss it whenever the rotation does not survive the round trip bit for bit.
                mapManager.CurrentMap.TryOverrideAdditionalDataOfSceneObject(gameObject, CaptureState());
            }
            else
            {
                mapManager.CurrentMap.TryOverrideAdditionalDataOfSceneObject(new SceneObjectModel(transform));
            }
            ItemContainerView.OnClose -= CloseAndSaveData;
        }

        public object CaptureState() {
            var slots = new List<ItemStackData>();
            foreach( var slot in Container ) {
                var data = ItemStackData.From( slot.Stack );
                if( data != null ) {
                    slots.Add( data );
                }
            }
            return JsonConvert.SerializeObject( slots, Formatting.Indented );
        }

        public void RestoreState(object state) {
            SetupChest(JsonConvert.DeserializeObject<List<ItemStackData>>(state.ToString()));
        }
    }
}
