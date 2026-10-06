using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hearthglade.Core.Expedition;
using Hearthglade.Core.Farming;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Entities;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Map.Visual;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    [Serializable]
    public class Map : MonoBehaviour {
        public int MapId { get; private set; }
        public MapSO MapType { get; private set; }

        /// <summary>Compact per-cell description of the map that chunk visuals are built from.</summary>
        public TerrainGrid Terrain { get; private set; }

        /// <summary>
        /// Which build-grid cells are taken by placed building pieces this session (docs/BUILDING_SYSTEM_PLAN.md
        /// Phase 3). Not populated from save data yet - only pieces placed since this map was loaded are in it.
        /// </summary>
        public BuildOccupancyGrid BuildGrid { get; private set; }

        /// <summary>Which build-grid edges (walls, doors) are taken - see <see cref="BuildEdgeGrid"/>. Restored
        /// from save the same way as <see cref="BuildGrid"/> (BlockContent.ShowSceneObjects).</summary>
        public BuildEdgeGrid EdgeGrid { get; private set; }

        /// <summary>Which cells have a floor tile - see <see cref="BuildFloorGrid"/> (docs/BUILDING_SYSTEM_PLAN.md
        /// section 9). Restored from save the same way as <see cref="BuildGrid"/>/<see cref="EdgeGrid"/>.</summary>
        public BuildFloorGrid FloorGrid { get; private set; }

        /// <summary>Every garden plot of the map and the crops in it (docs/FARMING_PLAN.md). Pure data: it grows
        /// with the world clock whether or not the plots' chunks are loaded, and is saved with the map.</summary>
        public FarmModel Farm { get; private set; }
        public int Seed { get; private set; }

        /// <summary>WorldGenVersion the map was generated with: the current one for a new map, the saved one for a loaded map.</summary>
        public int GeneratorVersion { get; private set; }
        public Vector3 EntryPosAtPreviousMap { get; set; }
        public bool HasEntryAtPreviousMap { get; set; }

        /// <summary>The direction and depth this island was sailed to (null for Home, ports and caves).</summary>
        public ExpeditionTarget? ExpeditionTrip { get; private set; }

        /// <summary>
        /// Every block of the map as data. Blocks are not GameObjects: what exists at runtime (scene objects,
        /// ground colliders, prefabs of non-ground blocks) is created per chunk while the chunk is active.
        /// </summary>
        public Dictionary<SerializableVector2Int, BlockModel> Models { get; private set; }
        public ChunkManager ChunkManager { get; private set; }
        public EntityManager EntitySpawner { get; private set; }

        private MapGenerator mapGenerator;
        private PlayerController playerController;
        private ICropCatalog cropCatalog;

        /// <summary>The world time that crops grow by (animals read it to know what is ripe).</summary>
        public IWorldClock WorldClock { get; private set; }

        [ NonSerialized ] public GameObject mapEntry = null;
        [ NonSerialized ] private int islandSizeThreshold = 5;

        public void InitializeMap( MapGenerator mapGenerator, MapSO mapSO, int seed, ExpeditionTarget? expeditionTrip = null ) {
            this.MapType = mapSO;
            this.Seed = seed;
            this.ExpeditionTrip = expeditionTrip;
            this.GeneratorVersion = WorldGenVersion.Current;
            this.MapId = GetHashCode();
            this.Models = new Dictionary<SerializableVector2Int, BlockModel>();
            this.Terrain = new TerrainGrid( Math.Max( 0, ChunkManager.ChunkSize * mapSO.SizeInChunks ) );
            this.BuildGrid = new BuildOccupancyGrid();
            this.EdgeGrid = new BuildEdgeGrid();
            this.FloorGrid = new BuildFloorGrid();
            this.cropCatalog = mapGenerator.Crops;
            this.WorldClock = mapGenerator.Clock;
            this.Farm = new FarmModel( cropCatalog );
            this.ChunkManager = new ChunkManager( this, playerController, mapGenerator.Content );
            this.EntitySpawner = new EntityManager( this, playerController );
            this.mapGenerator = mapGenerator;
        }

        public void SetPlayerController( PlayerController playerController  ) {
            this.playerController = playerController;
        }

        public void RestoreDataFromSavedState( MapModel savedMapState ) {
            this.HasEntryAtPreviousMap = savedMapState.hasEntryAtPreviousMap;
            this.EntryPosAtPreviousMap = savedMapState.entryPosAtPreviousMap;
            this.MapId = savedMapState.mapId;
            this.Seed = savedMapState.seed;
            this.GeneratorVersion = savedMapState.generatorVersion;
            // Depth 0 is what a save that predates expeditions (or a map that is no expedition) holds.
            this.ExpeditionTrip = savedMapState.expeditionDepth > 0
                ? new ExpeditionTarget( ( Direction ) savedMapState.expeditionDirection, savedMapState.expeditionDepth )
                : null;
            // Before any object spawns, so plots find their crops when their chunk is shown. Old saves have none.
            this.Farm = FarmModel.FromSnapshot( cropCatalog, savedMapState.farm );
        }

        // Called every frame: only reads the player position, streams chunks when the player crosses a border.
        public void UpdateChunks() {
            ChunkManager.Tick();
        }

        // Called once per game tick (GameConfig.SECONDS_PER_TICK).
        public void UpdateEntities( bool isDay ) {
            EntitySpawner.Tick( isDay );
        }

        /// <summary>Adds a block (data only): its model, terrain cell and chunk membership.</summary>
        public void SetBlock( BlockModel model, BlockKind kind ) {
            var cell = new SerializableVector2Int( model.gridX, model.gridY );
            if( Models.ContainsKey( cell ) ) {
                UnityEngine.Debug.LogWarning( $"Block with id {cell} exists on {name} map" );
                return;
            }
            Models[ cell ] = model;
            if( Terrain.InBounds( cell.x, cell.y ) ) {
                Terrain.Set( cell.x, cell.y, kind.ToCell( model.isWall, model.patch ) );
            }
            ChunkManager.AddBlock( cell );
        }

        /// <summary>Takes a block out of the map (model, chunk, terrain). Its runtime content is the caller's job.</summary>
        public void RemoveBlock( SerializableVector2Int cell ) {
            Models.Remove( cell );
            ChunkManager.RemoveBlock( cell );
            if( Terrain.InBounds( cell.x, cell.y ) ) {
                Terrain.Set( cell.x, cell.y, default );
            }
        }

        public BlockModel GetModel( SerializableVector2Int blockId ) {
            return Models.TryGetValue( blockId, out var model ) ? model : null;
        }

        public bool IsWorldPositionWalkable( Vector3 worldPos ) {
            var index = MapHelper.WorldPositionToBlockIndex( worldPos );
            return Terrain.Get( index.x, index.y ).Walkable;
        }

        /// <summary>
        /// The ground's current visual height at a cell (docs/BUILDING_SYSTEM_PLAN.md section 9), including
        /// the grass/sand bump (<see cref="TerrainVisualParams.ElevatedRise"/>) if the cell has not been
        /// flattened by a floor tile yet. Only used to rest the floor placement ghost on whatever is actually
        /// rendered there right now, so it stays visible standing on top of the bump while still just a
        /// preview - the piece actually placed rests at <see cref="GroundTopY"/> instead (the post-flatten
        /// height), never this. Every other build category ignores the bump entirely and stacks on
        /// <see cref="BuildGrid"/>'s flat per-story math instead.
        /// </summary>
        public float CurrentGroundTopY( int x, int z ) {
            var config = TerrainVisualSO.Load();
            float baseTopY = config != null ? config.topYOffset : 0.4f;
            float elevatedRise = config != null ? config.elevatedYRise : 0.2f;
            return Terrain.Get( x, z ).TopY( baseTopY, elevatedRise ) ?? baseTopY;
        }

        /// <summary>
        /// The height a placed floor tile rests at (docs/BUILDING_SYSTEM_PLAN.md section 9): the ground's
        /// flattened height, as if <see cref="TerrainCell.Flattened"/> were already set - because placing a
        /// floor tile always flattens its cell (<see cref="BuildingPlacer.PlaceBuilding"/> calls
        /// <see cref="TerrainGrid.SetFlattened"/> right after repositioning the piece to this height), the
        /// tile must rest where the ground will end up, not where a still-bumped grass/sand cell currently
        /// sits (see <see cref="CurrentGroundTopY"/>, used only for the pre-placement ghost) - otherwise the
        /// tile would end up floating once the terrain mesh flattens under it a moment later.
        /// </summary>
        public float GroundTopY( int x, int z ) {
            var config = TerrainVisualSO.Load();
            float baseTopY = config != null ? config.topYOffset : 0.4f;
            float elevatedRise = config != null ? config.elevatedYRise : 0.2f;
            var cell = Terrain.Get( x, z );
            cell.Flattened = true;
            return cell.TopY( baseTopY, elevatedRise ) ?? baseTopY;
        }

        // The measured mesh height of WoodenFloor (Tools/Blender/building_floor_poc.py, printed dims
        // 0.368x0.368x0.047) - the only floor asset today. A placed floor tile's own transform sits at
        // GroundTopY (its bottom), so anything standing on it must rest this much higher, or it visually
        // sinks into the planks (docs/BUILDING_SYSTEM_PLAN.md section 9). If a second floor asset with a
        // different thickness is ever added, this needs to become per-BuildableItemSO instead of one constant.
        public const float FloorTileHeight = 0.047f;

        // How thick a wall/door counts as when it clears grass, in cells (a bit more than the mesh's own thickness).
        private const float EdgePieceThicknessCells = 0.2f;

        private readonly HashSet<int> grassClearedPieces = new HashSet<int>();

        /// <summary>
        /// Keeps grass away from a placed piece whose recipe has <c>clearsGrass</c>: every tuft touching its
        /// footprint grown by <paramref name="radius"/> cells is dropped (per tuft, not per cell - see
        /// <see cref="GrassClearZone"/>). Idempotent per piece id, since a save restore calls this again each
        /// time a chunk is shown.
        /// </summary>
        public void ClearGrassUnderCells( int pieceId, int x0, int z0, int sizeX, int sizeZ, float radius ) {
            AddGrassClearZone( pieceId, GrassClearZone.ForCells( x0, z0, sizeX, sizeZ, radius ) );
        }

        /// <summary>Like <see cref="ClearGrassUnderCells"/> for a wall/door: a thin strip along its edge.</summary>
        public void ClearGrassOnEdge( int pieceId, int x, int z, EdgeSide side, float radius ) {
            AddGrassClearZone( pieceId, GrassClearZone.ForEdge( x, z, side, EdgePieceThicknessCells, radius ) );
        }

        private void AddGrassClearZone( int pieceId, GrassClearZone zone ) {
            if( !grassClearedPieces.Add( pieceId ) ) {
                return;
            }
            Terrain.AddGrassClearZone( zone );

            // Rebuild every chunk the zone reaches (a tuft up to a card's width past it can still be removed).
            float reach = zone.Radius + 1f;
            var touched = new HashSet<ChunkCoord>();
            foreach( var x in new[] { zone.CenterX - zone.HalfX - reach, zone.CenterX + zone.HalfX + reach } ) {
                foreach( var z in new[] { zone.CenterZ - zone.HalfZ - reach, zone.CenterZ + zone.HalfZ + reach } ) {
                    int cellX = Mathf.RoundToInt( x ), cellZ = Mathf.RoundToInt( z );
                    if( touched.Add( ChunkMath.ChunkOf( cellX, cellZ, ChunkManager.ChunkSize ) ) ) {
                        ChunkManager.InvalidateVisualsAround( cellX, cellZ );
                    }
                }
            }
        }

        /// <summary>
        /// How far above the flat build-layer baseline (layer 0 = Y 0) non-floor pieces must rest over the
        /// given cell footprint: the grass/sand bump of a still-elevated cell, or a floor tile's thickness.
        /// The highest cell wins, so a piece spanning uneven ground never sinks into it.
        /// </summary>
        public float BuildBaseRise( int x0, int z0, int sizeX, int sizeZ ) {
            var config = TerrainVisualSO.Load();
            float elevatedRise = config != null ? config.elevatedYRise : 0.2f;
            float rise = 0f;
            for( int x = x0; x < x0 + sizeX; x++ ) {
                for( int z = z0; z < z0 + sizeZ; z++ ) {
                    float cellRise = Terrain.Get( x, z ).TopY( 0f, elevatedRise ) ?? 0f;
                    if( FloorGrid.Has( x, z ) ) {
                        cellRise += FloorTileHeight;
                    }
                    rise = Mathf.Max( rise, cellRise );
                }
            }
            return rise;
        }

        /// <summary>
        /// Whether the player can step from fromPos to toPos: toPos's terrain must be walkable (existing
        /// check), AND no wall/door edge (<see cref="EdgeGrid"/>, at fromPos's own Y layer) may separate the
        /// two cells - a single-cell walkability check can't express "you may enter this cell, but not
        /// through this side", since a wall belongs to an edge between cells, not to either cell's interior.
        /// A blocked edge also keeps the player at least <see cref="WallCollisionRadius"/> away from the
        /// boundary itself, not just out of the far cell: a wall sits on the cell boundary with only its own
        /// thin thickness, so without this margin the player's pivot could reach the boundary exactly, letting
        /// the (much wider) player model clip halfway through the wall before a cell-crossing was ever detected.
        /// </summary>
        public bool CanMoveTo( Vector3 fromPos, Vector3 toPos ) {
            return IsWorldPositionWalkable( toPos )
                && !IsStepBlockedByEdges( fromPos, toPos, Mathf.RoundToInt( fromPos.y / MapGenerator.TILE_X_OFFSET ) );
        }

        /// <summary>
        /// Whether an animal can step from fromPos to toPos: free ground at toPos, and no wall, fence or closed
        /// gate on the ground layer in the way (the same edge rules as <see cref="CanMoveTo"/>, so a fenced garden keeps animals out).
        /// </summary>
        public bool CanStep( Vector3 fromPos, Vector3 toPos ) {
            return IsFreeGround( toPos ) && !IsStepBlockedByEdges( fromPos, toPos, 0 );
        }

        private bool IsStepBlockedByEdges( Vector3 fromPos, Vector3 toPos, int y ) {
            var fromCell = MapHelper.WorldPositionToBlockIndex( fromPos );

            if( IsBlockedByNearbyWall( toPos.x, fromPos.x, fromCell.x, MapGenerator.TILE_X_OFFSET,
                    dx => IsEdgeBlocked( fromCell.x, fromCell.y, dx, 0, y ) ) ) {
                return true;
            }
            if( IsBlockedByNearbyWall( toPos.z, fromPos.z, fromCell.y, MapGenerator.TILE_Z_OFFSET,
                    dz => IsEdgeBlocked( fromCell.x, fromCell.y, 0, dz, y ) ) ) {
                return true;
            }
            return false;
        }

        // Roughly the player model's own half-width (its trigger BoxCollider on Player.prefab is ~0.17m on
        // each horizontal axis) - how far from a blocked edge's boundary the player must stop.
        private const float WallCollisionRadius = 0.12f;

        private bool IsBlockedByNearbyWall( float toCoord, float fromCoord, int fromCell, float tileSize, Func<int, bool> edgeBlocked ) {
            int direction = toCoord > fromCoord ? 1 : toCoord < fromCoord ? -1 : 0;
            if( direction == 0 || !edgeBlocked( direction ) ) {
                return false;
            }
            float boundary = ( fromCell + 0.5f * direction ) * tileSize;
            return direction > 0 ? toCoord > boundary - WallCollisionRadius : toCoord < boundary + WallCollisionRadius;
        }

        private bool IsEdgeBlocked( int x, int z, int dx, int dz, int y ) {
            var ( ex, ez, side ) = BuildEdgeGrid.Canonicalize( x, z, dx, dz );
            // A door left open still occupies the edge (see BuildEdgeGrid.IsOccupied), but should not block
            // movement while it's open.
            return EdgeGrid.IsOccupied( ex, ez, y, side ) && !EdgeGrid.IsPassable( ex, ez, y, side );
        }

        /// <summary>
        /// Registers an object placed by the player (a building) with the block under it and parents it to the
        /// chunk, so it is saved with the map and hidden with the chunk. Returns the created model (its hash is
        /// the stable id callers use to also track the piece elsewhere, e.g. <see cref="BuildGrid"/>), or null
        /// if there is no block to attach it to.
        /// </summary>
        public SceneObjectModel AddSceneObject( Transform placedObject ) {
            var cell = MapHelper.WorldPositionToBlockIndex( placedObject.position );
            if( !Models.TryGetValue( cell, out var model ) ) {
                UnityEngine.Debug.LogWarning( $"Nothing to attach {placedObject.name} to at {cell}" );
                return null;
            }
            var sceneObject = new SceneObjectModel( placedObject );
            model.blockObjects.TryAdd( sceneObject.GetHashCode(), sceneObject );
            if( placedObject.TryGetComponent<SceneObject>( out var component ) ) {
                component.SceneObjectModel = sceneObject;
            }
            var chunkTransform = ChunkManager.GetChunkTransform( cell );
            if( chunkTransform != null ) {
                placedObject.SetParent( chunkTransform );
            }
            return sceneObject;
        }

        // A block the player can stand on that belongs to a walkable region bigger than islandSizeThreshold
        // (so they do not spawn on a one-tile island). The starting corner chunk is tried first, then the
        // rest of the map. Every check stops after islandSizeThreshold+1 tiles, not after the whole island.
        public BlockModel FindNodeToPlacePlayer() {
            if( MapType.perlinNoiseConfig != null && MapType.perlinNoiseConfig.HasIslandShape ) {
                return FindNodeOnWestCoast();
            }
            int cornerEnd = ChunkManager.ChunkSize;
            int mapEdge = ChunkManager.ChunkSize * MapType.SizeInChunks;
            return FindPlayerNode( 0, cornerEnd, 0, cornerEnd, islandSizeThreshold )
                ?? FindPlayerNode( 0, mapEdge, 0, mapEdge, islandSizeThreshold )
                ?? FindPlayerNode( 0, mapEdge, 0, mapEdge, 0 ); // a map made only of tiny islands: any walkable tile will do
        }

        // The island maps start the player on the west coast, in the middle, a couple of cells from the water.
        private BlockModel FindNodeOnWestCoast() {
            int size = ChunkManager.ChunkSize * MapType.SizeInChunks;
            bool Flat( int x, int y ) => IsWalkableGround( x, y );
            if( PlayerStartFinder.TryFind( size, Flat, islandSizeThreshold, out int x, out int y )
                && Models.TryGetValue( new SerializableVector2Int( x, y ), out var model ) ) {
                return model;
            }
            return FindPlayerNode( 0, size, 0, size, islandSizeThreshold ) ?? FindPlayerNode( 0, size, 0, size, 0 );
        }

        private BlockModel FindPlayerNode( int fromX, int toX, int fromY, int toY, int minIslandSize ) {
            for( var x = fromX; x < toX; x++ ) {
                for( var y = fromY; y < toY; y++ ) {
                    if( IsWalkableGround( x, y ) && GridFlood.CountReachable( x, y, IsWalkableGround, minIslandSize ) > minIslandSize ) {
                        return Models[ new SerializableVector2Int( x, y ) ];
                    }
                }
            }
            return null;
        }

        private bool IsWalkableGround( int gridX, int gridY ) {
            var cell = Terrain.Get( gridX, gridY );
            return cell.Ground && cell.Walkable && !cell.Wall;
        }

        /// <summary>Ground that can be walked on at a world position (not water, not a wall).</summary>
        public bool IsFreeGround( Vector3 worldPos ) {
            var index = MapHelper.WorldPositionToBlockIndex( worldPos );
            return IsWalkableGround( index.x, index.y );
        }

        private void SetPlayerPosition( BlockModel playerNode ) {
            var position = HasEntryAtPreviousMap ? EntryPosAtPreviousMap : playerNode.WorldPosition;
            playerController.transform.SetPositionAndRotation( position, Quaternion.identity );
            // The player is an interpolated Rigidbody: without moving the body too, its old pose wins on the next physics step
            // (unnoticed while the start was next to the origin).
            if( playerController.TryGetComponent<Rigidbody>( out var body ) ) {
                body.position = position;
                body.rotation = Quaternion.identity;
            }
            Physics.SyncTransforms();
        }

        private void SetEntryPosition( BlockModel playerNode, int destinationMapId ) {
            var mapEntryComponent = mapEntry.GetComponent<MapEntryBase>();
            if( mapEntryComponent == null ) {
                UnityEngine.Debug.LogError( "mapEntryComponent not found" );
                return;
            }

            if ( !mapEntryComponent.NewMapOnEachEntry ) {
                mapEntryComponent.DestinationMapId = destinationMapId;
            }
            
            mapEntry.transform.position = mapEntryComponent.GetEntryPos( playerNode, this );

            // The entry object already exists (SpawnMapEntry), so its model is registered as spawned:
            // Block.ShowSceneObjects must not instantiate a second copy of it.
            var sceneObject = new SceneObjectModel( mapEntry.transform );
            mapEntryComponent.SceneObjectModel = sceneObject;
            var targetBlock = GetModel( MapHelper.WorldPositionToBlockIndex( playerNode.WorldPosition ));
            targetBlock.blockObjects.TryAdd( sceneObject.GetHashCode(), sceneObject );
        }

        public void SetPlayerAndEntryPosition( int destinationMapId, bool setPlayerPosition = true, bool setEntryPosition = true ) {
            UnityEngine.Debug.Log("Setting player and entry pos for " + this.gameObject.name + " map");
            var playerNode = FindNodeToPlacePlayer();
            if( playerNode is null ) {
                UnityEngine.Debug.LogError( "Could not find player entry position" );
                return;
            }

            UnityEngine.Debug.Log( $"Player start on {name}: cell ({playerNode.gridX}, {playerNode.gridY})" );
            if(setPlayerPosition) {
                SetPlayerPosition( playerNode );
            }

            if(setEntryPosition) {
                SetEntryPosition( playerNode, destinationMapId );
            }
        }

        //TODO remove this one
        [Obsolete("USE TryOverrideAdditionalDataOfSceneObject( GameObject, object )")]
        public bool TryOverrideAdditionalDataOfSceneObject( SceneObjectModel sceneObjectModel ) {
            var hexIndex = MapHelper.WorldPositionToBlockIndex( sceneObjectModel.pos );
            if ( !Models.TryGetValue( hexIndex, out var block ) ) {
                return false;
            }

            var hashCode = sceneObjectModel.GetHashCode( );
            if ( !block.blockObjects.TryGetValue( hashCode, out var existingSceneObject ) ) {
                UnityEngine.Debug.LogWarning( $"Not found object with Hashcode: { hashCode }" );
                return false;
            }

            existingSceneObject.additionalData = sceneObjectModel.additionalData;
            return true;
        }
        
        public void TryOverrideAdditionalDataOfSceneObject( GameObject gameObject, object additionalData ) {
            var sceneObject = gameObject.GetComponent<SceneObject>( ).SceneObjectModel;
            if ( sceneObject is not null ) {
                sceneObject.additionalData = additionalData;
            }
        }

        private const int EntitySpawnAttempts = 32;
        private const float ScreenMargin = 0.05f;
        private readonly List<SerializableVector2Int> loadedChunksBuffer = new();
        private Dictionary<string, BiomeSO> biomesByBlock;
        private Camera mainCamera;

        /// <summary>
        /// A random spot in the loaded chunks, out of the camera's view, in a biome that spawns entities.
        /// On land it is free ground; a biome without ground (water) spawns its entities on the water.
        /// </summary>
        public bool TryFindEntitySpawn( out Vector3 position, out BiomeSO biome ) {
            position = default;
            biome = null;
            loadedChunksBuffer.Clear();
            loadedChunksBuffer.AddRange( ChunkManager.LoadedChunks );
            if( loadedChunksBuffer.Count == 0 ) {
                return false;
            }
            for( int i = 0; i < EntitySpawnAttempts; i++ ) {
                var randomChunkIndex = loadedChunksBuffer[ UnityEngine.Random.Range( 0, loadedChunksBuffer.Count ) ];
                if ( !ChunkManager.TryGetChunk( randomChunkIndex, out var chunk ) || chunk.blocksInChunk.Count == 0 ) {
                    continue;
                }
                var cell = chunk.GetRandomBlockIndex();
                var cellBiome = BiomeAt( cell );
                if( cellBiome == null || cellBiome.Entities.Count == 0 ) {
                    continue;
                }
                var model = Models[ cell ];
                bool isGround = Terrain.Get( cell.x, cell.y ).Ground;
                if( ( isGround && !IsWalkableGround( cell.x, cell.y ) ) || IsPositionOnScreen( model.WorldPosition ) ) {
                    continue;
                }
                position = model.WorldPosition;
                biome = cellBiome;
                return true;
            }
            return false;
        }

        /// <summary>The climate a cell was generated in; the middle of its biome's range when the block carries none (older saves).</summary>
        public Climate ClimateAt( SerializableVector2Int cell ) {
            if( !Models.TryGetValue( cell, out var model ) ) {
                return new Climate( 0f, 0f );
            }
            if( model.climateT != 0 && model.climateH != 0 ) {
                return new Climate( Climate.Decode( model.climateT ), Climate.Decode( model.climateH ) );
            }
            // No record of it (an older save).
            return BiomeAt( cell )?.ClimateCentre ?? new Climate( 0f, 0f );
        }

        /// <summary>The biome a cell belongs to, found through the block it was generated with (so it also works for loaded saves).</summary>
        public BiomeSO BiomeAt( SerializableVector2Int cell ) {
            if( !Models.TryGetValue( cell, out var model ) ) {
                return null;
            }
            biomesByBlock ??= IndexBiomesByBlock();
            return biomesByBlock.TryGetValue( model.prefabName, out var biome ) ? biome : null;
        }

        private Dictionary<string, BiomeSO> IndexBiomesByBlock() {
            var index = new Dictionary<string, BiomeSO>();
            var biomes = MapType.perlinNoiseConfig != null ? MapType.perlinNoiseConfig.Biomes : null;
            if( biomes == null ) {
                return index;
            }
            foreach( var biome in biomes ) {
                if( biome != null && biome.Block != null ) {
                    index.TryAdd( biome.Block.name, biome );
                }
            }
            return index;
        }

        // Entities must not appear in view. (Block.IsOnScreen used to answer this, but OnBecameVisible never
        // fires for a disabled renderer, so ground blocks always counted as off screen.)
        private bool IsPositionOnScreen( Vector3 worldPosition ) {
            if( mainCamera == null ) {
                mainCamera = Camera.main;
                if( mainCamera == null ) {
                    return false;
                }
            }
            var viewport = mainCamera.WorldToViewportPoint( worldPosition );
            return viewport.z > 0f
                && viewport.x > -ScreenMargin && viewport.x < 1f + ScreenMargin
                && viewport.y > -ScreenMargin && viewport.y < 1f + ScreenMargin;
        }

        public void SpawnBackgroundGameObject() {
            var prefab = MapType.backgroundPrefab;
            if( prefab != null ) {
                Instantiate( prefab, prefab.transform.position, Quaternion.identity, transform );
            }
        }
        
        public string GetMapInfo( ) {
            return new StringBuilder( )
                .AppendFormat( "Map Name:{0} | ", MapType.mapName )
                .AppendFormat( "Map Id:{0} | ", MapId )
                .AppendFormat( "Is underground:{0} | ", MapType.isUnderground )
                .AppendFormat( "Is home:{0}.", MapType.IsHome )
                .ToString( );
        }

    }
}
