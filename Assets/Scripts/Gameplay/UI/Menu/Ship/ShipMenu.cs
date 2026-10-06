using System;
using System.Collections.Generic;
using DG.Tweening;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.UI.Menu.Common;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
// The enclosing namespace ends in ".Ship" too, so the bare type name resolves to that namespace
// segment instead of Environment.Ship - an alias sidesteps the clash.
using ShipEntry = Hearthglade.Gameplay.Environment.Ship;

namespace Hearthglade.Gameplay.UI.Menu.Ship {
    public class ShipMenu : Common.Menu {

        // Dependencies
        private Inventory.InventoryService inventoryService;
        private ShipService shipService;
        private ItemCatalog catalog;
        private Hearthglade.Gameplay.Expeditions.ShipTreeService shipTree;
        private ShipTreeMenu shipTreeMenu;
        //

        [ Inject ]
        public void Construct( Inventory.InventoryService inventoryService, ShipService shipService, ItemCatalog catalog, Hearthglade.Gameplay.Expeditions.ShipTreeService shipTree, ShipTreeMenu shipTreeMenu ) {
            this.inventoryService = inventoryService;
            this.shipService = shipService;
            this.catalog = catalog;
            this.shipTree = shipTree;
            this.shipTreeMenu = shipTreeMenu;
        }

        [SerializeField] TextMeshProUGUI upgradeTitle = null;
        [SerializeField] TextMeshProUGUI upgradeDescription = null;
        [SerializeField] Transform ingredientsHolder = null;
        [SerializeField] GameObject ingredientPrefab = null;
        public List<ShipStat> shipStats = null;

        [ Tooltip( "Used only for a ship whose 'destinations' list is empty (keeps old ship prefabs working)." ) ]
        [ SerializeField ] List<Requirement> exploreCost = null;

        [ SerializeField ] Button exploreButton = null;
        [ SerializeField ] Button returnButton = null;

        [ Header( "Destination list (Phase 6: pick where to sail)" ) ]
        [ SerializeField ] Transform destinationsHolder = null;
        [ SerializeField ] GameObject destinationButtonPrefab = null;

        private ShipEntry currentShip;

        private void OnEnable( ) {
            shipService.OnShipInteraction += OpenMenu;
            returnButton.onClick.AddListener( shipService.OnReturnHome.Invoke );
        }
        
        private void OnDisable( ) {
            shipService.OnShipInteraction -= OpenMenu;
            returnButton.onClick.RemoveListener( shipService.OnReturnHome.Invoke );
        }

        private new void Start(){
            base.Start();
            for(int i = 0; i < shipStats.Count; i++ ){
                shipStats[i].statLevel = 0;
                shipStats[i].slider.value = shipStats[i].statLevel; 
                shipStats[i].slider.maxValue = shipStats[i].maxLevel; 
            }
        }

        public void OpenMenu( MapEntryBase shipEntry ) {
            currentShip = shipEntry as ShipEntry;
            OpenMenu();
            Refresh();
        }

        /// <summary>Picks which of the ship's destinations "Explore" will sail to, and refreshes its cost/description.</summary>
        public void SelectDestination( int index ) {
            // Tapping the destination that is already selected steps the depth of an expedition.
            var selected = currentShip?.SelectedDestination;
            var destinations = currentShip?.Destinations;
            if( selected != null && destinations != null && index >= 0 && index < destinations.Count && destinations[ index ] == selected ) {
                shipService.CycleDepth( selected );
            }
            currentShip?.SelectDestination( index );
            Refresh();
        }

        public void Close(){
            base.CloseMenu( true );
        }

        public void SetUpgradeInformation( string statName ){
            foreach(var stat in shipStats) {
                if ( string.Compare( statName, stat.name, StringComparison.Ordinal ) != 0 ) {
                    continue;
                }
                SetUpgradeInformation(stat);
                return;
            }
        }

        public void LevelUpStat(string statName){
            foreach(var stat in shipStats) {
                if ( string.Compare( statName, stat.name, StringComparison.Ordinal ) != 0 ) {
                    continue;
                }
                if ( !inventoryService.RemoveItems( stat.upgradeCost ) ) {
                    return;
                }
                stat.LevelUp();
                Refresh();
                DisableUpgradeIfMaxed(stat);
                return;
            }
        }

        public void Explore() {
            if ( !shipService.CanExplore ) {
                return;
            }
            var destination = currentShip?.SelectedDestination;
            if ( !inventoryService.TryPay( destination?.foodPoints ?? 0f, destination?.cost ?? exploreCost ) ) {
                return;
            }
            Refresh();

            shipService.OnShipUse( );
        }

        /// <summary> if it is max level disable button and change description of stat.</summary>
        private void DisableUpgradeIfMaxed(ShipStat stat) {
            if ( stat.statLevel < stat.maxLevel ) {
                return;
            }
            stat.upgradeButton.interactable = false;
            stat.description = "It can not be done any better";
        }

