namespace Hearthglade.Core.Items
{
    public enum WearResult
    {
        // Nothing to wear down: no tool, or an item without durability.
        None = 0,
        Worn = 1,
        // The last of the durability went: the tool is gone from the slot.
        Broken = 2,
    }

    // Tools wear out with use and are destroyed when their durability reaches zero.
    public static class ToolWear
    {
        // Durability lost by one finished piece of work.
        public const float PerUse = 1f;

        public static WearResult Apply( ItemSlot slot, float amount = PerUse ) {
            if( slot == null || slot.IsEmpty || !slot.Stack.Definition.HasDurability || amount <= 0f ) {
                return WearResult.None;
            }
            var stack = slot.Stack;
            var left = stack.Durability - amount;
            if( left > 0f ) {
                slot.Set( stack.WithDurability( left ) );
                return WearResult.Worn;
            }
            if( stack.Count > 1 ) {
                // A stack of tools: one breaks, the next is new.
                slot.Set( stack.WithCount( stack.Count - 1 ).WithDurability( stack.Definition.MaxDurability ) );
            }
            else {
                slot.Clear();
            }
            return WearResult.Broken;
        }
    }
}
