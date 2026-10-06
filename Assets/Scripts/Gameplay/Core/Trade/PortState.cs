using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;

namespace Hearthglade.Core.Trade {

    [Serializable]
    public class PortStockEntry {
        public string Item;
        public int Count;
    }

    [Serializable]
    public class PortSoldEntry {
        public string Item;
        public float Units;
    }

    /// <summary>The saved form of a <see cref="PortState"/>.</summary>
    [Serializable]
    public class PortStateData {
        public bool Discovered;
        public int MapId;
        public double Relation;
        public int LastRestock;
        public List<PortStockEntry> Stock = new List<PortStockEntry>();
        public List<PortSoldEntry> Sold = new List<PortSoldEntry>();

        // Null in a save that predates contracts.
        public ContractBoardData Contracts;
    }

    /// <summary>What changes about a port while the game is played: discovery, stock, what was sold to it and the relation.</summary>
    public sealed class PortState {
        private readonly Dictionary<ItemId, int> stock = new Dictionary<ItemId, int>();
        private readonly Dictionary<ItemId, float> sold = new Dictionary<ItemId, float>();

        public bool Discovered { get; private set; }

        /// <summary>The open orders of the port (docs/EXPLORATION_LOOP_PLAN.md, phase 6).</summary>
        public ContractBoard Contracts { get; private set; } = new ContractBoard();

        /// <summary>The id of the port's map once it was generated (0 = not yet).</summary>
        public int MapId { get; set; }

        public double RelationPoints { get; private set; }

        /// <summary>The completed-expeditions count at which the stock was last refilled.</summary>
        public int LastRestock { get; private set; }

        public int StockOf( ItemId item ) => stock.TryGetValue( item, out int count ) ? count : 0;

        /// <summary>How many units of an item the player has sold here and the port has not forgotten yet.</summary>
        public float SoldUnitsOf( ItemId item ) => sold.TryGetValue( item, out float units ) ? units : 0f;

        /// <summary>Discovers the port once the player has completed enough expeditions; returns true the moment it happens.</summary>
        public bool TryDiscover( PortProfile profile, int completedExpeditions ) {
            if( Discovered || completedExpeditions < profile.UnlockAfterExpeditions ) {
                return false;
            }
            Discovered = true;
            LastRestock = completedExpeditions;
            foreach( var offer in profile.Offers ) {
                stock[ offer.Item ] = offer.Stock;
            }
            return true;
        }

        /// <summary>Refills the stock and lets the port forget sold units, once per interval of expeditions that has passed.</summary>
        public void Restock( PortProfile profile, int completedExpeditions ) {
            if( !Discovered ) {
                return;
            }
            int intervals = ( completedExpeditions - LastRestock ) / profile.RestockEveryExpeditions;
            if( intervals < 1 ) {
                return;
            }
            LastRestock += intervals * profile.RestockEveryExpeditions;
            foreach( var offer in profile.Offers ) {
                stock[ offer.Item ] = Math.Max( StockOf( offer.Item ), offer.Stock );
            }
            float recovery = profile.SaturationRecovery * intervals;
            foreach( var item in new List<ItemId>( sold.Keys ) ) {
                float left = sold[ item ] - recovery;
                if( left > 0f ) {
                    sold[ item ] = left;
                } else {
                    sold.Remove( item );
                }
            }
        }

        public void AddStock( ItemId item, int count ) {
            stock[ item ] = Math.Max( 0, StockOf( item ) + count );
        }

        public void AddSold( ItemId item, int count ) {
            sold[ item ] = SoldUnitsOf( item ) + count;
        }

        public void AddRelation( double points ) {
            RelationPoints = Math.Max( 0.0, RelationPoints + points );
        }

        public PortStateData ToData() {
            var data = new PortStateData { Discovered = Discovered, MapId = MapId, Relation = RelationPoints, LastRestock = LastRestock, Contracts = Contracts.ToData() };
            foreach( var pair in stock ) {
                data.Stock.Add( new PortStockEntry { Item = pair.Key.Value, Count = pair.Value } );
            }
            foreach( var pair in sold ) {
                data.Sold.Add( new PortSoldEntry { Item = pair.Key.Value, Units = pair.Value } );
            }
            return data;
        }

        public static PortState FromData( PortStateData data ) {
            var state = new PortState();
            if( data == null ) {
                return state;
            }
            state.Discovered = data.Discovered;
            state.MapId = data.MapId;
            state.RelationPoints = Math.Max( 0.0, data.Relation );
            state.LastRestock = data.LastRestock;
            state.Contracts = ContractBoard.FromData( data.Contracts );
            foreach( var entry in data.Stock ?? new List<PortStockEntry>() ) {
                if( !string.IsNullOrEmpty( entry.Item ) ) {
                    state.stock[ new ItemId( entry.Item ) ] = Math.Max( 0, entry.Count );
                }
            }
            foreach( var entry in data.Sold ?? new List<PortSoldEntry>() ) {
                if( !string.IsNullOrEmpty( entry.Item ) && entry.Units > 0f ) {
                    state.sold[ new ItemId( entry.Item ) ] = entry.Units;
                }
            }
            return state;
        }
    }
}
