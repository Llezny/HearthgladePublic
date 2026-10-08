using Hearthglade.Core.Items;
using System;
using System.Collections.Generic;
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
        [ Tooltip( "What the character shows when the item is worn: a head, torso or shoes part of the character kit." ) ]
        public Hearthglade.Gameplay.Characters.CharacterPartSO WornPart;

        [ Header( "Hand stats" ) ]
        [ Tooltip( "The family of the item: picks its animation set. Many pickaxes share one group." ) ]
        public ToolGroup ToolGroup;
        [ Tooltip( "Gathering speed multiplier (bare hands = 1). 0 = no good for chopping trees." ), Min( 0 ) ] public float ChopSpeed;
        [ Tooltip( "Gathering speed multiplier (bare hands = 1). 0 = no good for harvesting plants." ), Min( 0 ) ] public float HarvestSpeed;
        [ Tooltip( "Gathering speed multiplier (bare hands = 1). 0 = no good for mining rock and ore." ), Min( 0 ) ] public float MineSpeed;
        [ Min( 0 ) ] public float AttackPower;

        [ Header( "Protection (worn)" ) ]
        [ Min( 0 ) ] public float ColdProtection;
        [ Min( 0 ) ] public float HeatProtection;
        [ Tooltip( "Fraction of damage taken off (0.25 = a quarter); the total of the outfit is capped at 0.8." ), Min( 0 ) ] public float DamageProtection;
    }
}