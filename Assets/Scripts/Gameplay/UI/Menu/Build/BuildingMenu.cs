using System.Collections.Generic;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Database;
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

        protected override IList< Recipe > Recipes => RecipiesDatabase.instance.buildingsDatabase;
        protected override string ActionVerb => "BUILD";
        protected override bool ShowsTime => false;

        [ Inject ]
        public void Construct( GameManager gameManager ) {
            this.gameManager = gameManager;
        }

        protected override void Start(){
            base.Start();
            Assert.IsNotNull( gameManager, "Game Manager is null" );
            buildingState = gameManager.GetState( "Building" );
            buildingState.AddAtEnter( GoToBuildingMode );

            buildingPlacer = GetComponent<BuildingPlacer>();
        }

        public void GoToBuildingMode(){
            if( SelectedCard == null ) {
                return;
            }
            buildingPlacer.SetupBuildingMode( SelectedCard.Recipe );
        }

        protected override void OnCardAction( CraftingCard card ) {
            Select( card );
            if( card.CanCraft ) {
                GoToBuildState();
            }
        }

        protected override void OnFooterAction() {
            if( SelectedCard != null && SelectedCard.CanCraft ) {
                GoToBuildState();
            }
        }

        protected override void UpdateFooter() {
            var canBuild = SelectedCard.CanCraft;
            statusText.text = canBuild ? "Ready to build" : "Missing materials";
            statusText.color = canBuild ? CraftingPalette.Emerald700 : CraftingPalette.Rose600;
            actionButton.interactable = canBuild;
            actionButtonLabel.text = "BUILD";
        }

        public override void CloseMenu( bool checkIfFadeOutShroud = true ) {
            base.CloseMenu( checkIfFadeOutShroud );
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
