using Hearthglade.Core.Items;
using Hearthglade.Core.Stats;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.HUD;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Player.Stats {

    public class PlayerStatsComponent : MonoBehaviour, ISaveable {

        public PlayerStatsModel playerStatsModel;
        [ SerializeField ] private SaveManager saveManager;
        private InventoryService inventoryService;

        [ Inject ]
        public void Construct( SaveManager saveManager, InventoryService inventoryService, GameEvents gameEvents, ClockManager clockManager, ExposureService exposure ) {
            this.saveManager = saveManager;
            this.inventoryService = inventoryService;
            this.playerStatsModel = new PlayerStatsModel( this.GetComponent<Rigidbody>(), gameEvents, clockManager, () => exposure.State.IsHarmful );
            saveManager.RegisterISavable(this);
            if( saveManager.TryGetState<PlayerStatsComponent>( out var gameState) ) {
                RestoreState(gameState);
            }
        }

        private void Update() {
            playerStatsModel.Tick();
        }

        private void OnEnable( ) {
            inventoryService.OnItemUsed += UseItem;
        }

        private void OnDisable( ) {
            inventoryService.OnItemUsed -= UseItem;
        }

        private void UseItem( ItemDefinition item, NutritionOverride nutrition ) {
            playerStatsModel.AddStatValue( StatsMap.thirst, nutrition.Thirst );
            playerStatsModel.AddStatValue( StatsMap.hunger, nutrition.Hunger );
            playerStatsModel.AddStatValue( StatsMap.health, nutrition.Health );
        }

        public Stat GetPlayerStat( StatsMap statName ) {
            return playerStatsModel.GetStat( statName );
        }

        public object CaptureState() {
            return playerStatsModel.CaptureState();
        }

        public void RestoreState(object state) {
            playerStatsModel.RestoreState(state);
        }
    }
}
