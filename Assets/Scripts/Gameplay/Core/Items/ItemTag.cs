using System;

namespace Hearthglade.Core.Items
{
    // Behavioural markers that logic can test instead of matching item names.
    [ Flags ]
    public enum ItemTag
    {
        None = 0,
        CookingPot = 1,
    }
}
