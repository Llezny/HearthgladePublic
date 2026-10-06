using System;
using System.Collections.Generic;
using System.Text;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    // One row of the crafting list: the crafted item, what it costs and a quick "craft x1" button.
    public class CraftingCard : MonoBehaviour
    {
        [ SerializeField ] Button selectButton = null;
        [ SerializeField ] Image border = null;
        [ SerializeField ] Image background = null;
        [ SerializeField ] Image icon = null;
        [ SerializeField ] Image iconBoxBorder = null;
        [ SerializeField ] Image iconBoxGradient = null; // amber tint of the selected card's icon box
        [ SerializeField ] TextMeshProUGUI quantityText = null;
        [ SerializeField ] TextMeshProUGUI nameText = null;
        [ SerializeField ] TextMeshProUGUI descriptionText = null;
        [ SerializeField ] TextMeshProUGUI timeText = null;
        [ SerializeField ] TextMeshProUGUI extraText = null;
        [ SerializeField ] GameObject readyBadge = null;
        [ SerializeField ] TextMeshProUGUI readyBadgeLabel = null;
        [ SerializeField ] GameObject timeGroup = null; // clock + seconds; only meaningful for timed recipes
        [ SerializeField ] Transform chipsRoot = null;
        [ SerializeField ] RequirementChip chipPrefab = null;
        [ SerializeField ] Button craftButton = null;
        [ SerializeField ] Image craftButtonImage = null;
        [ SerializeField ] Image craftButtonGradient = null; // emerald gradient, shown while highlighted
        [ SerializeField ] Image craftButtonGlow = null;
        [ SerializeField ] TextMeshProUGUI craftButtonLabel = null;

        readonly List< RequirementChip > chips = new List< RequirementChip >();
        readonly List< Sprite > requirementIcons = new List< Sprite >();
        ItemCatalog catalog;
        string verb;
        bool isSelected;

        public Recipe Recipe { get; private set; }
        public ItemSO Item { get; private set; }
        public RecipeCategory Category { get; private set; }

        // verb is what the card's button does ("CRAFT", "BUILD"); showTime hides the clock for recipes that are not timed.
        public void Bind( Recipe recipe, ItemCatalog catalog, string verb, bool showTime,
            Action< CraftingCard > onSelect, Action< CraftingCard > onQuickAction ) {
            this.catalog = catalog;
            this.verb = verb;
            Recipe = recipe;
            readyBadgeLabel.text = "READY TO " + verb;
            timeGroup.SetActive( showTime );
            Item = catalog.GetAsset( recipe.CraftedItem );
            Category = RecipeCategories.FromItemType( Item.itemType );

            icon.sprite = Item.icon;
            quantityText.text = "x" + recipe.craftedItemQuantity;
            nameText.text = Item.itemName;
            descriptionText.text = Item.itemDescription;
            timeText.text = FormatSeconds( recipe.craftTime );
            extraText.text = BuildExtraText( recipe, Item );

            foreach( var requirement in recipe.requirements ) {
                chips.Add( Instantiate( chipPrefab, chipsRoot ) );
                requirementIcons.Add( catalog.GetAsset( requirement.Item ).icon );
            }

            selectButton.onClick.AddListener( () => onSelect( this ) );
            craftButton.onClick.AddListener( () => onQuickAction( this ) );
            SetSelected( false );
        }

        public void Refresh( InventoryService inventoryService, bool isBusy ) {
            var missing = string.Empty;
            for( int i = 0; i < Recipe.requirements.Length; i++ ) {
                var requirement = Recipe.requirements[i];
                var owned = inventoryService.Count( requirement.Item );
                chips[i].Set( requirementIcons[i], owned, requirement.requiredQuantity );
                if( missing.Length == 0 && owned < requirement.requiredQuantity ) {
                    missing = catalog.GetAsset( requirement.Item ).itemName;
                }
            }

            CanCraft = missing.Length == 0;
            craftButton.interactable = CanCraft && !isBusy;
            craftButtonImage.color = CanCraft ? CraftingPalette.Emerald600 : CraftingPalette.Sand;
            craftButtonLabel.color = CanCraft ? Color.white : CraftingPalette.Ink400;
            craftButtonLabel.text = CanCraft ? verb : "MISSING: " + missing.ToUpper();
            RefreshBadge();
        }

        public bool CanCraft { get; private set; }

        public void SetSelected( bool selected ) {
            isSelected = selected;
            border.color = selected ? CraftingPalette.Amber500 : CraftingPalette.Border;
            background.color = selected ? CraftingPalette.SurfaceWarm : CraftingPalette.Surface;
            iconBoxBorder.color = selected ? CraftingPalette.WithAlpha( CraftingPalette.Amber500, 0.5f ) : CraftingPalette.Border;
            iconBoxGradient.enabled = selected;
            RefreshBadge();
        }

        // The selected card that can be crafted is the "featured" one in the design: badge, gradient button and glow.
        void RefreshBadge() {
            var featured = isSelected && CanCraft;
            readyBadge.SetActive( featured );
            craftButtonGradient.enabled = featured;
            craftButtonGlow.enabled = featured;
        }

        static string BuildExtraText( Recipe recipe, ItemSO item ) {
            var text = new StringBuilder();
            if( item.Damage > 0 ) {
                Append( text, CraftingPalette.Cyan600, "Atak: " + item.Damage );
            }
            if( !string.IsNullOrEmpty( recipe.tag ) ) {
                Append( text, CraftingPalette.Amber600, recipe.tag );
            }
            if( recipe.xp > 0 ) {
                Append( text, CraftingPalette.Emerald700, "+" + recipe.xp + " XP" );
            }
            return text.ToString();
        }

        static void Append( StringBuilder text, Color color, string value ) {
            if( text.Length > 0 ) {
                text.Append( "  " );
            }
            text.Append( "<color=#" ).Append( CraftingPalette.ToHex( color ) ).Append( ">" ).Append( value ).Append( "</color>" );
        }

        public static string FormatSeconds( float seconds ) {
            return Mathf.Approximately( seconds, Mathf.Round( seconds ) ) ? Mathf.RoundToInt( seconds ) + "s" : seconds.ToString( "0.#" ) + "s";
        }

        void OnDestroy() {
            selectButton.onClick.RemoveAllListeners();
            craftButton.onClick.RemoveAllListeners();
        }
    }
}
