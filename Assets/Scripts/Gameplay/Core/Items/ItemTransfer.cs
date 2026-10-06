using System;

namespace Hearthglade.Core.Items
{
    public enum MoveResult
    {
        Rejected, // nothing changed
        Moved,    // into an empty slot (all of it, or as much as fits)
        Stacked,  // merged into a stack of the same item
        Swapped,
    }

    // Drag-and-drop style moves between any two slots, whichever containers they belong to.
    public static class ItemTransfer
    {
        public static MoveResult Move( ItemSlot from, ItemSlot to ) {
            if( from == null || to == null || ReferenceEquals( from, to ) || from.IsEmpty ) {
                return MoveResult.Rejected;
            }
            var fromStack = from.Stack;
            var fromItem = fromStack.Definition;

            if( to.IsEmpty ) {
                if( !to.AcceptsFromPlayer( fromItem ) ) {
                    return MoveResult.Rejected;
                }
                var moved = Math.Min( fromStack.Count, to.CapacityFor( fromItem ) );
                if( moved <= 0 ) {
                    return MoveResult.Rejected;
                }
                Apply( from, fromStack.WithCount( fromStack.Count - moved ), to, fromStack.WithCount( moved ) );
                return MoveResult.Moved;
            }

            var toStack = to.Stack;
            if( toStack.Id == fromStack.Id ) {
                if( !to.AcceptsFromPlayer( fromItem ) ) {
                    return MoveResult.Rejected;
                }
                var moved = Math.Min( to.SpaceFor( fromItem ), fromStack.Count );
                if( moved <= 0 ) {
                    return MoveResult.Rejected;
                }
                Apply( from, fromStack.WithCount( fromStack.Count - moved ), to, toStack.WithAdded( fromStack, moved ) );
                return MoveResult.Stacked;
            }

            var toItem = toStack.Definition;
            if( !from.AcceptsFromPlayer( toItem ) || !to.AcceptsFromPlayer( fromItem ) ) {
                return MoveResult.Rejected;
            }
            if( fromStack.Count > to.CapacityFor( fromItem ) || toStack.Count > from.CapacityFor( toItem ) ) {
                return MoveResult.Rejected;
            }
            Apply( from, toStack, to, fromStack );
            return MoveResult.Swapped;
        }

        private static void Apply( ItemSlot from, in ItemStack newFrom, ItemSlot to, in ItemStack newTo ) {
            from.SetSilently( newFrom );
            to.SetSilently( newTo );
            // Destination first: a listener there may react by changing that slot again (e.g. fuel being ignited).
            to.Notify();
            from.Notify();
        }
    }
}
