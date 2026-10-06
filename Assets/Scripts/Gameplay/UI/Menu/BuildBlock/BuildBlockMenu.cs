using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.BuildBlock
{
    public class BuildBlockMenu : Common.Menu {
        
        private Inventory.InventoryService inventoryService;
        private MapManager mapManager;
        private static readonly Hearthglade.Core.Items.ItemId WoodItem = new Hearthglade.Core.Items.ItemId( "Wood" );
        private const int BuildCostWood = 2;


        public bool isBuildModeEnabled;
        [ SerializeField ] RecipeSlot ingredientSlot = null;
        [ SerializeField ] Button confirmButton = null;
        private GameObject selectedBlock = null;

        [ Inject ]
        public void Construct( Inventory.InventoryService inventoryService, MapManager mapManager ) {
            this.inventoryService = inventoryService;
            this.mapManager = mapManager;
        }
        
        private void OnEnable() {
            gameEvents.onBuildBlockSwitch += SwitchMode;
        }

        private void OnDisable() {
            gameEvents.onBuildBlockSwitch -= SwitchMode;
        }

        override public void OpenMenu(){
            base.OpenMenu();
            RefreshSlot();
            gameEvents.OnBuildBlockSwitch();
        }

        public override void CloseMenu( bool checkIfFadeOutShroud = true ){
            base.CloseMenu( checkIfFadeOutShroud );
            gameEvents.OnBuildBlockSwitch();
        }

        public void Close() {
            CloseMenu( true );
        }

        public void Build(){
            mapManager.ChangeBiomeOfBlock( selectedBlock );
            inventoryService.RemoveItem( WoodItem, BuildCostWood );
        }

        public void SwitchMode() {
            if(isBuildModeEnabled) {
                HideBuyableBlocks();
                OnBuildModeDisable();
            }

            else {
                ShowBuyableBlocks();
                OnBuildModeEnable();
            }
            isBuildModeEnabled = !isBuildModeEnabled;
        }

        public void ShowBuyableBlocks() {
            gameEvents.onBlockReached += OnBuildModeDisable;
            gameEvents.onBlockReached += OnBuildModeEnable;
        }

        public void HideBuyableBlocks() {
            gameEvents.onBlockReached -= OnBuildModeDisable;
            gameEvents.onBlockReached -= OnBuildModeEnable;
        }

        private void OnBuildModeEnable() {
            gameEvents.OnBuildModeEnable( );
        }

        private void OnBuildModeDisable() {
            gameEvents.OnBuildModeDisable();
        }

        private void RefreshSlot() {
            var itemsNum = inventoryService.Count( WoodItem );
            ingredientSlot.EnableQuantityText();
            ingredientSlot.SetQuantityText( BuildCostWood, itemsNum );
            confirmButton.interactable = BuildCostWood <= itemsNum; 
        }
    }
}