        private void SetUpgradeInformation( ShipStat stat ){
            upgradeTitle.text = stat.name + RomanNumber.To(stat.statLevel) ;
            upgradeDescription.text = stat.description;
            SetIngredients( stat.upgradeCost, stat.upgradeButton, stat.statLevel >= stat.maxLevel );
        }

        public void SetExploreInformation(){
            bool isShipBroken = shipStats[0].statLevel == 0;
            var destination = currentShip?.SelectedDestination;
            if( isShipBroken ){
                upgradeTitle.text = "Broken ship!";
                upgradeDescription.text = "You cannot use the ship if it is damaged, repair it to sail away.";
            }
            else if( destination != null ){
                upgradeTitle.text = destination.displayName;
                upgradeDescription.text = destination.description;
            }
            else{
                upgradeTitle.text = "Explore!";
                upgradeDescription.text = "Discover new islands!";
            }
            exploreButton.interactable = isShipBroken;
            if ( !shipService.CanExplore ) {
                // Away from Home the only way is back.
                upgradeTitle.text = "Set sail home";
                upgradeDescription.text = "Return to your island to prepare the next expedition.";
                SetIngredients( new List<Requirement>( ), exploreButton );
                exploreButton.interactable = false;
                return;
            }
            SetIngredients( destination?.cost ?? exploreCost, exploreButton );

            // Food comes out of the backpack by itself (the cheapest first); the line only tells whether there is enough of it.
            float food = destination?.foodPoints ?? 0f;
            if ( food > 0f ) {
                float owned = inventoryService.FoodPointsAvailable;
                bool enough = owned >= food;
                upgradeDescription.text += $"\nFood supplies: {Mathf.FloorToInt( owned )}/{Mathf.CeilToInt( food )}" + ( enough ? "" : " (not enough)" );
                exploreButton.interactable &= enough;
            }
        }

        /// <summary>Rebuilds the destination list under destinationsHolder from the interacted ship's data.
        /// A ship without a configured destinations list (or an unwired holder/prefab) leaves the old,
        /// single hardcoded "Explore" button as the only way to travel.</summary>
        private void RefreshDestinations(){
            if( destinationsHolder == null || destinationButtonPrefab == null || currentShip == null ) {
                return;
            }
            foreach( Transform child in destinationsHolder ) {
                Destroy( child.gameObject );
            }
            var destinations = currentShip.Destinations;
            for( int i = 0; i < destinations.Count; i++ ) {
                var index = i;
                var row = Instantiate( destinationButtonPrefab, destinationsHolder ).GetComponent<ShipDestinationButtonView>();
                bool selected = currentShip.SelectedDestination == destinations[ index ];
                row.Setup( destinations[ index ].displayName, selected, () => SelectDestination( index ) );
            }
            // The ship tree opens from the last row of the list (provisional, until the menu has its design).
            if( shipService.CanExplore && shipTreeMenu != null ) {
                var upgrades = Instantiate( destinationButtonPrefab, destinationsHolder ).GetComponent<ShipDestinationButtonView>();
                upgrades.Setup( $"Ship upgrades ({shipTree.Points} pts)", false, () => shipTreeMenu.OpenMenu() );
            }
        }

        private void SetIngredients( List<Requirement> ingredients, Button confirmButton, bool isLevelMaxed = false ){
            foreach( Transform child in ingredientsHolder ){
                Destroy(child.gameObject);
            }
            foreach( var ingredient in ingredients ){
                var ingredientItem = catalog.GetAsset( ingredient.Item );
                var ing = Instantiate( ingredientPrefab, ingredientsHolder );
                ing.GetComponent<Image>().sprite = ingredientItem.icon;

                var slot = ing.GetComponent<RecipeSlot>();
                var amountOfOwnedTargetItems = inventoryService.Count( ingredient.Item );
                slot.EnableQuantityText();
                slot.SetQuantityText( ingredient.requiredQuantity, amountOfOwnedTargetItems );
            }
            confirmButton.interactable = CanAfford( ingredients ) && !isLevelMaxed;
        }

        private bool CanAfford( List<Requirement> ingredients ) {
            return inventoryService.HasItems( ingredients );
        }

        private void Refresh(){
            RefreshDestinations();
            // Explore/destination info owns the shared title, description and cost icons by default.
            // Stats only get their OWN button's enabled state refreshed here - re-rendering their
            // info into the shared panel on every refresh used to stomp whatever the player just
            // selected (destination or stat), e.g. hiding a destination's real cost behind a stat's.
            SetExploreInformation();
            for(int i = 0; i < shipStats.Count; i++ ){
                var stat = shipStats[i];
                stat.upgradeButton.interactable = stat.statLevel < stat.maxLevel && CanAfford( stat.upgradeCost );
            }
        }

        public void ShowReturnButton(){
            returnButton.gameObject.SetActive( true );
        }

        public void HideReturnButton(){
            returnButton.gameObject.SetActive( false );
        }
    }
}
