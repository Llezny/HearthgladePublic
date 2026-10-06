using System.Collections.Generic;
using Hearthglade.Core.Items;

namespace Hearthglade.Core.Farming {

    public sealed class CropRegistry : ICropCatalog {
        private readonly Dictionary<string, CropDefinition> byId = new();

        public CropRegistry( IEnumerable<CropDefinition> definitions ) {
            foreach( var definition in definitions ) {
                byId[ definition.Id.Value ] = definition;
            }
        }

        public bool TryGet( ItemId id, out CropDefinition definition ) {
            if( id.IsEmpty ) {
                definition = null;
                return false;
            }
            return byId.TryGetValue( id.Value, out definition );
        }
    }
}
