using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Core.Trade;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using UnityEngine;

namespace Hearthglade.Gameplay.Trade
{
    /// <summary>
    /// One port as data: what it offers, what it pays extra for, how its relation with the player grows, and the map it lives on.
    /// Lives under Resources/ScriptableObjects/Ports (docs/EXPLORATION_LOOP_PLAN.md, section 5 and 6a).
    /// </summary>
    [CreateAssetMenu(fileName = "Port", menuName = "ScriptableObjects/Trade/Port")]
    public class PortSO : ScriptableObject
    {
        [Serializable]
        public class Offer
        {
            public ItemSO item;
            [Min(0)] public int stock = 5;
            [Tooltip("Local price against the base value: below 1 is cheap.")]
            [Min(0.01f)] public float priceMultiplier = 1f;
            [Tooltip("Signature goods stay locked until the relation reaches this level.")]
            [Min(0)] public int minRelationLevel;
        }

        [Serializable]
        public class Want
        {
            public ItemSO item;
            [Tooltip("What the port pays against the base value; above 1 is a hot good.")]
            [Min(1f)] public float demand = 1.5f;
        }

        [Header("Identity")]
        public string displayName;
        [Tooltip("Name of the MapSO of this port's island.")]
        public string mapName;
        [Tooltip("Discovered when the player has completed this many expeditions.")]
        [Min(0)] public int unlockAfterExpeditions = 2;

        [TextArea] public string description;
        [Tooltip("What setting sail for this port costs: a flat price, the same every time.")]
        public List<Requirement> travelCost = new List<Requirement>();

        [Header("Goods")]
        public List<Offer> offers = new List<Offer>();
        public List<Want> wants = new List<Want>();

        [Header("Relation")]
        [Tooltip("Relation points needed for level 1, 2, ...")]
        public List<float> levelThresholds = new List<float> { 150f, 500f };
        [Tooltip("Margin of the port per relation level; the last one counts for every higher level.")]
        public List<float> margins = new List<float> { 0.25f, 0.15f, 0.10f };

        [Header("Stock and saturation")]
        [Min(1)] public int restockEveryExpeditions = 3;
        [Tooltip("Each unit sold makes the next one worth this share of it.")]
        [Range(0.5f, 1f)] public float saturationPerUnit = 0.95f;
        [Tooltip("Sold units of every item the port forgets per refill.")]
        [Min(0f)] public float saturationRecovery = 5f;

        /// <summary>The stable id of the port (the asset name), used in saves.</summary>
        public string Id => name;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public PortProfile ToProfile()
        {
            var goods = new List<PortGood>();
            foreach (var offer in offers)
            {
                if (offer.item != null)
                {
                    goods.Add(new PortGood(offer.item.Id, offer.stock, offer.priceMultiplier, offer.minRelationLevel));
                }
            }
            var demand = new Dictionary<ItemId, float>();
            foreach (var want in wants)
            {
                if (want.item != null)
                {
                    demand[want.item.Id] = want.demand;
                }
            }
            var thresholds = new List<double>();
            foreach (float threshold in levelThresholds)
            {
                thresholds.Add(threshold);
            }
            return new PortProfile(Id, unlockAfterExpeditions, goods, demand, thresholds, margins,
                restockEveryExpeditions, saturationPerUnit, saturationRecovery);
        }
    }
}
