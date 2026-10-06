using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;

namespace Hearthglade.Core.Trade {

    /// <summary>One side of a barter being put together: items and how many of each, in the order they were added.</summary>
    public sealed class TradeBasket {
        private readonly List<ItemDefinition> items = new List<ItemDefinition>();
        private readonly Dictionary<ItemId, int> counts = new Dictionary<ItemId, int>();

        public IReadOnlyList<ItemDefinition> Items => items;

        public bool IsEmpty => items.Count == 0;

        public int CountOf( ItemId id ) => counts.TryGetValue( id, out int count ) ? count : 0;

        /// <summary>Adds up to <paramref name="count"/> units, never more than <paramref name="limit"/> of the item in total; returns how many went in.</summary>
        public int Add( ItemDefinition item, int count = 1, int limit = int.MaxValue ) {
            if( item == null || count <= 0 ) {
                return 0;
            }
            int added = Math.Min( count, Math.Max( 0, limit - CountOf( item.Id ) ) );
            if( added <= 0 ) {
                return 0;
            }
            if( !counts.ContainsKey( item.Id ) ) {
                items.Add( item );
            }
            counts[ item.Id ] = CountOf( item.Id ) + added;
            return added;
        }

        /// <summary>Takes units out again; the item leaves the basket with its last unit. Returns how many came out.</summary>
        public int Remove( ItemId id, int count = 1 ) {
            int have = CountOf( id );
            int removed = Math.Min( count, have );
            if( removed <= 0 ) {
                return 0;
            }
            if( removed == have ) {
                counts.Remove( id );
                items.RemoveAll( item => item.Id == id );
            } else {
                counts[ id ] = have - removed;
            }
            return removed;
        }

        public void Clear() {
            items.Clear();
            counts.Clear();
        }

        public List<ItemStack> ToStacks() {
            var stacks = new List<ItemStack>( items.Count );
            foreach( var item in items ) {
                stacks.Add( ItemStack.Of( item, counts[ item.Id ] ) );
            }
            return stacks;
        }
    }

    /// <summary>Checks on a copy of the backpack that a swap fits, so the real one is only touched when it will work.</summary>
    public static class InventorySwap {

        /// <summary>True when the player holds everything in <paramref name="give"/> and, once that is gone, <paramref name="take"/> fits.</summary>
        public static bool CanSwap( ItemContainer inventory, IEnumerable<ItemStack> give, IEnumerable<ItemStack> take ) {
            var copy = new ItemContainer( inventory.Size );
            for( int i = 0; i < inventory.Size; i++ ) {
                copy[ i ].Set( inventory[ i ].Stack );
            }
            foreach( var stack in give ) {
                if( !stack.IsEmpty && !copy.TryRemove( stack.Id, stack.Count ) ) {
                    return false;
                }
            }
            foreach( var stack in take ) {
                if( !stack.IsEmpty && !copy.Add( stack ).IsComplete ) {
                    return false;
                }
            }
            return true;
        }
    }
}
