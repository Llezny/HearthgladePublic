using System;

namespace Hearthglade.Core.Items
{
    // Everything a slot view needs to draw one slot; the view maps ItemId to an icon itself.
    public readonly struct SlotViewState
    {
        public bool IsEmpty { get; }
        public ItemId Item { get; }
        public ItemRarity Rarity { get; }
        public int Count { get; }
        public bool ShowDurability { get; }
        public float MaxDurability { get; }
        public float Durability { get; }

        private SlotViewState( ItemStack stack ) {
            IsEmpty = stack.IsEmpty;
            Item = stack.Id;
            Rarity = IsEmpty ? ItemRarity.Common : stack.Definition.Rarity;
            Count = stack.Count;
            ShowDurability = !IsEmpty && stack.Definition.HasDurability;
            MaxDurability = ShowDurability ? stack.Definition.MaxDurability : 0f;
            Durability = ShowDurability ? stack.Durability : 0f;
        }

        public static SlotViewState From( ItemSlot slot ) => new SlotViewState( slot.Stack );
    }

    // What the player can do with the stack in a slot from its context menu.
    [ Flags ]
    public enum SlotAction
    {
        None = 0,
        Use = 1,
    }

    public static class SlotActions
    {
        public static SlotAction Available( ItemSlot slot ) {
            var stack = slot.Stack;
            return !stack.IsEmpty && stack.Definition.IsUsable ? SlotAction.Use : SlotAction.None;
        }
    }
}
