using System.Collections.Generic;

namespace Hearthglade.Core.Items
{
    // The tool picked for one gathering interaction: where it lies (so it can wear out later) and how fast it is.
    public readonly struct ToolChoice
    {
        public ItemSlot Slot { get; }
        public ItemDefinition Definition { get; }
        public float Speed { get; }

        public ToolChoice( ItemSlot slot, ItemDefinition definition, float speed ) {
            Slot = slot;
            Definition = definition;
            Speed = speed;
        }

        public bool Found => Definition != null;

        public static ToolChoice None => default;
    }

    // Chooses the tool for a skill without touching anything: the item with the highest speed among the hand and the backpack wins.
    // A tie goes to the hand item, then to the earlier backpack slot. Pure, so asking twice gives the same answer.
    public static class ToolSelector
    {
        public static ToolChoice Pick( GatherSkill skill, ItemSlot hand, IEnumerable<ItemSlot> backpack ) {
            var best = ToolChoice.None;
            Consider( skill, hand, ref best );
            if( backpack != null ) {
                foreach( var slot in backpack ) {
                    Consider( skill, slot, ref best );
                }
            }
            return best;
        }

        // The weapon to hunt with: the Weapon with the highest AttackPower among the hand and the backpack, ties going the same way as tools.
        // Tools hit things too (an axe has AttackPower), but only a weapon is for hunting. The choice's Speed is the weapon's AttackPower.
        public static ToolChoice PickWeapon( ItemSlot hand, IEnumerable<ItemSlot> backpack ) {
            var best = ToolChoice.None;
            ConsiderWeapon( hand, ref best );
            if( backpack != null ) {
                foreach( var slot in backpack ) {
                    ConsiderWeapon( slot, ref best );
                }
            }
            return best;
        }

        private static void ConsiderWeapon( ItemSlot slot, ref ToolChoice best ) {
            if( slot == null || slot.IsEmpty ) {
                return;
            }
            var definition = slot.Stack.Definition;
            var power = definition.Stats.AttackPower;
            if( ( definition.Type & ItemType.Weapon ) != 0 && power > 0f && ( !best.Found || power > best.Speed ) ) {
                best = new ToolChoice( slot, definition, power );
            }
        }

        private static void Consider( GatherSkill skill, ItemSlot slot, ref ToolChoice best ) {
            if( slot == null || slot.IsEmpty ) {
                return;
            }
            var definition = slot.Stack.Definition;
            var speed = definition.Stats.SpeedFor( skill );
            if( speed > 0f && ( !best.Found || speed > best.Speed ) ) {
                best = new ToolChoice( slot, definition, speed );
            }
        }
    }
}
