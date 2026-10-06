using Hearthglade.Core.Items;

namespace Hearthglade.Core.Farming {

    public interface ICropCatalog {
        bool TryGet( ItemId id, out CropDefinition definition );
    }
}
