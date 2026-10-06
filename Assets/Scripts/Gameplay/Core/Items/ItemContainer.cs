using System;
using System.Collections;
using System.Collections.Generic;

namespace Hearthglade.Core.Items
{
    public readonly struct ItemAmount
    {
        public ItemId Id { get; }
        public int Count { get; }

        public ItemAmount( ItemId id, int count ) {
            Id = id;
            Count = count;
        }
    }

    public readonly struct AddResult
    {
        public int Added { get; }
        public int Remainder { get; }

        public AddResult( int added, int remainder ) {
            Added = added;
            Remainder = remainder;
        }

        public bool IsComplete => Remainder == 0;
    }

    // An ordered list of slots (backpack, chest, cooking station). Operations report what happened instead of logging.
    public sealed class ItemContainer : IEnumerable<ItemSlot>
    {
        private ItemSlot[] slots;

        // Raised after a slot's content changed, with that slot's index.
        public event Action<int, ItemSlot> SlotChanged;

        public ItemContainer( int size ) : this( CreateSlots( size ) ) { }

        public ItemContainer( IEnumerable<ItemSlot> slots ) {
            this.slots = new List<ItemSlot>( slots ).ToArray();
            for( int i = 0; i < this.slots.Length; i++ ) {
                var index = i;
                this.slots[ i ].Changed += slot => SlotChanged?.Invoke( index, slot );
            }
        }

        // Raised after the container got more slots (the new ones are empty and come last).
        public event Action Resized;

        // Adds empty slots at the end until the container has `newSize`; it never shrinks, so nothing is lost.
        public void Grow( int newSize ) {
            if( newSize <= slots.Length ) {
                return;
            }
            var grown = new ItemSlot[ newSize ];
            Array.Copy( slots, grown, slots.Length );
            for( int i = slots.Length; i < newSize; i++ ) {
                var index = i;
                grown[ i ] = new ItemSlot();
                grown[ i ].Changed += slot => SlotChanged?.Invoke( index, slot );
            }
            slots = grown;
            Resized?.Invoke();
        }

        private static IEnumerable<ItemSlot> CreateSlots( int size ) {
            for( int i = 0; i < size; i++ ) {
                yield return new ItemSlot();
            }
        }

        public int Size => slots.Length;

        public ItemSlot this[ int index ] => slots[ index ];

        public int IndexOf( ItemSlot slot ) => Array.IndexOf( slots, slot );

        public AddResult Add( ItemDefinition item, int count = 1 ) {
            return item == null ? new AddResult( 0, Math.Max( count, 0 ) ) : Add( ItemStack.Of( item, count ) );
        }

        // Tops up existing stacks of the item first, then uses empty slots. Whatever does not fit comes back as the remainder.
        public AddResult Add( in ItemStack stack ) {
            if( stack.IsEmpty ) {
                return new AddResult( 0, 0 );
            }
            var left = stack.Count;
            foreach( var slot in slots ) {
                if( left <= 0 ) {
                    break;
                }
                if( !slot.IsEmpty && slot.Stack.Id == stack.Id ) {
                    left -= slot.Add( stack.WithCount( left ) );
                }
            }
            foreach( var slot in slots ) {
                if( left <= 0 ) {
                    break;
                }
                if( slot.IsEmpty ) {
                    left -= slot.Add( stack.WithCount( left ) );
                }
            }
            return new AddResult( stack.Count - left, left );
        }

        // Free room for the item across all slots that would accept it.
        public int SpaceFor( ItemDefinition item ) {
            var space = 0;
            foreach( var slot in slots ) {
                space += slot.SpaceFor( item );
            }
            return space;
        }

        public bool CanFit( ItemDefinition item, int count = 1 ) => SpaceFor( item ) >= count;

        public int Count( ItemId id ) {
            var total = 0;
            foreach( var slot in slots ) {
                if( !slot.IsEmpty && slot.Stack.Id == id ) {
                    total += slot.Stack.Count;
                }
            }
            return total;
        }

        public int CountWhere( Func<ItemDefinition, bool> predicate ) {
            var total = 0;
            foreach( var slot in slots ) {
                if( !slot.IsEmpty && predicate( slot.Stack.Definition ) ) {
                    total += slot.Stack.Count;
                }
            }
            return total;
        }

        public int CountFood( FoodType foodType ) => CountWhere( item => ( item.FoodType & foodType ) != 0 );

        public bool Contains( ItemId id ) => Count( id ) > 0;

        public bool HasAll( IEnumerable<ItemAmount> amounts ) {
            foreach( var needed in Aggregate( amounts ) ) {
                if( Count( needed.Key ) < needed.Value ) {
                    return false;
                }
            }
            return true;
        }

        // All-or-nothing: the container is untouched when it does not hold enough.
        public bool TryRemove( ItemId id, int count ) {
            if( count <= 0 ) {
                return true;
            }
            if( Count( id ) < count ) {
                return false;
            }
            var left = count;
            foreach( var slot in slots ) {
                if( left <= 0 ) {
                    break;
                }
                if( !slot.IsEmpty && slot.Stack.Id == id ) {
                    left -= slot.Remove( left );
                }
            }
            return true;
        }

        // All-or-nothing across every amount (a cost the player either pays in full or not at all).
        public bool TryRemove( IEnumerable<ItemAmount> amounts ) {
            var needed = Aggregate( amounts );
            foreach( var entry in needed ) {
                if( Count( entry.Key ) < entry.Value ) {
                    return false;
                }
            }
            foreach( var entry in needed ) {
                TryRemove( entry.Key, entry.Value );
            }
            return true;
        }

        public MoveResult Move( int fromIndex, int toIndex ) => ItemTransfer.Move( slots[ fromIndex ], slots[ toIndex ] );

        private static Dictionary<ItemId, int> Aggregate( IEnumerable<ItemAmount> amounts ) {
            var result = new Dictionary<ItemId, int>();
            foreach( var amount in amounts ) {
                if( amount.Count <= 0 ) {
                    continue;
                }
                result.TryGetValue( amount.Id, out var existing );
                result[ amount.Id ] = existing + amount.Count;
            }
            return result;
        }

        public IEnumerator<ItemSlot> GetEnumerator() => ( (IEnumerable<ItemSlot>)slots ).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() {
            var text = new System.Text.StringBuilder();
            for( int i = 0; i < slots.Length; i++ ) {
                text.Append( i ).Append( ". " ).Append( slots[ i ] ).Append( '\n' );
            }
            return text.ToString();
        }
    }
}
