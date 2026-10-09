using System.Collections.Generic;
using Hearthglade.Gameplay.Database;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    // A tabbed list of recipe cards with a footer for the selected one. Crafting and the building menu are the same
    // screen (built by CraftingMenuBuilder); they differ in the recipe list and in what the action buttons do.
    public abstract class RecipeMenu : Common.Menu {
        [ Header( "Header" ) ]
        [ SerializeField ] Button closeButton = null;

        [ Header( "Tabs" ) ]
        [ SerializeField ] GameObject tabsBar = null;
        [ SerializeField ] Transform tabsRoot = null;
        [ SerializeField ] CraftingCategoryTab tabPrefab = null;
        [ SerializeField ] Sprite[] categoryIcons = null; // indexed by RecipeCategory
        [ SerializeField ] Sprite[] categoryActiveIcons = null; // optional variant for the active tab, indexed the same way

        [ Header( "Recipe list" ) ]
        [ SerializeField ] Transform cardsRoot = null;
        [ SerializeField ] CraftingCard cardPrefab = null;
        [ SerializeField ] ScrollRect cardsScroll = null;

        [ Header( "Footer" ) ]
        [ SerializeField ] Image selectedItemIcon = null;
        [ SerializeField ] TextMeshProUGUI selectedItemName = null;
        [ SerializeField ] protected TextMeshProUGUI statusText = null;
        [ SerializeField ] protected Button actionButton = null;
        [ SerializeField ] protected TextMeshProUGUI actionButtonLabel = null;
        [ SerializeField ] GameObject actionButtonGlow = null;

        protected Inventory.InventoryService inventoryService;
        protected ItemCatalog catalog;

        readonly List< CraftingCard > cards = new List< CraftingCard >();
        readonly List< CraftingCategoryTab > tabs = new List< CraftingCategoryTab >();
        bool isInitialized;

        protected CraftingCard SelectedCard { get; private set; }

        // What the recipes of this menu are and what pressing a card's (or the footer's) button does.
        protected abstract IList< Recipe > Recipes { get; }
        protected abstract string ActionVerb { get; }
        protected abstract bool ShowsTime { get; }
        protected virtual bool IsBusy => false;
        // A card the menu has but does not list right now (checked every time a category is shown, so it can depend on where the player is).
        protected virtual bool IsListed( CraftingCard card ) => true;
        protected abstract void OnCardAction( CraftingCard card );
        protected abstract void OnFooterAction();
        // Refresh what depends on the selection: status text, the footer button state and label.
        protected abstract void UpdateFooter();
        protected virtual void OnInitialized() { }
        protected virtual void OnSelectionChanged() { }

        [ Inject ]
        public void ConstructRecipeMenu( Inventory.InventoryService inventoryService, ItemCatalog catalog ) {
            this.inventoryService = inventoryService;
            this.catalog = catalog;
        }

        protected virtual void OnEnable() {
            OnOpen += OpenCallback;
        }

        protected virtual void OnDisable() {
            OnOpen -= OpenCallback;
        }

        private void OpenCallback() {
            if( !isInitialized ) {
                Initialize();
            }
            SetCategory( RecipeCategory.All );
            RefreshCards();
        }

        // The recipe database is only guaranteed to be loaded by the time the menu is opened,
        // so the list is built lazily on the first open.
        void Initialize() {
            isInitialized = true;

            closeButton.onClick.AddListener( () => CloseMenu( fadeOutShroud: true ) );
            actionButton.onClick.AddListener( OnFooterAction );

            foreach( var recipe in Recipes ) {
                if( !recipe.isUnlocked || !catalog.TryGetAsset( recipe.CraftedItem, out _ ) ) {
                    continue;
                }
                var card = Instantiate( cardPrefab, cardsRoot );
                card.Bind( recipe, catalog, ActionVerb, ShowsTime, Select, OnCardAction );
                cards.Add( card );
            }

            var listedCategories = 0;
            foreach( var category in RecipeCategories.Tabs ) {
                var hasRecipes = category == RecipeCategory.All || cards.Exists( card => card.Category == category );
                if( !hasRecipes ) {
                    continue;
                }
                var tab = Instantiate( tabPrefab, tabsRoot );
                tab.Bind( category, categoryIcons[ ( int ) category ], categoryActiveIcons[ ( int ) category ], SetCategory );
                tabs.Add( tab );
                if( category != RecipeCategory.All ) {
                    listedCategories++;
                }
            }
            // "All" next to a single category would show the same list twice.
            tabsBar.SetActive( listedCategories > 1 );

            OnInitialized();
        }

        void SetCategory( RecipeCategory category ) {
            foreach( var tab in tabs ) {
                tab.SetActive( tab.Category == category );
            }

            foreach( var card in cards ) {
                card.gameObject.SetActive( RecipeCategories.Contains( category, card.Category ) && IsListed( card ) );
            }

            if( SelectedCard == null || !SelectedCard.gameObject.activeSelf ) {
                Select( cards.Find( card => card.gameObject.activeSelf ) );
            }
            cardsScroll.verticalNormalizedPosition = 1f;
        }

        protected void Select( CraftingCard card ) {
            if( SelectedCard != null ) {
                SelectedCard.SetSelected( false );
            }
            SelectedCard = card;
            if( SelectedCard != null ) {
                SelectedCard.SetSelected( true );
            }
            OnSelectionChanged();
            RefreshFooter();
        }

        protected void RefreshCards() {
            foreach( var card in cards ) {
                card.Refresh( inventoryService, IsBusy );
            }
            RefreshFooter();
        }

        protected void RefreshFooter() {
            if( SelectedCard == null ) {
                return;
            }
            selectedItemIcon.sprite = SelectedCard.Item.icon;
            selectedItemName.text = SelectedCard.Item.itemName;
            UpdateFooter();
            actionButtonGlow.SetActive( actionButton.interactable );
        }
    }
}
