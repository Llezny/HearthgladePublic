using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using NUnit.Framework;
using UnityEditor;

namespace Hearthglade.Tests
{
    /// <summary>
    /// Items are referenced by id (the asset name) from recipes, ship costs, plants and tags. A typo or a renamed asset would
    /// only show up in the game as a missing item, so every reference is checked against the catalog here.
    /// Run with: Unity -runTests -testPlatform EditMode (the Editor must be closed).
    /// </summary>
    public class ItemReferenceTests
    {
        private const string DatabasePath = "Assets/ScriptableObjects/Configs/DatabaseSO.asset";
        private const string JsonFolder = "Assets/Resources/JSON";

        private static ItemCatalog Catalog()
        {
            var database = AssetDatabase.LoadAssetAtPath<DatabaseSO>(DatabasePath);
            Assert.NotNull(database, "DatabaseSO asset not found");
            return new ItemCatalog(database.Items.Where(item => item != null));
        }

        private static List<string> UnknownIds(ItemCatalog catalog, IEnumerable<string> ids)
        {
            return ids.Where(id => !catalog.TryGet(new ItemId(id), out _)).Distinct().OrderBy(id => id).ToList();
        }

        [Test]
        public void RecipeJson_OnlyNamesItemsOfTheCatalog()
        {
            var catalog = Catalog();
            var pattern = new Regex("\"(?:requiredItemName|craftedItemName)\"\\s*:\\s*\"([^\"]+)\"");
            var ids = Directory.GetFiles(JsonFolder, "*.*")
                .Where(path => path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                .SelectMany(path => pattern.Matches(File.ReadAllText(path)).Cast<Match>())
                .Select(match => match.Groups[1].Value)
                .ToList();

            Assert.Greater(ids.Count, 10, "the recipe files were read");
            CollectionAssert.IsEmpty(UnknownIds(catalog, ids), "recipe items that are not in the item database");
        }

        [Test]
        public void SerializedCostsAndPlants_OnlyNameItemsOfTheCatalog()
        {
            var catalog = Catalog();
            var pattern = new Regex(@"^\s*(?:-\s+)?requiredItemName:[ \t]*(\S.*?)\s*$", RegexOptions.Multiline);
            var ids = new List<string>();
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/")))
            {
                if (!(path.EndsWith(".asset") || path.EndsWith(".prefab") || path.EndsWith(".unity")))
                {
                    continue;
                }
                var text = File.ReadAllText(path);
                if (!text.Contains("ItemName:"))
                {
                    continue;
                }
                ids.AddRange(pattern.Matches(text).Cast<Match>().Select(match => match.Groups[1].Value.Trim('"', '\'')));
            }

            Assert.Greater(ids.Count, 0, "some ship costs name an item");
            CollectionAssert.IsEmpty(UnknownIds(catalog, ids), "serialized item names that are not in the item database");
        }

        [Test]
        public void Database_HasExactlyOneCookingPot()
        {
            var pots = Catalog().All.Where(item => item.HasTag(ItemTag.CookingPot)).Select(item => item.Id.Value).ToList();
            CollectionAssert.AreEqual(new[] { "Pot" }, pots);
        }
    }
}
