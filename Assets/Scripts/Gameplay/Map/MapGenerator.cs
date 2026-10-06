using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Expedition;
using Hearthglade.Core.Farming;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment.Farming;
using Hearthglade.Gameplay.Map.Generator.PerlinNoise;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using UnityEngine;

namespace Hearthglade.Gameplay.Map {
    public class MapGenerator {

        public const float TILE_X_OFFSET = 0.3675f;
        public const float TILE_Z_OFFSET = 0.3675f;

        // Map generation always runs behind the loading screen, so it may use most of a frame.
        private const double FrameBudgetMs = 12.0;

        private readonly Dictionary<string, MapSO> mapTypesDictionary = new ();


        // Dependencies
        private readonly GameObjectFactory gameObjectFactory;
        private readonly MapFactory mapFactory;
        private readonly CropCatalog crops;
        private readonly References references;
        private readonly SaveManager saveManager;
        //

        /// <summary>Creates the runtime content (scene objects, colliders, block prefabs) of chunks.</summary>
        public BlockContent Content { get; }

        /// <summary>The crops of the game; every map builds its FarmModel from them.</summary>
        public CropCatalog Crops => crops;

        public IWorldClock Clock { get; }

        public MapGenerator( GameObjectFactory gameObjectFactory, MapFactory mapFactory, CropCatalog crops, IWorldClock clock, References references, SaveManager saveManager ) {
            this.saveManager = saveManager;
            this.gameObjectFactory = gameObjectFactory;
            this.mapFactory = mapFactory;
            this.crops = crops;
            this.Clock = clock;
            this.references = references;
            this.Content = new BlockContent( gameObjectFactory, references );
            LoadMapTypes( );
        }

        private void LoadMapTypes( ) {
            var mapTypes = ResourceLoader.LoadAll<MapSO>( ResourceLoader.SO_PATH );
            var log = "Added such map types: ";
            foreach( var mapType in mapTypes ){
                log += mapType.mapName + ", ";
                mapTypesDictionary.Add( mapType.mapName, mapType );
            }
            UnityEngine.Debug.Log( log );
        }

        private Map CreateCleanMap( MapSO mapSO, int seed, ExpeditionTarget? expeditionTrip = null ){
            var mapComponent = mapFactory.Get();
            mapComponent.InitializeMap( this, mapSO, seed, expeditionTrip );
            mapComponent.transform.name = $"Map{mapComponent.MapId}";
            mapComponent.transform.parent = GameObject.FindGameObjectWithTag( UnityTags.MapHolder ).transform;
            return mapComponent;
        }

        /// <summary>
        /// The map's seed is derived from the game seed, the map type and how many maps the game generated
        /// before it: the same game seed always yields the same worlds, but two maps of one type differ.
        /// </summary>
        public static int GetMapSeed( int gameSeed, string mapName, int mapOrdinal ) {
            return SeedMixer.Derive( gameSeed, SeedMixer.HashString( mapName ), mapOrdinal );
        }

        /// <param name="expeditionTrip">The direction and depth of an expedition island: they shift its climate (see <see cref="ExpeditionClimate"/>).</param>
        public async UniTask GenerateNewMapAsync( string mapName, int mapOrdinal, Action<Map> onCreated, CancellationToken ct = default, ExpeditionTarget? expeditionTrip = null ) {
            if( !mapTypesDictionary.TryGetValue( mapName, out var mapSO ) ) {
                UnityEngine.Debug.LogError( $"Not found such map: {mapName}" );
                onCreated?.Invoke( null );
                return;
            }
            var mapComponent = CreateCleanMap( mapSO, GetMapSeed( saveManager.GameSeed, mapName, mapOrdinal ), expeditionTrip );
            onCreated?.Invoke( mapComponent );
            await GenerateMapAsync( mapComponent, ct );
        }

        public async UniTask GenerateMapAsync( Map mapComponent, CancellationToken ct = default ) {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int startFrame = Time.frameCount;
            await GenerateBlocksUsingNoiseAsync( mapComponent, ct );
            UnityEngine.Debug.Log( $"[MapGenerator] {mapComponent.MapType.mapName}: {mapComponent.Models.Count} blocks generated in {stopwatch.ElapsedMilliseconds} ms over {Time.frameCount - startFrame} frames" );
            SpawnMapEntry( mapComponent );
            mapComponent.SpawnBackgroundGameObject();
            // ChunkManager.ForceRefreshChunksAsync() is deliberately NOT called here: it enables
            // chunks around the player's CURRENT position, but for a freshly generated map the
            // player hasn't been moved to their spawn point yet (that happens in MapManager,
            // after this returns, via SetPlayerAndEntryPosition). Calling it here would enable
            // chunks around the wrong (pre-teleport) position, leaving the player standing on an
            // inactive chunk with no colliders. Callers must refresh the chunks themselves
            // after positioning the player.
        }

