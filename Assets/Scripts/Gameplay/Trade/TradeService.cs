using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.Items;
using Hearthglade.Core.Trade;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using VContainer;

namespace Hearthglade.Gameplay.Trade
{
    /// <summary>Carries out a barter between the player's backpack and a port: checks it, takes the goods, hands over the others.</summary>
    public sealed class TradeService
    {
        private readonly InventoryService inventory;
        private readonly ItemCatalog catalog;
        private readonly PortService portService;

        [Inject]
        public TradeService(InventoryService inventory, ItemCatalog catalog, PortService portService)
        {
            this.inventory = inventory;
            this.catalog = catalog;
            this.portService = portService;
        }

        /// <summary>What the player carries that can be traded, each item once with its total count.</summary>
        public List<(ItemDefinition item, int owned)> OwnedTradables()
        {
            var result = new List<(ItemDefinition, int)>();
            var seen = new HashSet<ItemId>();
            foreach (var slot in inventory.Container)
            {
                if (slot.IsEmpty || !slot.Stack.Definition.IsTradable || !seen.Add(slot.Stack.Id))
                {
                    continue;
                }
                result.Add((slot.Stack.Definition, inventory.Count(slot.Stack.Id)));
            }
            return result;
        }

        public int Owned(ItemId id) => inventory.Count(id);

        public bool TryGetDefinition(ItemId id, out ItemDefinition item) => catalog.TryGet(id, out item);

        public ItemSO AssetOf(ItemDefinition item) => catalog.GetAsset(item);

        /// <summary>The price check plus whether the goods that come in have room in the backpack.</summary>
        public TradeQuote Quote(PortEntry port, TradeBasket give, TradeBasket take, out bool fits)
        {
            var giveStacks = give.ToStacks();
            var takeStacks = take.ToStacks();
            fits = InventorySwap.CanSwap(inventory.Container, giveStacks, takeStacks);
            return BarterCalculator.Quote(port.Profile, port.State, giveStacks, takeStacks);
        }

        /// <summary>The open orders of a port, refreshed first (they lapse with expeditions, places grow with the relation).</summary>
        public IReadOnlyList<Contract> ContractsOf(PortEntry port)
        {
            portService.RefreshContracts(port);
            return port.State.Contracts.Active;
        }

        public bool CanFulfil(Contract contract)
        {
            return inventory.Count(contract.Item) >= contract.Count;
        }

        /// <summary>Hands in what the order asks for and takes the reward; false (nothing changes) when the goods are missing or the reward would not fit.</summary>
        public bool TryFulfil(PortEntry port, Contract contract)
        {
            if (!catalog.TryGet(contract.Item, out var asked) || !catalog.TryGet(contract.RewardItem, out var reward))
            {
                return false;
            }
            var give = new[] { ItemStack.Of(asked, contract.Count) };
            var take = new[] { ItemStack.Of(reward, contract.RewardCount) };
            if (!port.State.Contracts.Active.Contains(contract) || !InventorySwap.CanSwap(inventory.Container, give, take))
            {
                return false;
            }
            inventory.RemoveItem(contract.Item, contract.Count);
            inventory.AddItem(catalog.GetAsset(reward), contract.RewardCount);
            port.State.AddRelation(contract.RelationPoints);
            port.State.Contracts.Remove(contract);
            portService.RefreshContracts(port);
            return true;
        }

        /// <summary>Does the swap when it is acceptable and fits; the baskets are the caller's to clear afterwards.</summary>
        public bool TryTrade(PortEntry port, TradeBasket give, TradeBasket take)
        {
            var quote = Quote(port, give, take, out bool fits);
            if (!quote.IsAcceptable || !fits)
            {
                return false;
            }
            var giveStacks = give.ToStacks();
            var takeStacks = take.ToStacks();

            foreach (var stack in giveStacks)
            {
                inventory.RemoveItem(stack.Id, stack.Count);
            }
            foreach (var stack in takeStacks)
            {
                inventory.AddItem(catalog.GetAsset(stack.Definition), stack.Count);
            }
            BarterCalculator.Execute(port.Profile, port.State, giveStacks, takeStacks);
            return true;
        }
    }
}
