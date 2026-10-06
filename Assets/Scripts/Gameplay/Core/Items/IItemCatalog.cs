using System.Collections.Generic;

namespace Hearthglade.Core.Items
{
    // The one place item ids are turned into definitions.
    public interface IItemCatalog
    {
        IReadOnlyCollection<ItemDefinition> All { get; }

        bool TryGet( ItemId id, out ItemDefinition item );

        // Throws KeyNotFoundException for an id the catalog does not know.
        ItemDefinition Get( ItemId id );

    }
}
