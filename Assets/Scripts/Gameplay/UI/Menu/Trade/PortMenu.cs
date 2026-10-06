using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Core.Trade;
using Hearthglade.Gameplay.Trade;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Trade
{
    /// <summary>
    /// Barter with a port: tap goods of the backpack to offer them, tap goods of the port to ask for them, and swap when the
    /// bar says the port is content. Nobody sees prices; only the balance of the two sides. Rows are made in code, the layout
    /// (see PortMenuBuilder) only provides the lists, the bar and the buttons.
    /// </summary>
    public class PortMenu : Common.Menu {

        [ SerializeField ] TextMeshProUGUI titleText = null;
        [ SerializeField ] TextMeshProUGUI relationText = null;
        [ SerializeField ] TextMeshProUGUI hintText = null;
        [ SerializeField ] Image balanceFill = null;
        [ SerializeField ] RectTransform contractsList = null;
        [ SerializeField ] RectTransform yourGoodsList = null;
        [ SerializeField ] RectTransform theirGoodsList = null;
        [ SerializeField ] RectTransform giveList = null;
        [ SerializeField ] RectTransform takeList = null;
        [ SerializeField ] Button tradeButton = null;
        [ SerializeField ] Button closeButton = null;

        // Dependencies
        private TradeService tradeService;
        private PortService portService;
        private MessagePopup messagePopup;
        //

        private readonly TradeBasket give = new();
        private readonly TradeBasket take = new();
        private PortEntry port;

        [ Inject ]
        public void Construct( TradeService tradeService, PortService portService, MessagePopup messagePopup ) {
            this.tradeService = tradeService;
            this.portService = portService;
            this.messagePopup = messagePopup;
        }

        private void OnEnable() {
            closeButton.onClick.AddListener( () => CloseMenu() );
            tradeButton.onClick.AddListener( Trade );
        }

        private void OnDisable() {
            closeButton.onClick.RemoveAllListeners();
            tradeButton.onClick.RemoveAllListeners();
        }

        public void Open( PortEntry portToTradeWith ) {
            if( isOpen ) {
                return;
            }
            port = portToTradeWith;
            give.Clear();
            take.Clear();
            base.OpenMenu();
            Refresh();
        }

        private void Trade() {
            if( port == null || !tradeService.TryTrade( port, give, take ) ) {
                return;
            }
            give.Clear();
            take.Clear();
            var text = "Swapped";
            messagePopup.AddTextMessageToQueue( ref text );
            Refresh();
        }

        private void Refresh() {
            titleText.text = port.Asset.DisplayName;
            relationText.text = RelationText();
            RebuildContracts();
            RebuildYourGoods();
            RebuildTheirGoods();
            RebuildBasket( giveList, give );
            RebuildBasket( takeList, take );
            RefreshBalance();
        }

        private string RelationText() {
            int level = port.Profile.LevelFor( port.State.RelationPoints );
            var thresholds = port.Profile.LevelThresholds;
            return level >= thresholds.Count
                ? $"Trust: level {level} (the best)"
                : $"Trust: level {level}  -  {Mathf.FloorToInt( ( float ) port.State.RelationPoints )} / {Mathf.CeilToInt( ( float ) thresholds[ level ] )}";
        }

        // Orders: "bring X, get Y". Tapping one hands in the goods when the backpack holds them (and has room for the reward).
        private void RebuildContracts() {
            if( contractsList == null ) {
                return;
            }
            Clear( contractsList );
            int completed = portService.CompletedExpeditions;
            foreach( var contract in tradeService.ContractsOf( port ) ) {
                if( !tradeService.TryGetDefinition( contract.Item, out var asked ) || !tradeService.TryGetDefinition( contract.RewardItem, out var reward ) ) {
                    continue;
                }
                var captured = contract;
                bool ready = tradeService.CanFulfil( captured );
                string label = $"{contract.Count} {NameOf( asked )} -> {contract.RewardCount} {NameOf( reward )}";
                string right = $"{Mathf.Min( tradeService.Owned( contract.Item ), contract.Count )}/{contract.Count}";
                var asset = tradeService.AssetOf( asked );
                TradeRow.Create( contractsList, asset != null ? asset.icon : null, label + $"  ({contract.ExpiresAt - completed} trips left)", right, !ready, !ready ? null : () => {
                    if( tradeService.TryFulfil( port, captured ) ) {
                        var text = "Order done";
                        messagePopup.AddTextMessageToQueue( ref text );
                    } else {
                        var text = "No room for the reward";
                        messagePopup.AddTextMessageToQueue( ref text );
                    }
                    Refresh();
                } );
            }
        }

        private string NameOf( ItemDefinition item ) {
            var asset = tradeService.AssetOf( item );
            return asset != null ? asset.itemName : item.Id.Value;
        }

        private void RebuildYourGoods() {
            Clear( yourGoodsList );
            foreach( var ( item, owned ) in tradeService.OwnedTradables() ) {
                int left = owned - give.CountOf( item.Id );
                if( left <= 0 ) {
                    continue;
                }
                var captured = item;
                bool wanted = port.Profile.Wants.ContainsKey( item.Id );
                AddRow( yourGoodsList, captured, ( wanted ? "* " : "" ) + $"x{left}", false, () => {
                    give.Add( captured, 1, tradeService.Owned( captured.Id ) );
                    Refresh();
                } );
            }
        }

        private void RebuildTheirGoods() {
            Clear( theirGoodsList );
            int level = port.Profile.LevelFor( port.State.RelationPoints );
            foreach( var offer in port.Profile.Offers ) {
                if( !tradeService.TryGetDefinition( offer.Item, out var item ) ) {
                    continue;
                }
                bool locked = level < offer.MinRelationLevel;
                int stock = port.State.StockOf( offer.Item ) - take.CountOf( offer.Item );
                if( stock <= 0 && !locked ) {
                    continue;
                }
                var captured = item;
                int limit = port.State.StockOf( offer.Item );
                AddRow( theirGoodsList, captured, locked ? "locked" : $"x{stock}", locked, locked ? null : () => {
                    take.Add( captured, 1, limit );
                    Refresh();
                } );
            }
        }

        private void RebuildBasket( RectTransform list, TradeBasket basket ) {
            Clear( list );
            foreach( var item in basket.Items ) {
                var captured = item;
                AddRow( list, captured, $"x{basket.CountOf( item.Id )}", false, () => {
                    basket.Remove( captured.Id );
                    Refresh();
                } );
            }
        }

        private void RefreshBalance() {
            var quote = tradeService.Quote( port, give, take, out bool fits );
            double ratio = quote.TakeValue > 0.0 ? quote.GiveValue / quote.TakeValue : ( quote.GiveValue > 0.0 ? 1.0 : 0.0 );
            balanceFill.rectTransform.anchorMax = new Vector2( Mathf.Clamp01( ( float ) ratio ), 1f );
            balanceFill.color = quote.IsAcceptable ? CraftingPalette.Emerald500 : CraftingPalette.Amber500;
            hintText.text = quote.IsAcceptable && !fits ? "Your backpack is too full" : HintFor( quote.Problem );
            tradeButton.interactable = quote.IsAcceptable && fits;
        }

        private static string HintFor( TradeProblem problem ) {
            switch( problem ) {
                case TradeProblem.NothingOffered: return "Offer something from your goods";
                case TradeProblem.NothingRequested: return "Pick what you want in return";
                case TradeProblem.NotEnough: return "Offer a little more";
                case TradeProblem.OutOfStock: return "They do not have that many";
                case TradeProblem.Locked: return "Earn their trust to buy this";
                case TradeProblem.NotSold:
                case TradeProblem.NotTradable: return "That is not for trade";
                default: return "A fair swap";
            }
        }

        private static void Clear( RectTransform list ) {
            for( int i = list.childCount - 1; i >= 0; i-- ) {
                Destroy( list.GetChild( i ).gameObject );
            }
        }

        private void AddRow( RectTransform list, ItemDefinition item, string right, bool dim, UnityAction onClick ) {
            var asset = tradeService.AssetOf( item );
            string label = asset != null ? asset.itemName : item.Id.Value;
            TradeRow.Create( list, asset != null ? asset.icon : null, label, right, dim, onClick );
        }
    }

    /// <summary>One line of a list: icon, name and a count, a button when it has an action.</summary>
    public static class TradeRow {

        private const float Height = 34f;

        public static void Create( RectTransform parent, Sprite icon, string label, string right, bool dim, UnityAction onClick ) {
            var row = new GameObject( "Row", typeof( RectTransform ), typeof( Image ), typeof( HorizontalLayoutGroup ), typeof( LayoutElement ) );
            row.transform.SetParent( parent, false );
            var background = row.GetComponent<Image>();
            background.color = dim ? CraftingPalette.Sand : CraftingPalette.Surface;

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset( 4, 6, 3, 3 );
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.GetComponent<LayoutElement>().minHeight = Height;

            var iconObject = new GameObject( "Icon", typeof( RectTransform ), typeof( Image ), typeof( LayoutElement ) );
            iconObject.transform.SetParent( row.transform, false );
            var iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.enabled = icon != null;
            iconImage.raycastTarget = false;
            var iconSize = iconObject.GetComponent<LayoutElement>();
            iconSize.preferredWidth = 26;
            iconSize.preferredHeight = 26;

            var name = Text( row.transform, "Name", label, 12f, dim ? CraftingPalette.Ink400 : CraftingPalette.Ink900, TextAlignmentOptions.MidlineLeft );
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1;

            var count = Text( row.transform, "Count", right, 12f, dim ? CraftingPalette.Ink400 : CraftingPalette.Ink600, TextAlignmentOptions.MidlineRight );
            count.gameObject.GetComponent<LayoutElement>().preferredWidth = 44;

            if( onClick != null ) {
                var button = row.AddComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener( onClick );
            } else {
                background.raycastTarget = false;
            }
        }

        private static TextMeshProUGUI Text( Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment ) {
            var go = new GameObject( name, typeof( RectTransform ), typeof( TextMeshProUGUI ), typeof( LayoutElement ) );
            go.transform.SetParent( parent, false );
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }
    }
}
