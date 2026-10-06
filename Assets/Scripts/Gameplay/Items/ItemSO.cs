using Hearthglade.Core.Items;
using System;
using System.Collections.Generic;
using Hearthglade.Gameplay.Player.Stats;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;

namespace Hearthglade.Gameplay.Items {
    [CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/Items/Base", order = 1)]
    public class ItemSO : ScriptableObject {

        public Sprite icon;
        public ItemRarity ItemRarity;
        public ItemType itemType;
        public string itemName;

        // The stable identity: the asset name (itemName is only what the player reads).
        public Hearthglade.Core.Items.ItemId Id => new Hearthglade.Core.Items.ItemId( name );

        [TextArea]
        public string itemDescription;
        public int maxItemsInStack;

        [Header( "Cooking" )]
        [Tooltip( "How much burn time this item adds to a cooking station's fuel when used as fuel. 0 = not usable as fuel." )]
        public float FuelValue = 0f;

        [ Header( "Trade" ) ]
        [ Tooltip( "Hidden value that barter is priced from (nobody sees coins). 0 = cannot be traded." ) ]
        [ Min( 0 ) ] public int BaseValue = 0;

        [ Header( "Behaviour" ) ]
        [ Tooltip( "Markers that logic checks instead of matching the item by name." ) ]
        public Hearthglade.Core.Items.ItemTag Tags;

        [ Header( "Equipment" ) ]
        public GameObject GameObject;
        public float MaxDurability = 0;
        public bool HasDurability;
        public int Damage;
        public List<AttributeModifier> Modifiers;
    }
}