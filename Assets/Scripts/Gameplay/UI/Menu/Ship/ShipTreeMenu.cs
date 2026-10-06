using System.Collections.Generic;
using Hearthglade.Core.Expedition;
using Hearthglade.Gameplay.Expeditions;
using Hearthglade.Gameplay.UI.Menu.Trade;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Ship
{
    /// <summary>
    /// The ship tree (docs/EXPLORATION_LOOP_PLAN.md, phase 7): every branch as two rows, what it does now and what its next tier costs;
    /// tapping the cost row buys the tier. The rows are made in code, the layout (see ShipTreeMenuBuilder) only provides the list.
    /// </summary>
    public class ShipTreeMenu : Common.Menu {

        [ SerializeField ] TextMeshProUGUI titleText = null;
        [ SerializeField ] TextMeshProUGUI pointsText = null;
        [ SerializeField ] RectTransform list = null;
        [ SerializeField ] Button closeButton = null;

        private ShipTreeService shipTree;

        [ Inject ]
        public void Construct( ShipTreeService shipTree ) {
            this.shipTree = shipTree;
        }

        private void OnEnable() {
            closeButton.onClick.AddListener( () => CloseMenu() );
        }

        private void OnDisable() {
            closeButton.onClick.RemoveAllListeners();
            if( shipTree != null ) {
                shipTree.Changed -= Refresh;
            }
        }

        public override void OpenMenu() {
            if( isOpen ) {
                return;
            }
            base.OpenMenu();
            shipTree.Changed += Refresh;
            Refresh();
        }

        public override void CloseMenu( bool fadeOutShroud = false ) {
            shipTree.Changed -= Refresh;
            base.CloseMenu( fadeOutShroud );
        }

        private void Refresh() {
            titleText.text = "Ship upgrades";
            pointsText.text = $"Expedition points: {shipTree.Points}   (1 per trip, +1 for a new depth)";
            for( int i = list.childCount - 1; i >= 0; i-- ) {
                Destroy( list.GetChild( i ).gameObject );
            }
            foreach( var entry in shipTree.Entries ) {
                AddBranch( entry );
            }
        }

        private void AddBranch( ShipTreeEntry entry ) {
            int level = shipTree.LevelOf( entry );
            int max = entry.Node.Tiers.Count;
            TradeRow.Create( list, null, $"{entry.Asset.DisplayName} {level}/{max}: {EffectNow( entry )}", "", true, null );
            var next = shipTree.NextTier( entry );
            if( next == null ) {
                TradeRow.Create( list, null, "Bought out", "", true, null );
                return;
            }
            bool can = shipTree.CanBuy( entry );
            TradeRow.Create( list, null, $"Next: {CostText( next )}  ({EffectOf( entry.Node.Effect, next.Magnitude )})", can ? "Buy" : "", !can, can ? () => shipTree.TryBuy( entry ) : null );
        }

        private string EffectNow( ShipTreeEntry entry ) {
            float total = 0f;
            for( int i = 0; i < shipTree.LevelOf( entry ); i++ ) {
                total += entry.Node.Tiers[ i ].Magnitude;
            }
            return total <= 0f ? "not bought yet" : EffectOf( entry.Node.Effect, total );
        }

        private static string EffectOf( ShipEffect effect, float magnitude ) {
            switch( effect ) {
                case ShipEffect.ExpeditionDiscount: return $"trips cost {Mathf.RoundToInt( magnitude * 100f )}% less";
                case ShipEffect.BackpackRows: return $"{Mathf.RoundToInt( magnitude )} more backpack row{( Mathf.RoundToInt( magnitude ) == 1 ? "" : "s" )}";
                default: return $"{Mathf.RoundToInt( magnitude * 100f )}% chance of one more when gathering";
            }
        }

        private static string CostText( ShipNodeTier tier ) {
            var parts = new List<string> { $"{tier.Points} pts" };
            foreach( var ore in tier.Ore ) {
                parts.Add( $"{ore.Count} {ore.Id}" );
            }
            return string.Join( ", ", parts );
        }
    }
}
