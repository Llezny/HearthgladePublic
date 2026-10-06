using System;
using System.Collections;
using System.Collections.Generic;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    // Crafting screen: the recipe list plus a footer to craft the selected item in bulk (quantity selector, timed batches).
    public class Crafting : RecipeMenu {
        [ Header( "Crafting footer" ) ]
        [ SerializeField ] Button decreaseButton = null;
        [ SerializeField ] Button increaseButton = null;
        [ SerializeField ] Button maxButton = null;
        [ SerializeField ] TextMeshProUGUI quantityText = null;
        [ SerializeField ] Image actionButtonProgress = null;
        [ SerializeField ] TextMeshProUGUI durationText = null;

        private ControlEvents controlEvents;

        int quantity = 1;
        bool isCrafting;

        /// <summary>Raised once per finished batch; a future XP system can subscribe to it.</summary>
        public event Action< Recipe > OnCrafted;

        protected override IList< Recipe > Recipes => RecipiesDatabase.instance.craftingDatabase;
        protected override string ActionVerb => "CRAFT";
        protected override bool ShowsTime => true;
        protected override bool IsBusy => isCrafting;

        [ Inject ]
        public void Construct( ControlEvents controlEvents ) {
            this.controlEvents = controlEvents;
        }

        protected override void OnEnable() {
            base.OnEnable();
            controlEvents.onCraftingButton += OpenMenu;
        }

        protected override void OnDisable() {
            base.OnDisable();
            controlEvents.onCraftingButton -= OpenMenu;
        }

        protected override void OnInitialized() {
            decreaseButton.onClick.AddListener( () => SetQuantity( quantity - 1 ) );
            increaseButton.onClick.AddListener( () => SetQuantity( quantity + 1 ) );
            maxButton.onClick.AddListener( () => SetQuantity( MaxCraftable( SelectedCard ) ) );
        }

        protected override void OnSelectionChanged() {
            quantity = 1;
        }

        protected override void OnCardAction( CraftingCard card ) {
            StartCrafting( card, 1 );
        }

        protected override void OnFooterAction() {
            StartCrafting( SelectedCard, quantity );
        }

        void SetQuantity( int value ) {
            quantity = Mathf.Clamp( value, 1, Mathf.Max( 1, MaxCraftable( SelectedCard ) ) );
            RefreshFooter();
        }

        protected override void UpdateFooter() {
            var maxCraftable = MaxCraftable( SelectedCard );
            quantity = Mathf.Clamp( quantity, 1, Mathf.Max( 1, maxCraftable ) );
            var canCraft = maxCraftable >= 1;

            statusText.text = "You can craft: <color=#FFFFFF><u>x" + maxCraftable + "</u></color>";
            statusText.color = canCraft ? CraftingPalette.Emerald700 : CraftingPalette.Ink500;
            quantityText.text = quantity.ToString();

            decreaseButton.interactable = !isCrafting && quantity > 1;
            increaseButton.interactable = !isCrafting && quantity < maxCraftable;
            maxButton.interactable = !isCrafting && maxCraftable > 1;
            actionButton.interactable = !isCrafting && canCraft;
            actionButtonLabel.text = isCrafting ? "CRAFTING..." : "CRAFT (X" + quantity + ")";

            var totalSeconds = Mathf.RoundToInt( SelectedCard.Recipe.craftTime * quantity );
            durationText.text = "Duration: <b>" + totalSeconds + ( totalSeconds == 1 ? " second" : " seconds" ) + "</b>";
        }

        int MaxCraftable( CraftingCard card ) {
            if( card == null ) {
                return 0;
            }
            var max = int.MaxValue;
            foreach( var requirement in card.Recipe.requirements ) {
                var owned = inventoryService.Count( requirement.Item );
                max = Mathf.Min( max, owned / Mathf.Max( 1, requirement.requiredQuantity ) );
            }
            return max == int.MaxValue ? 0 : max;
        }

        void StartCrafting( CraftingCard card, int batches ) {
            if( isCrafting || card == null || card.Item == null ) {
                return;
            }
            StartCoroutine( CraftRoutine( card.Recipe, card.Item, batches ) );
        }

        // Ingredients of a batch are taken when it starts; the result is delivered when its timer ends.
        IEnumerator CraftRoutine( Recipe recipe, ItemSO item, int batches ) {
            isCrafting = true;
            RefreshCards();

            for( int batch = 0; batch < batches; batch++ ) {
                if( !CanAfford( recipe ) ) {
                    break;
                }
                if( !inventoryService.CanFit( item, recipe.craftedItemQuantity ) ) {
                    inventoryService.ReportNoRoom( item, recipe.craftedItemQuantity );
                    break;
                }
                if( !inventoryService.RemoveItems( recipe.requirements ) ) {
                    break;
                }
                RefreshCards();

                var elapsed = 0f;
                while( elapsed < recipe.craftTime ) {
                    elapsed += Time.unscaledDeltaTime;
                    SetProgress( ( batch + Mathf.Clamp01( elapsed / recipe.craftTime ) ) / batches );
                    yield return null;
                }

                inventoryService.AddItem( item, recipe.craftedItemQuantity );
                OnCrafted?.Invoke( recipe );
            }

            SetProgress( 0f );
            isCrafting = false;
            RefreshCards();
        }

        // The progress overlay is a sliced image, so it grows via its anchors instead of Image.fillAmount.
        void SetProgress( float progress ) {
            actionButtonProgress.rectTransform.anchorMax = new Vector2( Mathf.Clamp01( progress ), 1f );
        }

        bool CanAfford( Recipe recipe ) {
            return inventoryService.HasItems( recipe.requirements );
        }
    }
}
