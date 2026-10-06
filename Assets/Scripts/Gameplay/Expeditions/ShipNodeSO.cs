using System;
using System.Collections.Generic;
using Hearthglade.Core.Expedition;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using UnityEngine;

namespace Hearthglade.Gameplay.Expeditions
{
    /// <summary>
    /// One branch of the ship tree as data: what it does, and for each tier the expedition points, the ore and the share it adds.
    /// Lives under Resources/ScriptableObjects/ShipTree (docs/EXPLORATION_LOOP_PLAN.md, phase 7).
    /// </summary>
    [CreateAssetMenu(fileName = "ShipNode", menuName = "ScriptableObjects/Expedition/Ship node")]
    public class ShipNodeSO : ScriptableObject
    {
        [Serializable]
        public class Tier
        {
            [Min(0)] public int points = 3;
            [Tooltip("Ore (or anything else) the tier costs on top of the points.")]
            public List<Requirement> ore = new List<Requirement>();
            [Tooltip("What the tier adds: a share (0.15 = 15%) for the discount and the harvest bonus, rows for the backpack.")]
            public float magnitude = 0.15f;
        }

        public string displayName;
        [TextArea] public string description;
        public ShipEffect effect;
        [Tooltip("Where the branch stands in the list.")]
        public int order;
        public List<Tier> tiers = new List<Tier>();

        public string Id => name;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public ShipNode ToNode()
        {
            var list = new List<ShipNodeTier>();
            foreach (var tier in tiers)
            {
                var ore = new List<ItemAmount>();
                foreach (var requirement in tier.ore)
                {
                    ore.Add(new ItemAmount(requirement.Item, requirement.requiredQuantity));
                }
                list.Add(new ShipNodeTier(tier.points, ore, tier.magnitude));
            }
            return new ShipNode(Id, effect, list);
        }
    }
}