        private async UniTask GenerateBlocksUsingNoiseAsync( Map mapComponent, CancellationToken ct ) {
            var mapInfo = mapComponent.MapType;
            var config = mapInfo.perlinNoiseConfig;
            if( mapInfo.SizeInChunks <= 0 ) {
                UnityEngine.Debug.LogError( $"Map {mapInfo.mapName} has size {mapInfo.SizeInChunks}: endless maps cannot be generated up front" );
                return;
            }
            int mapEdgeSize = ChunkManager.ChunkSize * mapInfo.SizeInChunks;

            // ScriptableObjects are read here, on the main thread; the noise itself is pure and runs on the pool.
            var heightSettings = PerlinNoiseGenerator.ToSettings( config.HeightNoisePreset );
            var temperatureSettings = PerlinNoiseGenerator.ToSettings( config.TemperatureMapPreset );
            var humiditySettings = PerlinNoiseGenerator.ToSettings( config.HumidityMapPreset );
            var rules = PerlinNoiseGenerator.ToBiomeRules( config );
            var shape = config.ToShape();
            if( mapComponent.ExpeditionTrip is { } trip ) {
                ExpeditionClimate.Offsets( trip, out shape.TemperatureOffset, out shape.HumidityOffset );
            }
            var liquid = PerlinNoiseGenerator.ToLiquidFlags( config );
            var patches = PerlinNoiseGenerator.ToPatches( config );
            int seed = mapComponent.Seed;
            double densityScale = config.ResourceDensityScale;
            int chunkBudget = config.ResourceChunkBudget;

            // Resource tables are read from the biome assets here, on the main thread; planning what grows is pure and runs on the pool.
            var tables = new ResourceTable[ config.Biomes.Count ];
            var ruleTables = new IReadOnlyList<ResourceRule>[ tables.Length ];
            for( int i = 0; i < tables.Length; i++ ) {
                tables[ i ] = ResourceTable.From( config.Biomes[ i ].Resources );
                ruleTables[ i ] = tables[ i ].Rules;
            }
            var pois = config.Pois.Where( poi => poi != null ).ToList( );
            var poiRules = pois.Select( poi => poi.ToRule( config.Biomes ) ).ToList( );
            var (terrain, plan, sites) = await UniTask.RunOnThreadPool(
                () => {
                    var generated = TerrainGenerator.Generate( mapEdgeSize, seed, heightSettings, temperatureSettings, humiditySettings, rules, shape, liquid, patches );
                    var field = ResourceField.Build( generated, seed, liquid, densityScale );
                    // Sites first: they reserve their cells, and the planner leaves reserved cells empty.
                    var placed = PoiPlacer.Place( field, poiRules, seed );
                    PoiLayout.PaintPaths( generated, placed, poiRules, field.IsGround );
                    var planner = new ResourcePlanner( field, ruleTables, seed, TILE_X_OFFSET, ChunkManager.ChunkSize, chunkBudget );
                    return ( generated, planner.PlanMap( ), placed );
                },
                cancellationToken: ct );

            if( terrain.UnmatchedCount > 0 ) {
                UnityEngine.Debug.LogError( $"{terrain.UnmatchedCount} cells of map {mapInfo.mapName} match no biome, they are left empty. Check the biome ranges of {config.name}." );
            }

            // Blocks are only data here (a BlockModel, a terrain cell, chunk membership): nothing is
            // instantiated until the chunk becomes active, so this is a plain loop over the cells.
            var kinds = new BlockKind[ config.Biomes.Count ];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for ( int x = 0; x < mapEdgeSize; x++ ) {
                for ( int z = 0; z < mapEdgeSize; z++ ) {
                    int cell = terrain.Index( x, z );
                    int biomeIndex = terrain.Biome[ cell ];
                    if( biomeIndex == GeneratedTerrain.NoBiome ) {
                        continue;
                    }
                    var kind = kinds[ biomeIndex ] ??= Content.Catalog.Get( config.Biomes[ biomeIndex ].Block );
                    // Only ground can be farmed, so only ground keeps its climate (water blocks would just grow the save).
                    var model = kind.IsGround
                        ? new BlockModel( x, z, false, kind.PrefabName, terrain.Patch[ cell ], Climate.Encode( ( float ) terrain.Temperature[ cell ] ), Climate.Encode( ( float ) terrain.Humidity[ cell ] ) )
                        : new BlockModel( x, z, false, kind.PrefabName, terrain.Patch[ cell ] );
                    if( kind.IsGround ) {
                        plan.TryGetValue( cell, out var planned );
                        Content.AddSceneObjects( model, planned, tables );
                    }
                    mapComponent.SetBlock( model, kind );

                    if ( sw.Elapsed.TotalMilliseconds >= FrameBudgetMs ) {
                        await UniTask.Yield( PlayerLoopTiming.Update, ct );
                        sw.Restart();
                    }
                }
            }

            if( sites.Count > 0 ) {
                int pieces = PoiBuilder.Apply( mapComponent, pois, sites, seed );
                UnityEngine.Debug.Log( $"[MapGenerator] {mapInfo.mapName}: {sites.Count} points of interest placed ({pieces} objects)" );
            }
        }

