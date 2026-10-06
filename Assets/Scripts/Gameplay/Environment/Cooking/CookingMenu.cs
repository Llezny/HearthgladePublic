using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.UI.Menu.Common;
using Hearthglade.Gameplay.UI.Menu.Cooking;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.Environment.Cooking {
    public class CookingMenu : Menu {

        [field: SerializeField, Header( "References" )] public Transform RecipiesContainer { get; private set; }

        [field: SerializeField] public Slider ProgressSlider { get; private set; }
        [field: SerializeField] public Slider FuelSlider { get; private set; }

        [field: SerializeField] public Button CookButton { get; private set; }

        [field: SerializeField] public GameObject CookingIngredientSlotPrefab { get; private set; }
        [field: SerializeField] public Transform IngredientSlotsHolder { get; private set; }
        [field: SerializeField] public Transform OutputSlotHolder { get; private set; }
        [field: SerializeField] public Transform FuelSlotHolder { get; private set; }
        [field: SerializeField] public TextMeshProUGUI RecipePreviewText { get; private set; }

        private CookingStation currentCookingStation;
        private CookingStationState currentCookingStationState;
        private readonly List<InventorySlotView> spawnedIngredientSlots = new();
        private InventorySlotView spawnedOutputSlot;
        private InventorySlotView spawnedFuelSlot;

        private CookingService cookingService;
        private IInventoryGridView inventoryGridView;
        private IInventorySlotViewFactory inventorySlotViewFactory;


        [ Inject ]
        public void Construct( CookingService cookingService, IInventoryGridView inventoryGridView, IInventorySlotViewFactory inventorySlotViewFactory ) {
            this.cookingService = cookingService;
            this.inventoryGridView = inventoryGridView;
            this.inventorySlotViewFactory = inventorySlotViewFactory;
        }

        private void OnEnable() {
            cookingService.ActiveCookingStationChanged += OpenMenu;
            cookingService.StationUpgraded += OnStationUpgraded;
            cookingService.RecipePreviewChanged += OnRecipePreviewChanged;
            CookButton.onClick.AddListener( StartCooking );
        }

        private void OnDisable() {
            cookingService.ActiveCookingStationChanged -= OpenMenu;
            cookingService.StationUpgraded -= OnStationUpgraded;
            cookingService.RecipePreviewChanged -= OnRecipePreviewChanged;
            CookButton.onClick.RemoveListener( StartCooking );
        }

        private void StartCooking() {
            cookingService.StartCooking();
        }

        // Upgrading (dropping a Pot into the fireplace's one slot) is detected by CookingService
        // itself; this just refreshes the UI once it happens.
        // Fires mid-drag (the Pot is dragged in/out of its slot), so nothing may be destroyed here -
        // a destroyed dragged slot strands its frame. Only slot visibility and the preview change.
        private void OnStationUpgraded( CookingStation station, CookingStationState state ) {
            if( station != currentCookingStation ) {
                return;
            }
            ApplyIngredientSlotVisibility( state );
            if( RecipePreviewText != null ) {
                RecipePreviewText.text = cookingService.GetRecipePreviewMessage( state );
            }
        }

        private void OnRecipePreviewChanged( CookingStation station, string message ) {
            if( station == currentCookingStation && RecipePreviewText != null ) {
                RecipePreviewText.text = message;
            }
        }

        public void OpenMenu( CookingStation station, CookingStationState state ) {
            base.OpenMenu();
            RemoveListeners();
            this.currentCookingStation = station;
            this.currentCookingStationState = state;
            inventoryGridView.Show();
            AddListeners( );
            RefreshForActiveStation();
            // OutputSlotHolder/FuelSlotHolder live outside "Content" (plain scene objects, not part of
            // the CookingMenu prefab), so they don't auto-hide with the rest of the menu - toggle them here.
            SetStandaloneHoldersActive( true );
        }

        override public void CloseMenu( bool fadeOutShroud = false ) {
            base.CloseMenu( fadeOutShroud );
            RemoveListeners( );
            inventoryGridView.Hide();
            SetStandaloneHoldersActive( false );
        }

        private void SetStandaloneHoldersActive( bool active ) {
            if( OutputSlotHolder != null ) {
                OutputSlotHolder.gameObject.SetActive( active );
            }
            if( FuelSlotHolder != null ) {
                FuelSlotHolder.gameObject.SetActive( active );
            }
        }

        private void RefreshForActiveStation() {
            RefreshSliders();
            SpawnIngredientSlots( currentCookingStationState );
            spawnedOutputSlot = SpawnStandaloneSlot( OutputSlotHolder, currentCookingStationState.OutputSlot, spawnedOutputSlot, "Output" );
            spawnedFuelSlot = SpawnStandaloneSlot( FuelSlotHolder, currentCookingStationState.FuelSlot, spawnedFuelSlot, "Fuel" );
            if( RecipePreviewText != null ) {
                RecipePreviewText.text = cookingService.GetRecipePreviewMessage( currentCookingStationState );
            }
        }

        private void RefreshSliders( ) {
            UpdateCookingProgressSlider( currentCookingStationState.CookingProgress );
            UpdateFuelLeftSlider( currentCookingStationState.FuelLeft );
        }

        private void AddListeners( ) {
            currentCookingStationState.CookingProgressChanged += UpdateCookingProgressSlider;
            currentCookingStationState.FuelChanged += UpdateFuelLeftSlider;
        }

        private void RemoveListeners( ) {
            if( currentCookingStationState == null ) {
                return;
            }
            currentCookingStationState.CookingProgressChanged -= UpdateCookingProgressSlider;
            currentCookingStationState.FuelChanged -= UpdateFuelLeftSlider;
        }

        private void UpdateCookingProgressSlider( float value ) {
            ProgressSlider.value = value;
        }

        private void UpdateFuelLeftSlider( float value ) {
            FuelSlider.value = value;
            CookButton.interactable = value > 0;
        }

        // Rebuilt every time the active station changes, since each CookingStation now owns its own
        // ingredient slots and unlocked slot count (tied to its CookingStationLevel).
        private void SpawnIngredientSlots( CookingStationState state ) {
            foreach( var existingSlot in spawnedIngredientSlots ) {
                if( existingSlot != null ) {
                    Destroy( existingSlot.gameObject );
                }
            }
            spawnedIngredientSlots.Clear();

            var ingredients = state.Ingredients;
            for(int i = 0; i < ingredients.Size; i++) {
                var slot = inventorySlotViewFactory.Get( CookingIngredientSlotPrefab, IngredientSlotsHolder, ingredients[i], inventoryGridView.ItemListScroll );
                slot.gameObject.name = (i).ToString();
                spawnedIngredientSlots.Add( slot );
            }
            ApplyIngredientSlotVisibility( state );
        }

        // Slots beyond the station level's IngredientSlotCount() stay spawned but hidden, so an upgrade
        // or downgrade doesn't have to rebuild them.
        private void ApplyIngredientSlotVisibility( CookingStationState state ) {
            var usableSlotCount = state.Level.IngredientSlotCount();
            for( int i = 0; i < spawnedIngredientSlots.Count; i++ ) {
                if( spawnedIngredientSlots[ i ] != null ) {
                    spawnedIngredientSlots[ i ].gameObject.SetActive( i < usableSlotCount );
                }
            }
        }

        // Used for the output slot (finished dish waits here) and the fuel slot (drop wood/sticks here
        // to burn them) - both are a single standalone slot living in their own holder, using the same
        // slot prefab and drag-drop machinery as an ingredient slot.
        private InventorySlotView SpawnStandaloneSlot( Transform holder, ItemSlot model, InventorySlotView existing, string debugName ) {
            if( holder == null ) {
                UnityEngine.Debug.LogError( $"CookingMenu.{debugName}SlotHolder is not assigned in the inspector - that slot has nowhere to render." );
                return null;
            }
            if( existing != null ) {
                Destroy( existing.gameObject );
            }
            var slot = inventorySlotViewFactory.Get( CookingIngredientSlotPrefab, holder, model, inventoryGridView.ItemListScroll );
            slot.gameObject.name = debugName;
            // Instantiate(prefab, parent) keeps the prefab's own baked-in anchoredPosition, which was
            // authored for a different parent - snap it back to dead center of its actual holder.
            var rect = (RectTransform) slot.transform;
            rect.anchoredPosition = Vector2.zero;
            rect.localPosition = Vector3.zero;
            return slot;
        }
    }

}
