using System.Collections.Generic;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Housing;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Items.BuildableItems;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using UnityEngine.Assertions;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Build
{
    // The crafting screen, but the action is "Build": it closes the menu and lets the player place the object in the world
    // (BuildingPlacer takes the ingredients when the object is put down).
    public class BuildingMenu : RecipeMenu {
        public GameManager GameManager{ get { return gameManager; } private set { gameManager = value; } }

        BuildingPlacer buildingPlacer = null;
        GameManager gameManager = null;
        GameState buildingState = null;
        HouseService houseService = null;
        MurmurService murmurService = null;
        // An item of the backpack that the player chose to put down (see InventoryService.OnPlaceRequested); taken up by the next building mode.
        BuildableItemSO itemFromBackpack = null;

        protected override IList< Recipe > Recipes => RecipiesDatabase.instance.buildingsDatabase;
        protected override string ActionVerb => "BUILD";
        protected override bool ShowsTime => false;

        [ Inject ]
        public void Construct( GameManager gameManager, HouseService houseService, MurmurService murmurService ) {
            this.gameManager = gameManager;
            this.houseService = houseService;
            this.murmurService = murmurService;
        }

        // Furniture is built inside the house, everything else on the island.
        protected override bool IsListed( CraftingCard card ) {
            bool inside = houseService != null && houseService.IsInside;
            var place = card.Item is BuildableItemSO buildable ? buildable.place : BuildPlace.Outdoors;
            if ( place != ( inside ? BuildPlace.Indoors : BuildPlace.Outdoors ) ) {
                return false;
            }
            // Only the next level of the house is offered.
            return !( card.Item is HouseUpgradeItemSO upgrade ) || upgrade.level == houseService.Level + 1;
        }

        protected override void Start(){
            base.Start();
            Assert.IsNotNull( gameManager, "Game Manager is null" );
            buildingState = gameManager.GetState( "Building" );
            buildingState.AddAtEnter( GoToBuildingMode );

            buildingPlacer = GetComponent<BuildingPlacer>();
            inventoryService.OnPlaceRequested += PlaceFromBackpack;
        }

        void OnDestroy(){
            if( inventoryService != null ) {
                inventoryService.OnPlaceRequested -= PlaceFromBackpack;
            }
        }

        // "Place" on an item of the backpack: the same placing as after building it, only the item is the price.
        void PlaceFromBackpack( ItemSO item ) {
            if( !( item is BuildableItemSO buildable ) || buildable.buildingPrefab == null ) {
                return;
            }
            if( buildable.place != ( houseService.IsInside ? BuildPlace.Indoors : BuildPlace.Outdoors ) ) {
                murmurService.Show( buildable.place == BuildPlace.Indoors ? "This goes inside the house" : "This is built outside" );
                return;
            }
            itemFromBackpack = buildable;
            Hearthglade.Gameplay.UI.Menu.Common.MenuManager.instance.CloseAll( true );
            if( gameManager.CurrentState == buildingState ) {
                GoToBuildingMode();
            } else {
                gameManager.GoToState( buildingState );
            }
        }

        public void GoToBuildingMode(){
            if( itemFromBackpack != null ) {
                var item = itemFromBackpack;
                itemFromBackpack = null;
                buildingPlacer.SetupBuildingMode( item );
                return;
            }
            if( SelectedCard == null ) {
                return;
            }
            buildingPlacer.SetupBuildingMode( SelectedCard.Recipe );
        }

        protected override void OnCardAction( CraftingCard card ) {
            Select( card );
            if( card.CanCraft ) {
                Build( card );
            }
        }

        protected override void OnFooterAction() {
            if( SelectedCard != null && SelectedCard.CanCraft ) {
                Build( SelectedCard );
            }
        }

        protected override void UpdateFooter() {
            var canBuild = SelectedCard.CanCraft;
            statusText.text = canBuild ? "Ready to build" : "Missing materials";
            statusText.color = canBuild ? CraftingPalette.Emerald700 : CraftingPalette.Rose600;
            actionButton.interactable = canBuild;
            actionButtonLabel.text = SelectedCard.Item is HouseUpgradeItemSO ? "UPGRADE" : "BUILD";
        }

        public override void CloseMenu( bool checkIfFadeOutShroud = true ) {
            base.CloseMenu( checkIfFadeOutShroud );
        }

        // A bigger house is paid for on the spot; everything else is put down in the world.
        private void Build( CraftingCard card ) {
            if( card.Item is HouseUpgradeItemSO upgrade ) {
                if( houseService.TryUpgrade( upgrade, card.Recipe.requirements ) ) {
                    CloseMenu( true );
                }
                return;
            }
            GoToBuildState();
        }

        public void GoToBuildState(){
            if( gameManager.CurrentState == buildingState ) {
                // Already placing something: swap the item, the state stays.
                GoToBuildingMode();
            } else {
                gameManager.GoToState( buildingState );
            }
            CloseMenu( true );
        }
        public void GoToDefaultState(){
            gameManager.GoToDefaultState();
        }
    }
}
