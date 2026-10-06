using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Items.UsableItems;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hearthglade.Tests
{
    /// <summary>
    /// ItemCatalog bakes the ItemSO assets into the engine-free ItemDefinitions the game logic works with, and resolves ids
    /// (asset names) and legacy display names. Run with: Unity -runTests -testPlatform EditMode (the Editor must be closed).
    /// </summary>
    public class ItemCatalogTests
    {
        private const string DatabasePath = "Assets/ScriptableObjects/Configs/DatabaseSO.asset";

        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                Object.DestroyImmediate(item);
            }
            created.Clear();
        }

        private T Make<T>(string assetName, string displayName = null) where T : ItemSO
        {
            var item = ScriptableObject.CreateInstance<T>();
            item.name = assetName;
            item.itemName = displayName ?? assetName;
            item.maxItemsInStack = 10;
            created.Add(item);
            return item;
        }

        [Test]
        public void Bake_PlainItem_CopiesTheBaseFields()
        {
            var item = Make<ItemSO>("Torch");
            item.itemType = ItemType.Tool;
            item.ItemRarity = ItemRarity.Rare;
            item.maxItemsInStack = 3;
            item.FuelValue = 4f;
            item.HasDurability = true;
            item.MaxDurability = 80f;

            var definition = ItemDefinitionBaker.Bake(item);

            Assert.AreEqual(new ItemId("Torch"), definition.Id);
            Assert.AreEqual(ItemType.Tool, definition.Type);
            Assert.AreEqual(ItemRarity.Rare, definition.Rarity);
            Assert.AreEqual(3, definition.MaxStack);
            Assert.IsTrue(definition.IsFuel);
            Assert.IsTrue(definition.HasDurability);
            Assert.AreEqual(80f, definition.MaxDurability);
            Assert.IsFalse(definition.IsUsable);
        }

        [Test]
        public void Bake_FoodItem_CopiesFoodTypeAndQuality()
        {
            var item = Make<FoodItemSO>("Chicken");
            item.foodType = FoodType.Meat;
            item.NutritionQuality = 1.5f;

            var definition = ItemDefinitionBaker.Bake(item);

            Assert.AreEqual(FoodType.Meat, definition.FoodType);
            Assert.AreEqual(1.5f, definition.NutritionQuality);
            Assert.IsFalse(definition.IsUsable, "food that is not a UsableItem cannot be eaten directly");
        }

        [Test]
        public void Bake_UsableItem_CopiesTheHealingValues()
        {
            var item = Make<UsableItem>("Carrot");
            var serialized = new SerializedObject(item);
            serialized.FindProperty("hungerHealing").floatValue = 12f;
            serialized.FindProperty("thirstHealing").floatValue = 3f;
            serialized.FindProperty("healthHealing").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var definition = ItemDefinitionBaker.Bake(item);

            Assert.IsTrue(definition.IsUsable);
            Assert.AreEqual(12f, definition.Nutrition.Hunger);
            Assert.AreEqual(3f, definition.Nutrition.Thirst);
            Assert.AreEqual(1f, definition.Nutrition.Health);
        }

        [Test]
        public void Bake_ZeroMaxStack_BecomesOneItemPerSlot()
        {
            var item = Make<ItemSO>("FlatFish");
            item.maxItemsInStack = 0;
            Assert.AreEqual(1, ItemDefinitionBaker.Bake(item).MaxStack);
        }

        [Test]
        public void Catalog_ResolvesById_ButNotByDisplayName()
        {
            var mushroom = Make<ItemSO>("BrownMushroom", "Brown Mushroom");
            var catalog = new ItemCatalog(new[] { mushroom });

            Assert.IsTrue(catalog.TryGet(new ItemId("BrownMushroom"), out var byId));
            Assert.IsFalse(catalog.TryGet(new ItemId("Brown Mushroom"), out _), "ids only: the display name is just text");
            Assert.AreSame(mushroom, catalog.GetAsset(byId));
            Assert.AreSame(mushroom, catalog.GetAsset(new ItemId("BrownMushroom")));
        }

        [Test]
        public void Catalog_UnknownIds_DoNotResolve()
        {
            var catalog = new ItemCatalog(new[] { Make<ItemSO>("Wood") });

            Assert.IsFalse(catalog.TryGet(new ItemId("Nope"), out var missing));
            Assert.IsNull(missing);
            Assert.IsFalse(catalog.TryGet(default, out _));
            Assert.IsNull(catalog.GetAsset(new ItemId("Nope")));
            Assert.Throws<KeyNotFoundException>(() => catalog.Get(new ItemId("Nope")));
        }

        [Test]
        public void Catalog_SkipsNullEntriesAndDuplicateNames()
        {
            var first = Make<ItemSO>("Wood");
            var duplicate = Make<ItemSO>("Wood");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("empty entry"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Two items are named 'Wood'"));

            var catalog = new ItemCatalog(new[] { first, null, duplicate });

            Assert.AreEqual(1, catalog.All.Count);
            Assert.AreSame(first, catalog.GetAsset(new ItemId("Wood")));
        }

        [Test]
        public void Database_EveryItemBakes_WithUniqueIds()
        {
            var database = AssetDatabase.LoadAssetAtPath<DatabaseSO>(DatabasePath);
            Assert.NotNull(database, "DatabaseSO asset not found");
            var items = database.Items.Where(item => item != null).ToList();
            Assert.Greater(items.Count, 10, "the item database is not empty");

            var catalog = new ItemCatalog(items);

            Assert.AreEqual(items.Count, catalog.All.Count, "every item has a unique asset name");
            foreach (var item in items)
            {
                Assert.IsTrue(catalog.TryGet(new ItemId(item.name), out var definition), $"{item.name} resolves by id");
                Assert.GreaterOrEqual(definition.MaxStack, 1, $"{item.name} stacks");
                Assert.AreSame(item, catalog.GetAsset(definition));
            }
        }
    }
}
