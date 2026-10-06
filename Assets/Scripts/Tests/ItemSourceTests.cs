using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Trade;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.Tests
{
    /// <summary>
    /// The item -> source table: where every item of the game comes from. An item nobody can gather, craft or cook is a dead
    /// end for the player, so each one needs a source or a place on <see cref="NoSourceYet"/> (a list that should only shrink).
    /// Run with: Unity -runTests -testPlatform EditMode (the Editor must be closed).
    /// </summary>
    public class ItemSourceTests
    {
        private const string HomeMapPath = "Assets/Resources/ScriptableObjects/Maps/Home.asset";
        private const string ExpeditionMapPath = "Assets/Resources/ScriptableObjects/Maps/Expedition.asset";
        private const string DatabasePath = "Assets/ScriptableObjects/Configs/DatabaseSO.asset";
        private const string ItemRecipesPath = "Assets/Resources/JSON/HackyRecipies.json";
        private const string BuildingRecipesPath = "Assets/Resources/JSON/BuildingsRecipies.JSON";

        // Items with no source on purpose (or not yet). Remove an item from here when it gets one.
        private static readonly string[] NoSourceYet =
        {
            "Honeycomb", // no bees in the game yet
        };

        // Sources the asset scan cannot see: they are wired in code or in the map's entity list.
        private static readonly Dictionary<string, string> SpecialSources = new()
        {
            { "FlatFish", "fish holes (entities of Home, FishingPopup)" },
            { "Water", "built Well" },
        };

        private static readonly Regex CraftedName = new("\"craftedItemName\"\\s*:\\s*\"(\\w+)\"", RegexOptions.Compiled);

        private static PerlinNoiseMapConfig ConfigOf(string mapPath) => AssetDatabase.LoadAssetAtPath<MapSO>(mapPath).perlinNoiseConfig;

        private static DatabaseSO Database() => AssetDatabase.LoadAssetAtPath<DatabaseSO>(DatabasePath);

        /// <summary>Item asset name -> the biomes (of Home, or of the Expedition map) in which something that drops it grows.</summary>
        private static Dictionary<string, List<string>> GatheredOnHome()
        {
            var result = new Dictionary<string, List<string>>();
            foreach (var (mapName, config) in new[] { ("Home", ConfigOf(HomeMapPath)), ("Expedition", ConfigOf(ExpeditionMapPath)) })
            foreach (var biome in config.Biomes)
            {
                foreach (var spawn in biome.Resources)
                {
                    var resource = spawn.resourcePrefab != null ? spawn.resourcePrefab.GetComponent<Hearthglade.Gameplay.Resource.Resource>() : null;
                    var resourceSO = resource != null ? resource.resourceSO : null;
                    if (resourceSO == null)
                    {
                        continue;
                    }
                    // The main item and the bonus drop (a seed from a wild plant) are both found there.
                    foreach (var item in new[] { resourceSO.ItemSoOnGather, resourceSO.BonusChance > 0f ? resourceSO.BonusItem : null })
                    {
                        if (item == null)
                        {
                            continue;
                        }
                        if (!result.TryGetValue(item.name, out var biomes))
                        {
                            result[item.name] = biomes = new List<string>();
                        }
                        var label = mapName + ":" + biome.BiomeName;
                        if (!biomes.Contains(label))
                        {
                            biomes.Add(label);
                        }
                    }
                }
            }
            return result;
        }

        private static HashSet<string> Crafted()
        {
            var names = new HashSet<string>();
            foreach (var path in new[] { ItemRecipesPath, BuildingRecipesPath })
            {
                foreach (Match match in CraftedName.Matches(File.ReadAllText(path)))
                {
                    names.Add(match.Groups[1].Value);
                }
            }
            return names;
        }

        /// <summary>Item asset name -> how farming gives it: grown as a crop's produce, or dropped back as its seed.</summary>
        private static Dictionary<string, string> Farmed()
        {
            var result = new Dictionary<string, string>();
            foreach (var crop in Database().Crops.Where(c => c != null))
            {
                if (crop.produce != null)
                {
                    result[crop.produce.name] = "grown (" + crop.name + ")";
                }
                if (crop.seed != null && crop.seedDropChance > 0f)
                {
                    result.TryAdd(crop.seed.name, "seed drop (" + crop.name + ")");
                }
            }
            return result;
        }

        /// <summary>Item asset name -> the ports that sell it.</summary>
        private static Dictionary<string, List<string>> Traded()
        {
            var result = new Dictionary<string, List<string>>();
            foreach (var guid in AssetDatabase.FindAssets("t:PortSO"))
            {
                var port = AssetDatabase.LoadAssetAtPath<PortSO>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var offer in port.offers.Where(o => o.item != null && o.stock > 0))
                {
                    if (!result.TryGetValue(offer.item.name, out var ports))
                    {
                        result[offer.item.name] = ports = new List<string>();
                    }
                    if (!ports.Contains(port.name))
                    {
                        ports.Add(port.name);
                    }
                }
            }
            return result;
        }

        private static HashSet<string> Cooked()
        {
            return new HashSet<string>(Database().CookingRecipes.Where(r => r != null && r.TargetItem != null).Select(r => r.TargetItem.name));
        }

        [Test]
        public void EveryItem_HasASource()
        {
            var gathered = GatheredOnHome();
            var crafted = Crafted();
            var cooked = Cooked();
            var farmed = Farmed();
            var traded = Traded();
            var table = new StringBuilder("item -> source\n");
            var missing = new List<string>();

            foreach (var item in Database().Items.Where(i => i != null).OrderBy(i => i.name))
            {
                var sources = new List<string>();
                if (gathered.TryGetValue(item.name, out var biomes))
                {
                    sources.Add("gathered in " + string.Join("/", biomes));
                }
                if (traded.TryGetValue(item.name, out var ports))
                {
                    sources.Add("sold by " + string.Join("/", ports));
                }
                if (crafted.Contains(item.name))
                {
                    sources.Add("crafted");
                }
                if (cooked.Contains(item.name))
                {
                    sources.Add("cooked");
                }
                if (farmed.TryGetValue(item.name, out var farming))
                {
                    sources.Add(farming);
                }
                if (SpecialSources.TryGetValue(item.name, out var special))
                {
                    sources.Add(special);
                }
                table.AppendLine($"  {item.name}: {(sources.Count == 0 ? "-" : string.Join(", ", sources))}");
                if (sources.Count == 0 && !NoSourceYet.Contains(item.name))
                {
                    missing.Add(item.name);
                }
            }

            UnityEngine.Debug.Log(table.ToString());
            Assert.IsEmpty(missing, "items nobody can get: " + string.Join(", ", missing));
        }

        [Test]
        public void NoSourceYet_ListsOnlyItemsThatStillHaveNone()
        {
            var gathered = GatheredOnHome();
            var crafted = Crafted();
            var cooked = Cooked();
            foreach (var name in NoSourceYet)
            {
                Assert.IsFalse(gathered.ContainsKey(name) || crafted.Contains(name) || cooked.Contains(name),
                    $"{name} has a source now, remove it from NoSourceYet");
            }
        }
    }
}
