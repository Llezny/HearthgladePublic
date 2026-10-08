using System;
using System.Collections.Generic;

namespace Hearthglade.Core.Items
{
    // What the player wears: one slot per equipment kind with its rules. Equipped/Unequipped come from the slots
    // themselves, so dragging, loading a save or any code that fills a slot behaves the same.
    public sealed class EquipmentModel
    {
        private readonly Dictionary<SlotType, ItemSlot> slots = new();
        private readonly Dictionary<SlotType, ItemDefinition> equipped = new();

        public event Action<SlotType, ItemDefinition> Equipped;
        public event Action<SlotType, ItemDefinition> Unequipped;

        public EquipmentModel() {
            Add( SlotType.Chest, new ItemSlot( ItemType.Chestplate ) );
            Add( SlotType.Head, new ItemSlot( ItemType.Helmet ) );
            Add( SlotType.Feet, new ItemSlot( ItemType.Footwear ) );
            Add( SlotType.Hand, new ItemSlot( ItemType.Tool | ItemType.Weapon ) );
        }

        public IEnumerable<KeyValuePair<SlotType, ItemSlot>> Slots => slots;

        public ItemSlot this[ SlotType type ] => slots[ type ];

        // What the worn clothing shields from, summed over the head, chest and feet. The hand holds tools, which protect from nothing.
        public Protection Protection {
            get {
                var total = default( Protection );
                foreach( var pair in equipped ) {
                    if( pair.Key != SlotType.Hand ) {
                        total += pair.Value.Protection;
                    }
                }
                return total;
            }
        }

        public bool TryGetSlotType( ItemSlot slot, out SlotType type ) {
            foreach( var pair in slots ) {
                if( ReferenceEquals( pair.Value, slot ) ) {
                    type = pair.Key;
                    return true;
                }
            }
            type = SlotType.Other;
            return false;
        }

        private void Add( SlotType type, ItemSlot slot ) {
            slots.Add( type, slot );
            slot.Changed += changed => OnSlotChanged( type, changed );
        }

        private void OnSlotChanged( SlotType type, ItemSlot slot ) {
            var now = slot.IsEmpty ? null : slot.Stack.Definition;
            equipped.TryGetValue( type, out var before );
            if( before?.Id == now?.Id ) {
                return;
            }
            if( now == null ) {
                equipped.Remove( type );
            }
            else {
                equipped[ type ] = now;
            }
            if( before != null ) {
                Unequipped?.Invoke( type, before );
            }
            if( now != null ) {
                Equipped?.Invoke( type, now );
            }
        }
    }
}
