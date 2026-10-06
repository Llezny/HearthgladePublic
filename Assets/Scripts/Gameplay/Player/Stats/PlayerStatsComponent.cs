using Hearthglade.Core.Items;
using Hearthglade.Core.Stats;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Items.UsableItems;
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
        private EquipmentService equipmentService;

        [ Inject ]
        public void Construct( SaveManager saveManager, InventoryService inventoryService, EquipmentService equipmentService, GameEvents gameEvents, ClockManager clockManager ) {
            this.saveManager = saveManager;
            this.inventoryService = inventoryService;
            this.equipmentService = equipmentService;
            this.playerStatsModel = new PlayerStatsModel(  this.GetComponent<Rigidbody>(), gameEvents, clockManager );
            saveManager.RegisterISavable(this);
            if( saveManager.TryGetState<PlayerStatsComponent>( out var gameState) ) {
                RestoreState(gameState);
            }
        }

        private void Update() {
            playerStatsModel.Tick();
        }

        private void OnEnable( ) {
            equipmentService.OnEquipped += Equip;
            equipmentService.OnUnequipped += Unequip;
            inventoryService.OnItemUsed += UseItem;
        }

        private void OnDisable( ) {
            equipmentService.OnEquipped -= Equip;
            equipmentService.OnUnequipped -= Unequip;
            inventoryService.OnItemUsed -= UseItem;
        }

        private void Equip( ItemSO item, SlotType slotType ) {
            playerStatsModel.AddStatModifiers( item.Modifiers );
        }

        private void Unequip( ItemSO item, SlotType slotType ) {
            playerStatsModel.RemoveStatModifiers( item.Modifiers );
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
