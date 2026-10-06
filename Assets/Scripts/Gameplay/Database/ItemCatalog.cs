using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Items;

namespace Hearthglade.Gameplay.Database
{
    // Definitions baked from the ItemSO assets (ids are the asset names), plus the way back to the asset for views (icon, prefab, sfx).
    public sealed class ItemCatalog : IItemCatalog
    {
        private readonly Dictionary<ItemId, ItemDefinition> definitions = new();
        private readonly Dictionary<ItemId, ItemSO> assets = new();

        public ItemCatalog( IEnumerable<ItemSO> items ) {
            foreach( var item in items ) {
                if( item == null ) {
                    UnityEngine.Debug.LogWarning( "The item database has an empty entry, skipping it" );
                    continue;
                }
                var id = new ItemId( item.name );
                if( assets.ContainsKey( id ) ) {
                    UnityEngine.Debug.LogError( $"Two items are named '{item.name}', the second one is ignored", item );
                    continue;
                }
                assets.Add( id, item );
                definitions.Add( id, ItemDefinitionBaker.Bake( item ) );
            }
        }

        public IReadOnlyCollection<ItemDefinition> All => definitions.Values;

        public bool TryGet( ItemId id, out ItemDefinition item ) => definitions.TryGetValue( id, out item );

        public ItemDefinition Get( ItemId id ) {
            if( !definitions.TryGetValue( id, out var item ) ) {
                throw new KeyNotFoundException( $"There is no item '{id}'" );
            }
            return item;
        }

        public bool TryGetAsset( ItemId id, out ItemSO asset ) => assets.TryGetValue( id, out asset );

        public ItemSO GetAsset( ItemId id ) => assets.TryGetValue( id, out var asset ) ? asset : null;

        public ItemSO GetAsset( ItemDefinition item ) => item == null ? null : GetAsset( item.Id );
    }
}
