using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Items.UsableItems;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hearthglade.Tests
{
    /// <summary>
    /// The saved shape of a stack: the id-based format, and the older ItemName/ItemsCount shape that saves and POI loot
    /// still contain. Run with: Unity -runTests -testPlatform EditMode (the Editor must be closed).
    /// </summary>
    public class ItemStackDataTests
    {
        private readonly List<Object> created = new();
        private ItemCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            var wood = Make("Wood", "Wood", 10);
            var mushroom = Make("BrownMushroom", "Brown Mushroom", 5);
            var axe = Make("Axe", "Axe", 1);
            axe.HasDurability = true;
            axe.MaxDurability = 100f;
            catalog = new ItemCatalog(new[] { wood, mushroom, axe });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                Object.DestroyImmediate(item);
            }
            created.Clear();
        }

        private ItemSO Make(string assetName, string displayName, int maxStack)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.name = assetName;
            item.itemName = displayName;
            item.maxItemsInStack = maxStack;
            item.itemType = ItemType.Resource;
            created.Add(item);
            return item;
        }

        [Test]
        public void Written_UsesTheIdShapeOnly()
        {
            var json = JsonConvert.SerializeObject(ItemStackData.From(ItemStack.Of(catalog.Get(new ItemId("Wood")), 4)));

            StringAssert.Contains("\"Id\":\"Wood\"", json);
            StringAssert.Contains("\"Count\":4", json);
            foreach (var legacy in new[] { "Durability", "Nutrition" })
            {
                StringAssert.DoesNotContain(legacy, json, $"a plain stack does not write {legacy}");
            }
        }

        [Test]
        public void EmptyStack_HasNoData()
        {
            Assert.IsNull(ItemStackData.From(default));
        }

        [Test]
        public void RoundTrip_KeepsCountDurabilityAndNutrition()
        {
            var original = ItemStack.Restore(catalog.Get(new ItemId("Axe")), 1, 12.5f, new NutritionOverride(3, 2, 1));

            var json = JsonConvert.SerializeObject(ItemStackData.From(original));
            var read = JsonConvert.DeserializeObject<ItemStackData>(json);

            Assert.IsTrue(read.TryToStack(catalog, out var restored));
            Assert.AreEqual(original.Id, restored.Id);
            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual(12.5f, restored.Durability);
            Assert.AreEqual(3f, restored.Nutrition.Hunger);
            Assert.AreEqual(2f, restored.Nutrition.Thirst);
            Assert.AreEqual(1f, restored.Nutrition.Health);
        }

        [Test]
        public void UnknownItem_IsReportedAndSkipped()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("'Gone' does not exist any more"));
            Assert.IsFalse(new ItemStackData { Id = "Gone", Count = 1 }.TryToStack(catalog, out _));
        }
    }
}
