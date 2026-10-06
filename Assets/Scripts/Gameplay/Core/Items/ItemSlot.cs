using System;

namespace Hearthglade.Core.Items
{
    // One cell of a container: at most one stack plus the rules deciding what may go in.
    public sealed class ItemSlot
    {
        public ItemType AllowedTypes { get; }
        public int MaxCount { get; }
        public bool IsLocked { get; set; }
        // Take-only slot (e.g. cooking output): a player can drag items out but never in. Code can still fill it with Add.
        public bool OutputOnly { get; set; }
        // Extra restriction on top of AllowedTypes (e.g. a fuel slot only takes burnable items).
        public Func<ItemDefinition, bool> Filter { get; set; }

        public ItemStack Stack { get; private set; }
        public event Action<ItemSlot> Changed;

        public ItemSlot( ItemType allowedTypes = ItemType.Everything, int maxCount = int.MaxValue ) {
            AllowedTypes = allowedTypes;
            MaxCount = maxCount;
        }

        public bool IsEmpty => Stack.IsEmpty;

        // Code-side check; players additionally go through AcceptsFromPlayer.
        public bool Accepts( ItemDefinition item ) {
            return item != null && !IsLocked && ( AllowedTypes & item.Type ) != 0 && ( Filter?.Invoke( item ) ?? true );
        }

        public bool AcceptsFromPlayer( ItemDefinition item ) => !OutputOnly && Accepts( item );

        public int CapacityFor( ItemDefinition item ) => Math.Min( MaxCount, item.MaxStack );

        // How many more units of `item` this slot could take right now.
        public int SpaceFor( ItemDefinition item ) {
            if( !Accepts( item ) ) {
                return 0;
            }
            if( IsEmpty ) {
                return CapacityFor( item );
            }
            return Stack.Id == item.Id ? Math.Max( 0, CapacityFor( item ) - Stack.Count ) : 0;
        }

        // Moves as much of `incoming` into the slot as fits and returns how many units went in.
        public int Add( in ItemStack incoming ) {
            if( incoming.IsEmpty ) {
                return 0;
            }
            var added = Math.Min( SpaceFor( incoming.Definition ), incoming.Count );
            if( added <= 0 ) {
                return 0;
            }
            Stack = IsEmpty ? incoming.WithCount( added ) : Stack.WithAdded( incoming, added );
            Notify();
            return added;
        }

        // Replaces the content without checking rules (restoring a save, swapping).
        public void Set( in ItemStack stack ) {
            SetSilently( stack );
            Notify();
        }

        public void Clear() {
            if( IsEmpty ) {
                return;
            }
            Set( default );
        }

        // Takes up to `amount` units out and returns how many were removed.
        public int Remove( int amount ) {
            var removed = Math.Min( amount, Stack.Count );
            if( removed <= 0 ) {
                return 0;
            }
            Set( Stack.WithCount( Stack.Count - removed ) );
            return removed;
        }

        // Lets a transfer update both slots before either one raises Changed, so listeners never see an item in two places.
        internal void SetSilently( in ItemStack stack ) {
            Stack = stack.IsEmpty ? default : stack;
        }

        internal void Notify() {
            Changed?.Invoke( this );
        }

        public override string ToString() => Stack.ToString();
    }
}