        private void SpawnMapEntry( Map mapComponent ) {
            var mapInfo = mapComponent.MapType;
            if ( mapInfo.entryObject == null ) {
                return;
            }
            mapComponent.mapEntry = gameObjectFactory.Get(
                mapInfo.entryObject,
                Vector3.zero,
                Quaternion.Euler(mapInfo.entryObject.transform.eulerAngles),
                mapComponent.transform
            );
            mapComponent.mapEntry.name = mapInfo.entryObject.name;
        }

        public async UniTask LoadMapAsync( MapModel savedMapState, bool setActive, Action<Map> onCreated, CancellationToken ct = default ){
            if( !mapTypesDictionary.TryGetValue( savedMapState.mapTypeName, out var mapSO ) ) {
                UnityEngine.Debug.LogError( $"Map type: {savedMapState.mapTypeName} not found" );
                return;
            }
            var mapComponent = CreateCleanMap( mapSO, savedMapState.seed );

            mapComponent.RestoreDataFromSavedState( savedMapState );
            onCreated?.Invoke( mapComponent );

            await LoadBlocksAsync( mapComponent, savedMapState.blockModels, ct );
            mapComponent.SpawnBackgroundGameObject();

            mapComponent.gameObject.SetActive( setActive );
            if( setActive ) {
                // Chunks are streamed around the player, who only stands on the active map. An inactive
                // map is refreshed by MapManager when the player enters it.
                await mapComponent.ChunkManager.ForceRefreshChunksAsync();
            }
        }

        /// <summary>Swaps one block for another at the same grid position and refreshes the terrain around it.</summary>
        public void ReplaceBlock( Map map, SerializableVector2Int cell, GameObject blockPrefab ) {
            var kind = Content.Catalog.Get( blockPrefab );
            if( kind == null || !map.Models.TryGetValue( cell, out var oldModel ) ) {
                return;
            }
            Content.HideSceneObjects( oldModel );
            map.RemoveBlock( cell );

            // A block the player swapped in grows nothing of its own.
            var model = new BlockModel( cell.x, cell.y, false, kind.PrefabName, 0, oldModel.climateT, oldModel.climateH );
            map.SetBlock( model, kind );
            map.ChunkManager.OnBlockChanged( cell );
            map.ChunkManager.InvalidateVisualsAround( cell.x, cell.y );
        }

        private async UniTask LoadBlocksAsync( Map targetMap, Dictionary<SerializableVector2Int, BlockModel> blockModelsDictionary, CancellationToken ct ){
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var missingPrefabs = new HashSet<string>();
            foreach( var blockModel in blockModelsDictionary.Values ) {
                if( blockModel == null ) {
                    continue;
                }
                var kind = Content.Catalog.Get( blockModel.prefabName );
                if( kind == null ) {
                    if( missingPrefabs.Add( blockModel.prefabName ) ) {
                        UnityEngine.Debug.LogError( $"Saved block prefab {blockModel.prefabName} not found, its blocks are dropped" );
                    }
                    continue;
                }
                // Objects are kept as saved (a gathered resource stays gone). Saves keep the object keys of the
                // launch that wrote them, so they are recomputed; a block saved without objects simply has none.
                blockModel.RebuildObjectKeys();
                targetMap.SetBlock( blockModel, kind );

                if ( sw.Elapsed.TotalMilliseconds >= FrameBudgetMs ) {
                    await UniTask.Yield( PlayerLoopTiming.Update, ct );
                    sw.Restart();
                }
            }
        }


        public MapSO GetMapType( string mapName ) {
            return mapTypesDictionary[ mapName ];
        }

        public bool TryGetMapType( string mapName, out MapSO mapType ) {
            return mapTypesDictionary.TryGetValue( mapName, out mapType );
        }

    }
}
