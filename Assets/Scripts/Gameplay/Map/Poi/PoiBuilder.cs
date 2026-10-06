using System.Collections.Generic;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Characters;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Map.Visual;
using Hearthglade.Gameplay.Resource;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Newtonsoft.Json;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    /// <summary>
    /// Turns the sites chosen by <see cref="PoiPlacer"/> into scene objects of the map: every piece of a site becomes a
    /// <see cref="SceneObjectModel"/> in the block it stands in, exactly like a building of the player, so saving needs
    /// nothing new. Chests are filled from the loot table of the site.
    /// </summary>
    public static class PoiBuilder
    {
        /// <returns>How many scene objects were added to the map.</returns>
        public static int Apply(Map map, IReadOnlyList<PoiSO> pois, IReadOnlyList<PoiPlacement> sites, int seed)
        {
            int added = 0;
            var stacks = new List<LootStack>();
            foreach (var site in sites)
            {
                var poi = pois[site.Type];
                var lootEntries = poi.ToLootEntries();
                int tier = LootRoller.TierFor(site.StartDistance, poi.tierStep, poi.maxTier);

                for (int i = 0; i < poi.pieces.Count; i++)
                {
                    var piece = poi.pieces[i];
                    if (piece.prefab == null)
                    {
                        continue;
                    }
                    // Turn the offset around the vertical axis by the rotation of the site.
                    PoiLayout.Rotate(site.RotationY, piece.offset.x, piece.offset.y, out float cellsX, out float cellsZ);
                    var template = piece.prefab.transform;
                    var position = new Vector3((site.CellX + cellsX) * MapGenerator.TILE_X_OFFSET, template.position.y, (site.CellY + cellsZ) * MapGenerator.TILE_Z_OFFSET);
                    var rotation = template.eulerAngles;
                    rotation.y += site.RotationY + piece.rotation;

                    int cellX = Mathf.RoundToInt(position.x / MapGenerator.TILE_X_OFFSET);
                    int cellZ = Mathf.RoundToInt(position.z / MapGenerator.TILE_Z_OFFSET);
                    if (!map.Models.TryGetValue(new SerializableVector2Int(cellX, cellZ), out var model))
                    {
                        UnityEngine.Debug.LogWarning($"[Poi] {poi.name} at ({site.CellX},{site.CellY}): the piece {piece.prefab.name} falls on cell ({cellX},{cellZ}), which has no block.");
                        continue;
                    }

                    // A character stands on the ground like the player does: on the grass/sand bump of an elevated cell, not in it.
                    if (piece.prefab.GetComponent<NpcBrain>() != null && map.Terrain.Get(cellX, cellZ).TopY(0f, ElevatedRise()) is float lift)
                    {
                        position.y += lift;
                    }

                    object data = null;
                    if (piece.prefab.GetComponent<ChestSceneObject>() != null)
                    {
                        LootRoller.Roll(lootEntries, poi.lootRollsMin, poi.lootRollsMax, tier, seed, site.CellX, site.CellY + i * 997, stacks);
                        data = ChestJson(stacks);
                    }

                    var sceneObject = new SceneObjectModel(position, rotation, template.localScale, piece.prefab.name, data);
                    model.blockObjects ??= new Dictionary<int, SceneObjectModel>();
                    if (model.blockObjects.TryAdd(sceneObject.GetHashCode(), sceneObject))
                    {
                        added++;
                    }
                }
            }
            return added;
        }

        private static float ElevatedRise()
        {
            var config = TerrainVisualSO.Load();
            return config != null ? config.elevatedYRise : 0f;
        }

        // The same shape a chest writes when it saves its own state (ChestSceneObject.CaptureState).
        private static string ChestJson(List<LootStack> stacks)
        {
            var slots = new List<ItemStackData>();
            foreach (var stack in stacks)
            {
                slots.Add(new ItemStackData { Id = stack.Item, Count = stack.Count });
            }
            return JsonConvert.SerializeObject(slots, Formatting.Indented);
        }
    }
}
