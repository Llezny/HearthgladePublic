using Hearthglade.Core.Items;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    using RarityToAddressable = Dictionary<ItemRarity, string>;
    public class ItemFrameService {
        RarityToAddressable itemFrameAddressables = new() {
            {ItemRarity.Common, "ItemFrame/Common"},
            {ItemRarity.Uncommon, "ItemFrame/Uncommon"},
            {ItemRarity.Rare, "ItemFrame/Rare"},
            {ItemRarity.Epic, "ItemFrame/Epic"},
            {ItemRarity.Legendary, "ItemFrame/Legendary"},
        };

        // One load per rarity, kept for the session: every slot refresh used to start (and never
        // release) its own load, and views now get an already-completed handle they can apply at once.
        private readonly Dictionary<ItemRarity, AsyncOperationHandle> frameHandles = new();

        public AsyncOperationHandle GetFrameAsync( ItemRarity itemRarity ) {
            if( !frameHandles.TryGetValue( itemRarity, out var handle ) ) {
                handle = Addressables.LoadAssetAsync<Sprite>( itemFrameAddressables[ itemRarity ] );
                frameHandles[ itemRarity ] = handle;
            }
            return handle;
        }
    }
}
