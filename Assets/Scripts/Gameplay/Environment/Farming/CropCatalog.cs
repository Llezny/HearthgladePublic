using System.Collections.Generic;
using Hearthglade.Core.Farming;
using Hearthglade.Core.Items;

namespace Hearthglade.Gameplay.Environment.Farming
{
    // Definitions baked from the CropSO assets (ids are the asset names), plus the way back to the asset for views.
    public sealed class CropCatalog : ICropCatalog
    {
        private readonly Dictionary<ItemId, CropSO> assets = new();
        private readonly List<CropSO> all = new();
        private readonly CropRegistry registry;

        public CropCatalog( IEnumerable<CropSO> crops ) {
            var definitions = new List<CropDefinition>();
            foreach( var crop in crops ) {
                if( crop == null ) {
                    UnityEngine.Debug.LogWarning( "The database has an empty crop entry, skipping it" );
                    continue;
                }
                if( assets.ContainsKey( crop.Id ) ) {
                    UnityEngine.Debug.LogError( $"Two crops are named '{crop.name}', the second one is ignored", crop );
                    continue;
                }
                assets.Add( crop.Id, crop );
                all.Add( crop );
                definitions.Add( crop.ToDefinition() );
            }
            registry = new CropRegistry( definitions );
        }

        public IReadOnlyList<CropSO> All => all;

        public bool TryGet( ItemId id, out CropDefinition definition ) => registry.TryGet( id, out definition );

        public bool TryGetAsset( ItemId id, out CropSO asset ) => assets.TryGetValue( id, out asset );
    }
}
