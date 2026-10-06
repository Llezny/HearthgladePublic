using System;
using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.Expedition;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Expeditions
{
    /// <summary>One branch of the tree: its data and its logic side.</summary>
    public sealed class ShipTreeEntry
    {
        public ShipNodeSO Asset { get; }
        public ShipNode Node { get; }

        public ShipTreeEntry(ShipNodeSO asset)
        {
            Asset = asset;
            Node = asset.ToNode();
        }
    }

    /// <summary>
    /// The expedition points and the ship tree (docs/EXPLORATION_LOOP_PLAN.md, phase 7): cheaper expeditions, a bigger backpack, a
    /// bonus to gathering. A registered entry point, so that its saved state always exists and the backpack has its extra rows from the start.
    /// </summary>
    public sealed class ShipTreeService : IStartable, ISaveable
    {
        private readonly InventoryService inventory;
        private readonly MessagePopup messagePopup;
        private readonly ShipTreeState state = new ShipTreeState();
        private readonly List<ShipTreeEntry> entries = new List<ShipTreeEntry>();
        private readonly List<ShipNode> nodes = new List<ShipNode>();

        public event Action Changed;

        public IReadOnlyList<ShipTreeEntry> Entries => entries;

        public int Points => state.Points;

        public ShipTreeService(SaveManager saveManager, InventoryService inventory, MessagePopup messagePopup)
        {
            this.inventory = inventory;
            this.messagePopup = messagePopup;
            foreach (var asset in ResourceLoader.LoadAll<ShipNodeSO>(ResourceLoader.SHIP_TREE_PATH).OrderBy(node => node.order).ThenBy(node => node.name))
            {
                var entry = new ShipTreeEntry(asset);
                entries.Add(entry);
                nodes.Add(entry.Node);
            }
            saveManager.RegisterISavable(this);
            if (saveManager.TryGetState<ShipTreeService>(out var saved))
            {
                RestoreState(saved);
            }
        }

        public void Start()
        {
            ApplyBackpack();
        }

        public int LevelOf(ShipTreeEntry entry) => state.LevelOf(entry.Node);

        public bool IsMaxed(ShipTreeEntry entry) => state.IsMaxed(entry.Node);

        public ShipNodeTier NextTier(ShipTreeEntry entry) => state.NextTier(entry.Node);

        /// <summary>What an expedition costs against its price: 1 = full price.</summary>
        public float CostShare => state.CostShare(nodes);

        /// <summary>The chance that gathering gives one more of its yield.</summary>
        public float HarvestChance => state.HarvestChance(nodes);

        public int ExtraBackpackRows => state.ExtraBackpackRows(nodes);

        public void Award(int points, string reason = null)
        {
            if (points <= 0)
            {
                return;
            }
            state.Award(points);
            var text = $"+{points} expedition point{(points == 1 ? "" : "s")}" + (reason != null ? $" ({reason})" : "");
            messagePopup.AddTextMessageToQueue(ref text);
            Changed?.Invoke();
        }

        public bool CanBuy(ShipTreeEntry entry)
        {
            var tier = state.NextTier(entry.Node);
            return tier != null && state.CanAffordPoints(entry.Node) && inventory.Container.HasAll(tier.Ore);
        }

        /// <summary>Buys the next tier: the ore and the points are taken together or not at all.</summary>
        public bool TryBuy(ShipTreeEntry entry)
        {
            var tier = state.NextTier(entry.Node);
            if (tier == null || !CanBuy(entry) || !inventory.Container.TryRemove(tier.Ore))
            {
                return false;
            }
            state.TryBuy(entry.Node);
            ApplyBackpack();
            var text = $"Ship upgraded: {entry.Asset.DisplayName}";
            messagePopup.AddTextMessageToQueue(ref text);
            Changed?.Invoke();
            return true;
        }

        private void ApplyBackpack()
        {
            inventory.EnsureRows(InventoryService.MIN_INVENTORY_ROWS + ExtraBackpackRows);
        }

        /// <summary>For the console: sets the points.</summary>
        public void SetPoints(int points)
        {
            state.Award(points - state.Points);
            Changed?.Invoke();
        }

        public object CaptureState()
        {
            return state.ToData();
        }

        public void RestoreState(object saved)
        {
            var data = saved is JToken token
                ? token.ToObject<ShipTreeData>()
                : JsonConvert.DeserializeObject<ShipTreeData>(saved.ToString());
            state.Load(data);
        }
    }
}
