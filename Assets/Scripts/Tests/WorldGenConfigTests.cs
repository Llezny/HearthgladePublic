using System.Linq;
using Hearthglade.Gameplay.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.Tests
{
    /// <summary>
    /// Checks the shipped world generation assets. A biome config with a hole makes cells without a block, and a
    /// noise preset outside the sane ranges brings back the lopsided maps the equalising step was added to fix.
    /// Run with: Unity -runTests -testPlatform EditMode (the Editor must be closed).
    /// </summary>
    public class WorldGenConfigTests
    {
        private const string HomeMapPath = "Assets/Resources/ScriptableObjects/Maps/Home.asset";
        private const string ForestMapPath = "Assets/Resources/ScriptableObjects/Maps/Forest.asset";
        private const string WinterMapPath = "Assets/Resources/ScriptableObjects/Maps/Winter.asset";

        private static PerlinNoiseMapConfig HomeConfig()
        {
            var home = AssetDatabase.LoadAssetAtPath<MapSO>(HomeMapPath);
            Assert.NotNull(home, $"Home map asset not found at {HomeMapPath}");
            Assert.NotNull(home.perlinNoiseConfig, "Home has no perlinNoiseConfig");
            return home.perlinNoiseConfig;
        }

        private static PerlinNoiseMapConfig ForestConfig()
        {
            var forest = AssetDatabase.LoadAssetAtPath<MapSO>(ForestMapPath);
            Assert.NotNull(forest, $"Forest map asset not found at {ForestMapPath}");
            Assert.NotNull(forest.perlinNoiseConfig, "Forest has no perlinNoiseConfig");
            return forest.perlinNoiseConfig;
        }

        private static PerlinNoiseMapConfig WinterConfig()
        {
            var winter = AssetDatabase.LoadAssetAtPath<MapSO>(WinterMapPath);
            Assert.NotNull(winter, $"Winter map asset not found at {WinterMapPath}");
            Assert.NotNull(winter.perlinNoiseConfig, "Winter has no perlinNoiseConfig");
            return winter.perlinNoiseConfig;
        }

        [Test]
        public void ForestConfig_HasNoProblems()
        {
            // Phase 6 session 2: Forest used to have no perlinNoiseConfig at all and crashed on entry.
            var problems = ForestConfig().GetProblems();
            Assert.IsEmpty(problems, string.Join("; ", problems));
        }

        [Test]
        public void WinterConfig_HasNoProblems()
        {
            var problems = WinterConfig().GetProblems();
            Assert.IsEmpty(problems, string.Join("; ", problems));
        }

        [Test]
        public void HomeConfig_HasNoProblems()
        {
            var problems = HomeConfig().GetProblems();
            Assert.IsEmpty(problems, string.Join("; ", problems));
        }

        [Test]
        public void HomeConfig_EveryBiomeHasABlock()
        {
            var config = HomeConfig();
            Assert.GreaterOrEqual(config.Biomes.Count, 4, "Home should have at least water, sand and two land biomes");
            foreach (var biome in config.Biomes)
            {
                Assert.NotNull(biome.Block, $"biome {biome.BiomeName} has no block");
            }
        }

        [Test]
        public void HomeConfig_HasTheBiomeSet()
        {
            var names = HomeConfig().Biomes.Select(b => b.BiomeName).ToList();
            foreach (var expected in new[] { "Water", "Beach", "Rocks", "Taiga", "Swamp", "Forest", "Meadow" })
            {
                CollectionAssert.Contains(names, expected);
            }
        }

        [Test]
        public void HomeConfig_EveryLandBiomeHasAVisibleLookAndCosmeticPatches()
        {
            foreach (var biome in HomeConfig().Biomes)
            {
                var so = biome.Block.GetComponent<Hearthglade.Gameplay.Environment.Block.Base.Block>().blockSO;
                Assert.NotNull(so, biome.BiomeName);
                foreach (var patch in biome.Patches)
                {
                    Assert.Greater(patch.Scale, 0f, $"{biome.BiomeName} patch scale");
                    Assert.That(patch.Coverage, Is.InRange(0f, 1f), $"{biome.BiomeName} patch coverage");
                }
            }
        }

        [Test]
        public void HomeBiomes_DoNotSpawnCaveEntries()
        {
            // Caves are not part of the world for now (see the world generation plan, phase 6).
            foreach (var biome in HomeConfig().Biomes)
            {
                foreach (var resource in biome.Resources)
                {
                    Assert.IsNull(resource.resourcePrefab.GetComponent<Hearthglade.Gameplay.Environment.MapEntryBase>(),
                        $"{biome.BiomeName} spawns the map entry {resource.resourcePrefab.name}");
                }
            }
        }

        [Test]
        public void HomeBiomes_EverySpawnedResourceIsGatherableAndHasACollider()
        {
            foreach (var biome in HomeConfig().Biomes)
            {
                foreach (var resource in biome.Resources)
                {
                    Assert.NotNull(resource.resourcePrefab, $"{biome.BiomeName} has an empty resource entry");
                    var prefab = resource.resourcePrefab;
                    Assert.NotNull(prefab.GetComponent<Hearthglade.Gameplay.Resource.Resource>(),
                        $"{biome.BiomeName}: {prefab.name} is not gatherable (no Resource component)");
                    Assert.NotNull(prefab.GetComponentInChildren<Collider>(true),
                        $"{biome.BiomeName}: {prefab.name} has no collider");
                }
            }
        }

        [Test]
        public void HomeBiomes_ResourceRulesAreSane()
        {
            foreach (var biome in HomeConfig().Biomes)
            {
                foreach (var resource in biome.Resources)
                {
                    var name = $"{biome.BiomeName}: {resource.resourcePrefab.name}";
                    Assert.That(resource.spawnProbability, Is.InRange(0f, 1f), name);
                    if (resource.clusterSpacing > 0)
                    {
                        Assert.GreaterOrEqual(resource.clusterMax, resource.clusterMin, $"{name} deposit size");
                        Assert.GreaterOrEqual(resource.clusterSpacing, 1, name);
                    }
                }
            }
        }

        [Test]
        public void HomeBiomes_BigObjectsAreSpacedApart_AndChunksHaveABudget()
        {
            var config = HomeConfig();
            Assert.Greater(config.ResourceChunkBudget, 0, "Home caps the objects per chunk");
            var spaced = new[] { "PineTree", "SpruceTree", "Stone1", "StoneMedium", "Bush", "Crystal", "IronOre", "GoldOre" };
            foreach (var biome in config.Biomes)
            {
                foreach (var resource in biome.Resources.Where(r => spaced.Contains(r.resourcePrefab.name)))
                {
                    Assert.Greater(resource.minSpacing, 0f, $"{biome.BiomeName}: {resource.resourcePrefab.name} overlaps its neighbours without a spacing");
                    Assert.LessOrEqual(resource.minSpacing, 3f, $"{biome.BiomeName}: {resource.resourcePrefab.name} spacing is huge");
                }
            }
        }

        [Test]
        public void HomePois_AreCompleteAndTheirPiecesFitTheClearedSite()
        {
            var config = HomeConfig();
            Assert.IsNotEmpty(config.Pois, "Home has points of interest");
            var database = AssetDatabase.LoadAssetAtPath<Hearthglade.Gameplay.Database.DatabaseSO>("Assets/ScriptableObjects/Configs/DatabaseSO.asset");
            foreach (var poi in config.Pois)
            {
                Assert.IsNotEmpty(poi.pieces, $"{poi.name} has pieces");
                foreach (var piece in poi.pieces)
                {
                    Assert.NotNull(piece.prefab, $"{poi.name} has an empty piece");
                    Assert.Contains(piece.prefab, database.Prefabs, $"{piece.prefab.name} of {poi.name} must be in the database, or it cannot be spawned by name");
                    Assert.LessOrEqual(piece.offset.magnitude, poi.clearRadius - 0.5f, $"{piece.prefab.name} of {poi.name} sticks out of the cleared site");
                }
                Assert.IsTrue(poi.pieces.Any(p => p.prefab.GetComponent<Hearthglade.Gameplay.Resource.ChestSceneObject>() != null), $"{poi.name} has a chest");
                Assert.IsTrue(poi.loot.Any(l => l.item != null && l.minTier == 0 && l.weight > 0f), $"{poi.name} holds something at the lowest tier");
                foreach (var loot in poi.loot)
                {
                    Assert.NotNull(loot.item, $"{poi.name} has an empty loot entry");
                    Assert.Contains(loot.item, database.Items, $"{loot.item.name} of {poi.name} is not in the item database");
                    Assert.GreaterOrEqual(loot.max, loot.min, $"{poi.name}: {loot.item.name} maximum below minimum");
                }
                Assert.LessOrEqual(poi.lootRollsMax, 6, "a chest has 6 slots");
            }
        }

        [TestCase("HeightNoisePreset")]
        [TestCase("TemperatureMapPreset")]
        [TestCase("HumidityMapPreset")]
        public void HomeNoisePresets_AreInTheSaneRanges(string which)
        {
            var config = HomeConfig();
            var preset = which switch
            {
                "HeightNoisePreset" => config.HeightNoisePreset,
                "TemperatureMapPreset" => config.TemperatureMapPreset,
                _ => config.HumidityMapPreset
            };
            Assert.NotNull(preset, which);
            Assert.GreaterOrEqual(preset.Octaves, 1, "Octaves");
            Assert.That(preset.Persistance, Is.InRange(0.01f, 1f), "Persistance is an amplitude multiplier per octave, below 1 the detail is finer");
            Assert.GreaterOrEqual(preset.Lacunarity, 1f, "Lacunarity is a frequency multiplier per octave, at least 1");
            Assert.GreaterOrEqual(preset.Scale, 1f, "Scale is in map cells per lattice unit");
        }
    }
}
