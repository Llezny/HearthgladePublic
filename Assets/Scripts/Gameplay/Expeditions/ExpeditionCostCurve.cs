using System.Collections.Generic;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using UnityEngine;

namespace Hearthglade.Gameplay.Expeditions
{
    /// <summary>
    /// What setting sail on an expedition costs by depth: a food budget plus a few items, each a base cost plus the extra per depth beyond the first.
    /// The numbers are a first guess (docs/EXPLORATION_LOOP_PLAN.md, phase 2) to be tuned by playing.
    /// </summary>
    [CreateAssetMenu(fileName = "ExpeditionCostCurve", menuName = "ScriptableObjects/Expedition/Cost curve")]
    public class ExpeditionCostCurve : ScriptableObject
    {
        [ Tooltip( "Hunger points of food the first depth costs; the food is taken from the backpack (see FoodBudget)." ) ]
        public float baseFood = 20f;
        public float foodPerExtraDepth = 8f;
        [ Tooltip( "The items part of the cost (a little wood), on top of the food." ) ]
        public List<Requirement> baseCost = new List<Requirement>();
        public List<Requirement> perExtraDepth = new List<Requirement>();

        public float FoodFor(int depth)
        {
            return baseFood + Mathf.Max(0, depth - 1) * foodPerExtraDepth;
        }

        public List<Requirement> CostFor(int depth)
        {
            int extra = Mathf.Max(0, depth - 1);
            var totals = new Dictionary<string, int>();
            var order = new List<string>();
            void Add(List<Requirement> source, int times)
            {
                foreach (var requirement in source)
                {
                    if (!totals.ContainsKey(requirement.requiredItemName))
                    {
                        totals[requirement.requiredItemName] = 0;
                        order.Add(requirement.requiredItemName);
                    }
                    totals[requirement.requiredItemName] += requirement.requiredQuantity * times;
                }
            }
            Add(baseCost, 1);
            Add(perExtraDepth, extra);

            var cost = new List<Requirement>();
            foreach (var item in order)
            {
                if (totals[item] > 0)
                {
                    cost.Add(new Requirement(item, totals[item]));
                }
            }
            return cost;
        }
    }
}
