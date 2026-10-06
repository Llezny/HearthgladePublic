using System.Collections.Generic;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Items.BuildableItems;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    /// <summary>
    /// Creates and removes the runtime content of a chunk: scene objects (trees, rocks, ...), the ground
    /// colliders and the prefabs of blocks that are visible themselves (non-ground ones, e.g. water).
    /// Blocks are data (<see cref="BlockModel"/>) until their chunk becomes active; only then is anything
    /// instantiated, so a 130x130 map does not keep 16900 GameObjects alive.
    /// </summary>
    public sealed class BlockContent
    {
        private static readonly int BlockLayer = LayerMask.NameToLayer(UnityTags.Block);

        private readonly GameObjectFactory gameObjectFactory;
        private readonly References references;

        public BlockCatalog Catalog { get; }

        public BlockContent(GameObjectFactory gameObjectFactory, References references)
        {
            this.gameObjectFactory = gameObjectFactory;
            this.references = references;
            this.Catalog = new BlockCatalog(references);
        }

        // ---- scene object data ---------------------------------------------------------------------------

        /// <summary>
        /// Gives a ground block the resources the <see cref="ResourcePlanner"/> chose for its cell (null = none): the
        /// planner already blended the biome tables, spaced the objects apart and cut the chunk to its budget, so this
        /// only turns the plan into scene object models.
        /// </summary>
        /// <param name="tables">Resource table per biome index of the map config.</param>
        public void AddSceneObjects(BlockModel model, IReadOnlyList<PlannedResource> planned, IReadOnlyList<ResourceTable> tables)
        {
            model.blockObjects = new Dictionary<int, SceneObjectModel>();
            if (planned == null)
            {
                return;
            }
            foreach (var placement in planned)
            {
                var template = tables[placement.Table].Templates[placement.RuleIndex];
                var rotation = template.Euler;
                rotation.y = placement.RotationY;
                var position = new Vector3(
                    model.WorldPosition.x + placement.OffsetX,
                    template.Y,
                    model.WorldPosition.z + placement.OffsetZ);
                var sceneObject = new SceneObjectModel(position, rotation, template.Scale, template.Name);
                model.blockObjects.TryAdd(sceneObject.GetHashCode(), sceneObject);
            }
        }

        // ---- scene objects at runtime ----------------------------------------------------------------------

        // True when at least one scene object of the block still has to be instantiated.
        public static bool HasUnspawnedSceneObjects(BlockModel model)
        {
            if (model?.blockObjects == null)
            {
                return false;
            }
            foreach (var sceneObjectModel in model.blockObjects.Values)
            {
                if (!sceneObjectModel.isSpawned)
                {
                    return true;
                }
            }
            return false;
        }

        // True when at least one scene object of the block is currently instantiated.
        public static bool HasSpawnedSceneObjects(BlockModel model)
        {
            if (model?.blockObjects == null)
            {
                return false;
            }
            foreach (var sceneObjectModel in model.blockObjects.Values)
            {
                if (sceneObjectModel.isSpawned)
                {
                    return true;
                }
            }
            return false;
        }

        // Callers pass the live Map (not a field of BlockContent, since one BlockContent is shared by every
        // map - see docs/BUILDING_SYSTEM_PLAN.md Phase 4/9) so a (re)spawned piece can register itself in
        // whichever of Map.BuildGrid/EdgeGrid/FloorGrid its category belongs to.
        public void ShowSceneObjects(BlockModel model, Transform parent, Map map)
        {
            if (model?.blockObjects == null)
            {
                return;
            }
            List<SceneObjectModel> orphans = null;
            foreach (var sceneObjectModel in model.blockObjects.Values)
            {
                if (sceneObjectModel.isSpawned)
                {
                    continue;
                }
                if (!references.HasGameObject(sceneObjectModel.name))
                {
                    // A save from a game version that had a prefab since removed: it can never spawn, so forget it
                    // instead of retrying (and logging) every time the chunk is shown.
                    (orphans ??= new List<SceneObjectModel>()).Add(sceneObjectModel);
                    continue;
                }
                var sceneObject = SpawnSceneObject(sceneObjectModel, parent);
                if (sceneObject == null)
                {
                    continue;
                }
                sceneObjectModel.isSpawned = true;
                RegisterBuildingPiece(sceneObjectModel, map);
                if (sceneObject.GetComponent(typeof(Resource.Resource)) is Resource.Resource resource)
                {
                    resource.AddInteractionCompletedCallback(() => model.RemoveSceneObject(sceneObjectModel));
                }
            }

            if (orphans != null)
            {
                foreach (var orphan in orphans)
                {
                    UnityEngine.Debug.LogWarning($"Dropped the saved object '{orphan.name}' at block ({model.gridX},{model.gridY}): no such prefab any more.");
                    model.RemoveSceneObject(orphan);
                }
            }
        }

        // A piece placed live by BuildingPlacer already registers itself in BuildGrid/EdgeGrid/FloorGrid on
        // the spot; this only covers pieces that need (re)spawning - loaded from a save, or a chunk that was
        // hidden and shown again. Idempotent (BoxOf/CellOf checks) because the latter can happen many times
        // per piece per session. Walls/doors are edge-anchored (BuildEdgeGrid); floor tiles are their own
        // per-cell set (BuildFloorGrid); everything else is cell-anchored (BuildOccupancyGrid) - see
        // docs/BUILDING_SYSTEM_PLAN.md sections 6 and 9.
        private void RegisterBuildingPiece(SceneObjectModel sceneObjectModel, Map map)
        {
            if (map == null || !references.TryGetBuildableItem(sceneObjectModel.name, out var item))
            {
                return;
            }
            switch (item.category)
            {
                case BuildCategory.Wall:
                case BuildCategory.Fence:
                case BuildCategory.Gate:
                case BuildCategory.Door:
                    RegisterInEdgeGrid(sceneObjectModel, item, map.EdgeGrid);
                    break;
                case BuildCategory.Floor:
                    RegisterInFloorGrid(sceneObjectModel, map);
                    break;
                default:
                    RegisterInCellGrid(sceneObjectModel, item, map.BuildGrid);
                    break;
            }
            if (references.TryGetGrassClearRadius(item, out var grassRadius))
            {
                ClearGrassUnderSavedPiece(sceneObjectModel, item, map, grassRadius);
            }
        }

        // Terrain flags are not saved, so a restored piece re-clears its grass (idempotent per piece id).
        private static void ClearGrassUnderSavedPiece(SceneObjectModel sceneObjectModel, BuildableItemSO item, Map map, float radius)
        {
            if (item.category.IsEdgePiece())
            {
                var (x, z, side) = MapHelper.WorldPositionToEdge(sceneObjectModel.pos);
                map.ClearGrassOnEdge(sceneObjectModel.GetHashCode(), x, z, side, radius);
                return;
            }
            var cell = MapHelper.WorldPositionToBlockIndex(sceneObjectModel.pos);
            map.ClearGrassUnderCells(sceneObjectModel.GetHashCode(), cell.x, cell.y, item.sizeInCells.x, item.sizeInCells.z, radius);
        }

        private void RegisterInCellGrid(SceneObjectModel sceneObjectModel, BuildableItemSO item, BuildOccupancyGrid buildGrid)
        {
            if (buildGrid == null)
            {
                return;
            }
            int pieceId = sceneObjectModel.GetHashCode();
            if (buildGrid.BoxOf(pieceId) != null)
            {
                return;
            }
            var cell = MapHelper.WorldPositionToBlockIndex(sceneObjectModel.pos);
            int yLayer = Mathf.RoundToInt(sceneObjectModel.pos.y / MapGenerator.TILE_X_OFFSET);
            var size = item.sizeInCells;
            var box = new BuildCellBox(cell.x, yLayer, cell.y, size.x, size.y, size.z);
            if (!buildGrid.CanPlace(box))
            {
                // Overlaps something else already there (e.g. two independent saves merged, or the piece's
                // footprint changed since it was saved) - log and skip rather than throw during map load.
                UnityEngine.Debug.LogWarning($"Could not restore build-grid occupancy for '{sceneObjectModel.name}' at {cell}: cells already taken.");
                return;
            }
            buildGrid.Place(pieceId, box);
        }

        private void RegisterInEdgeGrid(SceneObjectModel sceneObjectModel, BuildableItemSO item, BuildEdgeGrid edgeGrid)
        {
            if (edgeGrid == null)
            {
                return;
            }
            int pieceId = sceneObjectModel.GetHashCode();
            if (edgeGrid.BoxOf(pieceId) != null)
            {
                return;
            }
            var (x, z, side) = MapHelper.WorldPositionToEdge(sceneObjectModel.pos);
            int yLayer = Mathf.RoundToInt(sceneObjectModel.pos.y / MapGenerator.TILE_X_OFFSET);
            var box = new BuildEdgeBox(x, z, yLayer, side, item.sizeInCells.y, item.category.ToEdgeKind());
            if (!edgeGrid.CanPlace(box))
            {
                UnityEngine.Debug.LogWarning($"Could not restore edge-grid occupancy for '{sceneObjectModel.name}' at ({x},{z},{side}): edge already taken.");
                return;
            }
            edgeGrid.Place(pieceId, box);
        }

        // Also re-flattens the terrain bump under the tile and invalidates the chunk's visuals
        // (docs/BUILDING_SYSTEM_PLAN.md section 9) - otherwise a reloaded save would have the tile correctly
        // registered in FloorGrid but the ground would still render bumped until something else happened to
        // touch that chunk's terrain.
        private void RegisterInFloorGrid(SceneObjectModel sceneObjectModel, Map map)
        {
            var floorGrid = map.FloorGrid;
            if (floorGrid == null)
            {
                return;
            }
            int pieceId = sceneObjectModel.GetHashCode();
            if (floorGrid.CellOf(pieceId) != null)
            {
                return;
            }
            var cell = MapHelper.WorldPositionToBlockIndex(sceneObjectModel.pos);
            if (!floorGrid.CanPlace(cell.x, cell.y))
            {
                UnityEngine.Debug.LogWarning($"Could not restore floor occupancy for '{sceneObjectModel.name}' at {cell}: cell already has a floor tile.");
                return;
            }
            floorGrid.Place(pieceId, cell.x, cell.y);
            map.Terrain.SetFlattened(cell.x, cell.y, true);
            map.ChunkManager.InvalidateVisualsAround(cell.x, cell.y);
        }

        public void HideSceneObjects(BlockModel model)
        {
            if (model?.blockObjects == null)
            {
                return;
            }
            foreach (var sceneObjectModel in model.blockObjects.Values)
            {
                if (!sceneObjectModel.isSpawned)
                {
                    continue;
                }
                sceneObjectModel.isSpawned = false;
                if (sceneObjectModel.gameObject != null)
                {
                    // The instance is pooled and loses its state, so keep it in the model for the next spawn.
                    if (sceneObjectModel.gameObject.GetComponent(typeof(ISceneObjectWithData)) is ISceneObjectWithData withData)
                    {
                        sceneObjectModel.additionalData = withData.CaptureState();
                    }
                    Lean.Pool.LeanPool.Despawn(sceneObjectModel.gameObject);
                }
                sceneObjectModel.gameObject = null;
            }
        }

        private SceneObject SpawnSceneObject(SceneObjectModel sceneObjectModel, Transform parent)
        {
            if (!references.TryGetGameObject(sceneObjectModel.name, out var sceneObjectPrefab))
            {
                return null;
            }
            sceneObjectModel.gameObject = gameObjectFactory.Get(
                sceneObjectPrefab,
                sceneObjectModel.pos,
                Quaternion.Euler(sceneObjectModel.rot),
                sceneObjectModel.sca,
                parent);

            if (sceneObjectModel.gameObject == null)
            {
                UnityEngine.Debug.LogError($"Cannot spawn such gameObject:  {sceneObjectModel.name}");
                return null;
            }
            if (sceneObjectModel.gameObject.GetComponent(typeof(SceneObject)) is not SceneObject sceneObject)
            {
                UnityEngine.Debug.LogError($"Spawned object without scene object component. name:  {sceneObjectModel.name}");
                Lean.Pool.LeanPool.Despawn(sceneObjectModel.gameObject);
                sceneObjectModel.gameObject = null;
                return null;
            }

            sceneObject.SceneObjectModel = sceneObjectModel;
            if (sceneObjectModel.gameObject.GetComponent(typeof(ISceneObjectWithData)) is ISceneObjectWithData component)
            {
                component.RestoreState(sceneObjectModel.additionalData);
            }
            return sceneObject;
        }

        // ---- block prefabs and colliders ----------------------------------------------------------------------

        /// <summary>
        /// Instantiates the prefab of a block that is visible itself (a non-ground one, e.g. water). Ground
        /// blocks have no instance: the terrain mesh draws them and <see cref="BuildColliders"/> covers them.
        /// </summary>
        public GameObject SpawnBlockVisual(BlockModel model, BlockKind kind, Transform parent)
        {
            var instance = gameObjectFactory.Get(kind.Prefab, model.WorldPosition, Quaternion.identity, Vector3.one, parent);
            if (instance != null)
            {
                instance.name = $"({model.gridX},{model.gridY})";
            }
            return instance;
        }

        /// <summary>
        /// One GameObject with a BoxCollider per ground block, shaped like the collider of the block prefab, on
        /// the Block layer (the player's GroundChecker and the building placer query that layer). The object is
        /// filled while inactive so physics registers all colliders at once when it is switched on.
        /// </summary>
        public GameObject BuildColliders(Chunk chunk, Map map)
        {
            var root = new GameObject("_Colliders") { layer = BlockLayer, tag = UnityTags.Block };
            root.SetActive(false);
            root.transform.SetParent(chunk.chunkGameObject.transform, false);
            var origin = root.transform.position;

            var entries = new List<(SerializableVector2Int cell, BlockKind kind, Vector3 center)>();
            foreach (var cell in chunk.blocksInChunk)
            {
                if (!map.Models.TryGetValue(cell, out var model))
                {
                    continue;
                }
                var kind = Catalog.Get(model.prefabName);
                if (kind == null || !kind.IsGround || !kind.HasCollider)
                {
                    continue;
                }
                entries.Add((cell, kind, model.WorldPosition + kind.ColliderCenter - origin));
            }

            // A row of neighbouring blocks of the same kind whose boxes touch edge to edge is one box: a chunk
            // is then a few dozen colliders instead of 169, which is far cheaper to add and for physics to
            // register. Every collider lives on this one object, so nothing can tell them apart anyway.
            entries.Sort((a, b) => a.cell.y != b.cell.y ? a.cell.y.CompareTo(b.cell.y) : a.cell.x.CompareTo(b.cell.x));
            int i = 0;
            while (i < entries.Count)
            {
                int last = i;
                while (last + 1 < entries.Count && ContinuesRun(entries[last], entries[last + 1]))
                {
                    last++;
                }
                var first = entries[i];
                var size = first.kind.ColliderSize;
                size.x += entries[last].center.x - first.center.x;
                var box = root.AddComponent<BoxCollider>();
                box.center = (first.center + entries[last].center) * 0.5f;
                box.size = size;
                i = last + 1;
            }
            root.SetActive(true);
            return root;
        }

        private static bool ContinuesRun(
            (SerializableVector2Int cell, BlockKind kind, Vector3 center) a,
            (SerializableVector2Int cell, BlockKind kind, Vector3 center) b)
        {
            const float Tolerance = 1e-3f;
            return a.cell.y == b.cell.y
                && b.cell.x == a.cell.x + 1
                && ReferenceEquals(a.kind, b.kind)
                && Mathf.Abs(b.center.x - a.center.x - a.kind.ColliderSize.x) < Tolerance
                && Mathf.Abs(b.center.y - a.center.y) < Tolerance
                && Mathf.Abs(b.center.z - a.center.z) < Tolerance;
        }
    }
}
