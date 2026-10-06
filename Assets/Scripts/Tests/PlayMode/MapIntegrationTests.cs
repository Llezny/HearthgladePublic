using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Entities;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Entities;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Hearthglade.PlayModeTests {

    /// <summary>
    /// Boots the real Game scene as a new game and checks the map pipeline end to end: generation from the
    /// seed, chunk streaming, terrain visuals, ground colliders and scene objects.
    /// Run with: Unity -runTests -testPlatform PlayMode (the Editor must be closed).
    /// </summary>
    public class MapIntegrationTests {

        private const int Seed = 12345;
        private const string Tag = "[PlayModeIT]";

        private static async UniTask WaitUntil( Func<bool> condition, float timeoutSeconds, string what ) {
            float start = Time.realtimeSinceStartup;
            while( !condition() ) {
                if( Time.realtimeSinceStartup - start > timeoutSeconds ) {
                    Assert.Fail( $"Timed out after {timeoutSeconds}s waiting for: {what}" );
                }
                await UniTask.Yield();
            }
        }

        private static async UniTask<MapManager> BootNewGame( string playerName = "playmode-test" ) {
            var container = ResourceLoader.LoadSaveContainer();
            container.SaveHeader = new SaveHeader { NewGame = true, PlayerName = playerName, Seed = Seed, SaveDate = "" };

            float start = Time.realtimeSinceStartup;
            await SceneManager.LoadSceneAsync( "Game" );
            var scope = UnityEngine.Object.FindFirstObjectByType<GameplayScope>();
            Assert.NotNull( scope, "GameplayScope not found in the Game scene" );
            var mapManager = scope.Container.Resolve<MapManager>();
            await WaitUntil( () => mapManager.IsMapReady, 120f, "MapManager.IsMapReady" );
            UnityEngine.Debug.Log( $"{Tag} new game (scene load + Home generation + first chunks) ready after {Time.realtimeSinceStartup - start:F2}s" );
            return mapManager;
        }

        // Loads the Game scene with the given save header and waits until the map is ready.
        private static async UniTask<MapManager> LoadGame( SaveHeader header ) {
            ResourceLoader.LoadSaveContainer().SaveHeader = header;
            await SceneManager.LoadSceneAsync( "Game" );
            var scope = UnityEngine.Object.FindFirstObjectByType<GameplayScope>();
            Assert.NotNull( scope, "GameplayScope not found in the Game scene" );
            var mapManager = scope.Container.Resolve<MapManager>();
            await WaitUntil( () => mapManager.IsMapReady, 120f, "MapManager.IsMapReady after loading a save" );
            return mapManager;
        }

        private static int CountModelObjects( Map map ) {
            return map.Models.Values.Sum( model => model.blockObjects?.Count ?? 0 );
        }

        private static string TerrainSignature( Map map ) {
            // Cheap fingerprint of the terrain: number of cells per biome/flag combination plus a position-weighted sum.
            long weighted = 0;
            int ground = 0, elevated = 0;
            for( int y = 0; y < map.Terrain.Size; y++ ) {
                for( int x = 0; x < map.Terrain.Size; x++ ) {
                    var cell = map.Terrain.Get( x, y );
                    if( cell.Ground ) { ground++; }
                    if( cell.Elevated ) { elevated++; }
                    weighted += ( cell.Biome + 1 ) * ( cell.Ground ? 1 : 3 ) * ( x * 31 + y * 17 + 1 );
                }
            }
            return $"ground={ground} elevated={elevated} weighted={weighted}";
        }

        // The player is a Rigidbody: moving only its transform can be undone by physics on the next step.
        private static void Teleport( PlayerController player, Vector3 position ) {
            player.transform.position = position;
            if( player.TryGetComponent<Rigidbody>( out var body ) ) {
                body.position = position;
                body.linearVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
        }

        private static int CountSpawnedSceneObjects( Map map ) {
            return map.Models.Values.Sum( model => model.blockObjects.Values.Count( o => o.isSpawned ) );
        }

        private static int CountLiveSceneObjects( Map map ) {
            // Every spawned scene object is a live, active-in-hierarchy SceneObject under this map.
            return map.GetComponentsInChildren<SceneObject>( false ).Length;
        }

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator NewGame_GeneratesHome_FromTheSeed_WithGroundEverywhereItMatters( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();

            // Terrain comes from the seed.
            Assert.AreEqual( MapGenerator.GetMapSeed( Seed, "Home", 0 ), map.Seed );
            int edge = ChunkManager.ChunkSize * map.MapType.SizeInChunks;
            Assert.Greater( map.Models.Count, edge * edge * 0.95, "almost every cell of the map has a block" );
            Assert.AreEqual( map.Models.Count, map.Terrain.Size * map.Terrain.Size - CountEmptyCells( map ) );

            // Blocks are data: only the active chunks have colliders, not all ~17000 blocks of the map.
            int colliders = map.GetComponentsInChildren<Collider>( true ).Length;
            UnityEngine.Debug.Log( $"{Tag} {map.Models.Count} blocks, {colliders} colliders alive, {map.GetComponentsInChildren<Transform>( true ).Length} transforms under the map" );
            Assert.Less( colliders, map.Models.Count / 4, "colliders exist only for chunks near the player" );
            Assert.Greater( colliders, 0 );

            // The player stands on walkable ground, not on water or a hole.
            var playerCell = MapHelper.WorldPositionToBlockIndex( player.transform.position );
            Assert.IsTrue( map.IsWorldPositionWalkable( player.transform.position ), $"player cell {playerCell} is not walkable" );
            Assert.IsTrue( map.Terrain.Get( playerCell.x, playerCell.y ).Solid, "player stands on a solid ground cell" );

            // Streaming window: 3x3 around the player (fewer at the map corner), all active.
            var playerChunk = ChunkManager.PositionToChunkIndex( player.transform.position );
            Assert.IsTrue( map.ChunkManager.LoadedChunks.Contains( playerChunk ) );
            foreach( var index in map.ChunkManager.LoadedChunks ) {
                Assert.IsTrue( map.ChunkManager.TryGetChunk( index, out var chunk ) );
                Assert.IsTrue( chunk.chunkGameObject.activeInHierarchy, $"chunk {index} active" );
                Assert.AreEqual( ChunkVisualState.Built, chunk.visualState, $"chunk {index} visuals were awaited by the loading screen" );
                // A chunk of open sea has no ground, so no terrain mesh.
                bool hasGround = chunk.blocksInChunk.Any( cell => map.Terrain.Get( cell.x, cell.y ).Solid );
                if( hasGround ) {
                    Assert.NotNull( chunk.topVisual, $"chunk {index} has terrain" );
                }
            }

            // Ground colliders exist under the player (GroundChecker and building rely on the Block layer).
            var hits = Physics.OverlapBox( player.transform.position + Vector3.down * 0.1f, new Vector3( 0.05f, 0.2f, 0.05f ), Quaternion.identity, LayerMask.GetMask( UnityTags.Block ), QueryTriggerInteraction.Collide );
            Assert.Greater( hits.Length, 0, "a Block-layer collider under the player" );

            // Chunks outside the window are inactive.
            int active = 0;
            foreach( Transform child in map.transform ) {
                if( child.name.StartsWith( "Chunk(" ) && child.gameObject.activeSelf ) {
                    active++;
                }
            }
            Assert.AreEqual( map.ChunkManager.LoadedChunks.Count, active );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Home_KeepsTheStartClear_AndNoChunkExceedsItsBudget( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            int budget = map.MapType.perlinNoiseConfig.ResourceChunkBudget;
            Assert.Greater( budget, 0 );

            var start = MapHelper.WorldPositionToBlockIndex( player.transform.position );
            float clear = ResourceField.StartClearRadius;
            var perChunk = new System.Collections.Generic.Dictionary<( int, int ), int>();
            // The map entry (the ship wreck) is placed next to the player on purpose; only planned resources count here.
            string entry = map.MapType.entryObject != null ? map.MapType.entryObject.name : null;
            foreach( var model in map.Models.Values ) {
                if( model.blockObjects == null ) {
                    continue;
                }
                // Camp pieces are placed by the points of interest, not planned as resources, so they do not count against the budget.
                int resources = model.blockObjects.Values.Count( o => o.name != entry && o.name != "Fireplace" && o.name != "Tent" && o.name != "Chest" );
                if( resources == 0 ) {
                    continue;
                }
                float dx = model.gridX - start.x, dz = model.gridY - start.y;
                Assert.Greater( dx * dx + dz * dz, clear * clear, $"cell {model.gridX},{model.gridY} next to the player's start holds objects" );
                var chunk = ( model.gridX / ChunkManager.ChunkSize, model.gridY / ChunkManager.ChunkSize );
                perChunk[ chunk ] = ( perChunk.TryGetValue( chunk, out var n ) ? n : 0 ) + resources;
            }
            Assert.Greater( perChunk.Count, 20, "objects grow all over the island" );
            Assert.That( perChunk.Values, Has.All.LessThanOrEqualTo( budget ), "no chunk holds more resources than the budget" );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Home_HasCamps_WithLootedChests_AwayFromTheStart( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var scope = UnityEngine.Object.FindFirstObjectByType<GameplayScope>();
            var references = scope.Container.Resolve<References>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var start = MapHelper.WorldPositionToBlockIndex( player.transform.position );

            var all = map.Models.Values.Where( m => m.blockObjects != null ).SelectMany( m => m.blockObjects.Values ).ToList();
            var fireplaces = all.Where( o => o.name == "Fireplace" ).ToList();
            var chests = all.Where( o => o.name == "Chest" ).ToList();
            UnityEngine.Debug.Log( $"{Tag} camps: {fireplaces.Count} fireplaces, {all.Count( o => o.name == "Tent" )} tents, {chests.Count} chests" );
            Assert.GreaterOrEqual( fireplaces.Count, 2, "the camps were placed" );
            Assert.AreEqual( fireplaces.Count, chests.Count, "every camp has one chest" );
            Assert.AreEqual( fireplaces.Count, all.Count( o => o.name == "Tent" ), "every camp has a tent" );

            foreach( var fire in fireplaces ) {
                float distance = Vector3.Distance( new Vector3( fire.pos.x, 0, fire.pos.z ), new Vector3( player.transform.position.x, 0, player.transform.position.z ) ) / MapGenerator.TILE_X_OFFSET;
                Assert.GreaterOrEqual( distance, 14f, "a camp is not built on top of the start" );

                // Nothing else grows in the cleared site.
                var nearby = all.Where( o => o != fire && Vector3.Distance( new Vector3( o.pos.x, 0, o.pos.z ), new Vector3( fire.pos.x, 0, fire.pos.z ) ) / MapGenerator.TILE_X_OFFSET <= 2.6f ).Select( o => o.name ).ToList();
                CollectionAssert.AreEquivalent( new[] { "Tent", "Chest" }, nearby, "only the tent and the chest stand next to the fireplace" );
            }

            foreach( var chest in chests ) {
                Assert.IsNotNull( chest.additionalData, "the chest is filled" );
                // The chest keeps its slots as JSON; read the item names out of it.
                var json = chest.additionalData.ToString();
                var names = System.Text.RegularExpressions.Regex.Matches( json, "\"Id\":\\s*\"([^\"]+)\"" );
                var counts = System.Text.RegularExpressions.Regex.Matches( json, "\"Count\":\\s*(\\d+)" );
                Assert.Greater( names.Count, 0, "the chest is not empty" );
                Assert.LessOrEqual( names.Count, 6, "fits the six slots of a chest" );
                Assert.AreEqual( names.Count, counts.Count );
                foreach( System.Text.RegularExpressions.Match name in names ) {
                    Assert.IsTrue( references.Catalog.TryGet( new Hearthglade.Core.Items.ItemId( name.Groups[ 1 ].Value ), out _ ), $"loot item '{name.Groups[ 1 ].Value}' resolves to an item" );
                }
            }

            // Walk into a camp: its pieces spawn like any other scene object and the chest restores the loot it was generated with.
            var camp = fireplaces[ 0 ];
            Teleport( player, new Vector3( camp.pos.x, player.transform.position.y, camp.pos.z ) + new Vector3( 0f, 0.5f, 0f ) );
            var campChunk = ChunkManager.PositionToChunkIndex( player.transform.position );
            await WaitUntil( ( ) => map.ChunkManager.LastVisitedChunk == campChunk, 10f, "streaming reacts to the walk into the camp" );
            await WaitUntil( ( ) => !HasPendingObjects( map ), 30f, "the objects of the camp chunks are spawned" );
            var chestObject = map.GetComponentsInChildren<Hearthglade.Gameplay.Resource.ChestSceneObject>( false )
                .FirstOrDefault( c => Vector3.Distance( c.transform.position, camp.pos ) < 3f );
            Assert.NotNull( chestObject, "the chest of the camp is alive in the scene" );
            Assert.IsTrue( chestObject.CaptureState().ToString().Contains( "\"Id\"" ), "the live chest holds the generated loot" );
            Assert.NotNull( chestObject.SceneObjectModel, "the chest is tied to its model, so its contents are saved back into the map" );
        } );

        private static int CountEmptyCells( Map map ) {
            int empty = 0;
            for( int y = 0; y < map.Terrain.Size; y++ ) {
                for( int x = 0; x < map.Terrain.Size; x++ ) {
                    if( !map.Models.ContainsKey( new SerializableVector2Int( x, y ) ) ) {
                        empty++;
                    }
                }
            }
            return empty;
        }

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator SceneObjects_AreSpawnedOnce_AndNeverDuplicated_WhenWalkingBetweenChunks( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();

            // Objects appear over a few frames (2 ms budget per frame) - not over hundreds of them.
            float start = Time.realtimeSinceStartup;
            await WaitUntil( ( ) => CountSpawnedSceneObjects( map ) > 0 && !HasPendingObjects( map ), 30f, "scene objects of the active chunks spawned" );
            UnityEngine.Debug.Log( $"{Tag} scene objects of the start area ready {Time.realtimeSinceStartup - start:F2}s after the map was ready; {CountSpawnedSceneObjects( map )} objects" );
            Assert.AreEqual( CountSpawnedSceneObjects( map ), CountLiveSceneObjects( map ), "one live object per spawned model" );

            // Walk one chunk to the east and back, twice, fast (the hide of a chunk may not have run yet).
            var home = player.transform.position;
            var chunkWidth = ChunkManager.ChunkSize * MapGenerator.TILE_X_OFFSET;
            for( int lap = 0; lap < 2; lap++ ) {
                Teleport( player, home + new Vector3( chunkWidth, 0, 0 ) );
                await UniTask.DelayFrame( 3 );
                Teleport( player, home );
                await UniTask.DelayFrame( 3 );
            }
            await WaitUntil( ( ) => !HasPendingObjects( map ), 30f, "scene objects settled after walking around" );
            await UniTask.DelayFrame( 5 );

            Assert.AreEqual( CountSpawnedSceneObjects( map ), CountLiveSceneObjects( map ), "no duplicated or leaked scene objects after walking back and forth" );
            foreach( var index in map.ChunkManager.LoadedChunks ) {
                map.ChunkManager.TryGetChunk( index, out var chunk );
                Assert.IsTrue( chunk.chunkGameObject.activeSelf, $"chunk {index} is loaded and must be active" );
            }
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator SavedObject_WithARemovedPrefab_IsDroppedInsteadOfRetriedForever( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var content = UnityEngine.Object.FindFirstObjectByType<GameplayScope>().Container.Resolve<MapGenerator>().Content;
            var cell = MapHelper.WorldPositionToBlockIndex( player.transform.position );
            var model = map.Models[ new SerializableVector2Int( cell.x, cell.y ) ];
            var ghost = new SceneObjectModel( model.WorldPosition, Vector3.zero, Vector3.one, "obsolete_prefab_that_no_longer_exists" );
            model.blockObjects.Add( ghost.GetHashCode(), ghost );
            Assert.IsTrue( BlockContent.HasUnspawnedSceneObjects( model ) );

            LogAssert.Expect( LogType.Warning, new System.Text.RegularExpressions.Regex( "Dropped the saved object 'obsolete_prefab_that_no_longer_exists'" ) );
            content.ShowSceneObjects( model, map.transform, map );

            Assert.IsFalse( model.blockObjects.Values.Any( o => o.name == ghost.name ), "the object of the removed prefab is forgotten" );
            Assert.IsFalse( BlockContent.HasUnspawnedSceneObjects( model ), "nothing is left to retry" );
        } );

        private static bool HasPendingObjects( Map map ) {
            foreach( var index in map.ChunkManager.LoadedChunks ) {
                if( !map.ChunkManager.TryGetChunk( index, out var chunk ) ) {
                    continue;
                }
                foreach( var blockId in chunk.blocksInChunk ) {
                    if( map.Models.TryGetValue( blockId, out var model ) && BlockContent.HasUnspawnedSceneObjects( model ) ) {
                        return true;
                    }
                }
            }
            return false;
        }

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Streaming_LoadsTheNextChunksAndTheirTerrain_WhenThePlayerMoves( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var before = ChunkManager.PositionToChunkIndex( player.transform.position );

            // Walk three chunks along the map diagonal, one chunk at a time like a player does (a teleport over
            // chunks whose colliders are not there yet would make the player fall).
            var chunkWidth = ChunkManager.ChunkSize * MapGenerator.TILE_X_OFFSET;
            SerializableVector2Int after = before;
            for( int step = 0; step < 3; step++ ) {
                Teleport( player, player.transform.position + new Vector3( chunkWidth, 0, chunkWidth ) );
                after = ChunkManager.PositionToChunkIndex( player.transform.position );
                // LastVisitedChunk moves when the streaming window is re-centred (LoadedChunks may already contain
                // the chunk near the map corner, so it says nothing about whether streaming reacted yet).
                await WaitUntil( ( ) => map.ChunkManager.LastVisitedChunk == after, 5f, "streaming reacts within a few frames (not on the 1 s tick)" );
                await WaitUntil( ( ) => map.ChunkManager.LoadedChunks.All( index => {
                    map.ChunkManager.TryGetChunk( index, out var stepChunk );
                    return stepChunk.chunkGameObject.activeSelf;
                } ), 5f, "all chunks of the window activated" );
            }
            Assert.AreNotEqual( before, after );

            await WaitUntil( ( ) => map.ChunkManager.LoadedChunks.All( index => {
                map.ChunkManager.TryGetChunk( index, out var chunk );
                return chunk.visualState == ChunkVisualState.Built;
            } ), 10f, "terrain visuals of the new window built" );

            // Chunks are activated one per frame (colliders are built on activation).
            await WaitUntil( ( ) => map.ChunkManager.LoadedChunks.All( index => {
                map.ChunkManager.TryGetChunk( index, out var chunk );
                return chunk.chunkGameObject.activeSelf;
            } ), 5f, "all chunks of the new window activated" );
            foreach( var index in map.ChunkManager.LoadedChunks ) {
                map.ChunkManager.TryGetChunk( index, out var chunk );
                // A chunk of open sea has no ground, so no terrain mesh.
                if( chunk.blocksInChunk.Any( cell => map.Terrain.Get( cell.x, cell.y ).Solid ) ) {
                    Assert.NotNull( chunk.topVisual, $"chunk {index} has terrain" );
                }
            }
            Assert.IsFalse( map.ChunkManager.LoadedChunks.Contains( before ), $"the old window is gone (before={before} after={after} player chunk now={ChunkManager.PositionToChunkIndex( player.transform.position )} loaded={string.Join( ",", map.ChunkManager.LoadedChunks )})" );
            map.ChunkManager.TryGetChunk( before, out var oldChunk );
            await WaitUntil( ( ) => !oldChunk.chunkGameObject.activeSelf, 5f, "the old chunk is hidden" );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator SaveAndLoad_RestoresTheSameMap( ) => UniTask.ToCoroutine( async ( ) => {
            const string playerName = "playmode-roundtrip";
            var saveDirectory = System.IO.Path.Combine( Application.persistentDataPath, SaveManager.SAVE_FILE_NAME, playerName );
            try {
                var mapManager = await BootNewGame( playerName );
                var map = mapManager.CurrentMap;
                var modelCount = map.Models.Count;
                var objectCount = CountModelObjects( map );
                var signature = TerrainSignature( map );
                var seed = map.Seed;
                var mapId = map.MapId;

                var scope = UnityEngine.Object.FindFirstObjectByType<GameplayScope>();
                scope.Container.Resolve<SaveManager>().SaveGame();
                var contentPath = System.IO.Path.Combine( saveDirectory, SaveManager.CONTENT_FILE_NAME );
                Assert.IsTrue( System.IO.File.Exists( contentPath ), "save content written" );
                UnityEngine.Debug.Log( $"{Tag} save size {new System.IO.FileInfo( contentPath ).Length / 1024} KB" );

                var header = ResourceLoader.LoadSaveContainer().SaveHeader;
                Assert.IsFalse( header.NewGame, "saving marks the game as existing" );
                float start = Time.realtimeSinceStartup;
                var loadedManager = await LoadGame( new SaveHeader( header ) );
                UnityEngine.Debug.Log( $"{Tag} load of the saved game ready after {Time.realtimeSinceStartup - start:F2}s" );
                var loaded = loadedManager.CurrentMap;

                Assert.AreEqual( modelCount, loaded.Models.Count, "same number of blocks" );
                Assert.AreEqual( objectCount, CountModelObjects( loaded ), "same number of scene objects" );
                Assert.AreEqual( signature, TerrainSignature( loaded ), "same terrain" );
                Assert.AreEqual( seed, loaded.Seed, "seed restored" );
                Assert.AreEqual( mapId, loaded.MapId, "map id restored" );

                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                Assert.IsTrue( loaded.ChunkManager.LoadedChunks.Contains( ChunkManager.PositionToChunkIndex( player.transform.position ) ) );
                await WaitUntil( ( ) => !HasPendingObjects( loaded ), 30f, "scene objects of the loaded map spawned" );
                Assert.AreEqual( CountSpawnedSceneObjects( loaded ), CountLiveSceneObjects( loaded ), "one live object per spawned model after loading" );

                // A gathered resource is removed from its block: that must still work after loading (keys are recomputed).
                foreach( var model in loaded.Models.Values ) {
                    foreach( var pair in model.blockObjects ) {
                        Assert.AreEqual( pair.Value.GetHashCode(), pair.Key, $"object {pair.Value.name} of block ({model.gridX},{model.gridY}) is filed under a stale key" );
                    }
                }
                var withObjects = loaded.Models.Values.First( m => m.blockObjects.Count > 0 );
                int before = withObjects.blockObjects.Count;
                Assert.IsTrue( withObjects.RemoveSceneObject( withObjects.blockObjects.Values.First() ) );
                Assert.AreEqual( before - 1, withObjects.blockObjects.Count );
            }
            finally {
                if( System.IO.Directory.Exists( saveDirectory ) ) {
                    System.IO.Directory.Delete( saveDirectory, true );
                }
            }
        } );

        // The saves a player already has were written by the old game (blocks as GameObjects, indented JSON,
        // no seed/counter fields). They must keep loading. Skipped when there is no such save on this machine.
        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator AnExistingSaveFromTheOldFormat_StillLoads( ) => UniTask.ToCoroutine( async ( ) => {
            const string playerName = "name";
            var contentPath = System.IO.Path.Combine( Application.persistentDataPath, SaveManager.SAVE_FILE_NAME, playerName, SaveManager.CONTENT_FILE_NAME );
            if( !System.IO.File.Exists( contentPath ) ) {
                Assert.Ignore( $"no legacy save at {contentPath}" );
            }
            var before = System.IO.File.GetLastWriteTimeUtc( contentPath );

            float start = Time.realtimeSinceStartup;
            var mapManager = await LoadGame( new SaveHeader { PlayerName = playerName, NewGame = false, SaveDate = "" } );
            UnityEngine.Debug.Log( $"{Tag} legacy save ({new System.IO.FileInfo( contentPath ).Length / 1024} KB) ready after {Time.realtimeSinceStartup - start:F2}s" );
            var map = mapManager.CurrentMap;

            Assert.Greater( map.Models.Count, 10000, "the map came back with its blocks" );
            Assert.Greater( CountModelObjects( map ), 0, "and its scene objects" );
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            // The player's position comes from the save after the map was loaded, so the streaming window follows it a few frames later.
            await WaitUntil( ( ) => map.ChunkManager.LoadedChunks.Contains( ChunkManager.PositionToChunkIndex( player.transform.position ) )
                && map.ChunkManager.LoadedChunks.All( index => map.ChunkManager.TryGetChunk( index, out var chunk ) && chunk.visualState == ChunkVisualState.Built ),
                20f, "the chunks around the player loaded and built" );
            Assert.AreEqual( before, System.IO.File.GetLastWriteTimeUtc( contentPath ), "loading must not modify the save" );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator NewGame_Home_IsOneIsland_AndStartsOnTheWestCoast( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            int edge = map.Terrain.Size;

            // One island: all ground is one connected piece and a sea margin surrounds it.
            int ground = 0, minX = edge, maxX = 0, minY = edge, maxY = 0, startX = -1, startY = -1;
            for( int y = 0; y < edge; y++ ) {
                for( int x = 0; x < edge; x++ ) {
                    var cell = map.Terrain.Get( x, y );
                    if( !cell.Ground ) { continue; }
                    ground++;
                    minX = Math.Min( minX, x ); maxX = Math.Max( maxX, x );
                    minY = Math.Min( minY, y ); maxY = Math.Max( maxY, y );
                    if( startX < 0 ) { startX = x; startY = y; }
                }
            }
            Assert.Greater( ground, edge * edge / 4, "the island is roomy" );
            Assert.GreaterOrEqual( Math.Min( Math.Min( minX, minY ), Math.Min( edge - 1 - maxX, edge - 1 - maxY ) ), 3, "sea around the island" );
            Assert.AreEqual( ground, GridFlood.CountReachable( startX, startY, ( x, y ) => map.Terrain.Get( x, y ).Ground, edge * edge ), "one piece of land" );

            // The player starts on the west coast, in the middle of the map.
            var playerCell = MapHelper.WorldPositionToBlockIndex( player.transform.position );
            Assert.Less( playerCell.x, edge / 3, "the start is on the west side" );
            Assert.That( playerCell.y, Is.InRange( edge / 4, edge * 3 / 4 ), "the start is around the middle" );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator NewGame_Home_HasTheWholeBiomeSet_AndPatchesOfBareGround( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var looks = new System.Collections.Generic.HashSet<int>();
            int patched = 0, ground = 0;
            foreach( var model in map.Models.Values ) {
                var cell = map.Terrain.Get( model.gridX, model.gridY );
                if( !cell.Ground ) { continue; }
                ground++;
                looks.Add( cell.Biome );
                patched += model.patch != 0 ? 1 : 0;
            }
            foreach( var look in new[] { TerrainBiome.Grass, TerrainBiome.Dirt, TerrainBiome.Sand, TerrainBiome.Stone, TerrainBiome.Taiga, TerrainBiome.Forest, TerrainBiome.Swamp } ) {
                Assert.IsTrue( looks.Contains( look ), $"no ground with the look {look} on Home" );
            }
            Assert.That( patched / ( double ) ground, Is.InRange( 0.03, 0.4 ), "cosmetic patches on part of the ground" );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Entities_SpawnOnlyWhereTheirBiomeListsThem_AndWithinTheirLimits( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;

            // No frame passes while ticking, so nothing wanders off: every entity stands where it was spawned.
            for( int i = 0; i < 400; i++ ) {
                map.EntitySpawner.Tick( true );
            }

            var entities = map.GetComponentsInChildren<Entity>( true );
            Assert.Greater( entities.Length, 0, "the entity manager spawned nothing in 400 ticks" );
            var perKind = new System.Collections.Generic.Dictionary<string, int>();
            foreach( var entity in entities ) {
                var cell = MapHelper.WorldPositionToBlockIndex( entity.transform.position );
                var biome = map.BiomeAt( cell );
                Assert.NotNull( biome, $"{entity.name} stands at {cell}, which belongs to no biome" );
                var data = biome.Entities.FirstOrDefault( d => entity.name.StartsWith( d.prefab.name ) );
                Assert.NotNull( data, $"{entity.name} at {cell} is not in the entity table of {biome.name}" );

                // Water entities float on a cell without ground, the animals stand on free ground.
                if( map.Terrain.Get( cell.x, cell.y ).Ground ) {
                    Assert.IsTrue( map.IsFreeGround( entity.transform.position ), $"{entity.name} at {cell} is not on free ground" );
                }
                perKind[ data.prefab.name ] = perKind.TryGetValue( data.prefab.name, out var count ) ? count + 1 : 1;
            }

            foreach( var pair in perKind ) {
                int allowed = map.MapType.perlinNoiseConfig.Biomes.SelectMany( b => b.Entities ).Where( d => d.prefab.name == pair.Key ).Sum( d => d.maxAlive );
                Assert.LessOrEqual( pair.Value, allowed, $"more {pair.Key} than the biomes allow" );
            }
            UnityEngine.Debug.Log( $"{Tag} entities after 400 ticks: " + string.Join( ", ", perKind.Select( p => $"{p.Key} x{p.Value}" ) ) );
        } );

        private static bool IsAlive( Map map, Entity entity ) {
            // Entities wait inactive until the player comes close; a pooled one is moved out of the map.
            return entity != null && entity.transform.parent == map.transform;
        }

        // The swarms of the night-only entities. Other biomes reuse FireflySwarm for their daytime pollen motes and mist, which are not fireflies.
        private static FireflySwarm[] Fireflies( Map map ) {
            var names = map.MapType.perlinNoiseConfig.Biomes.SelectMany( b => b.Entities )
                .Where( d => d.prefab is FireflySwarm && d.time == SpawnTime.Night ).Select( d => d.prefab.name ).ToHashSet();
            return map.GetComponentsInChildren<FireflySwarm>( true ).Where( e => IsAlive( map, e ) && names.Any( n => e.name.StartsWith( n ) ) ).ToArray();
        }

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Ambient_ButterfliesComeByDay_FirefliesByNight_AndLeaveWhenTheirTimeIsOver( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;

            for( int i = 0; i < 400; i++ ) {
                map.EntitySpawner.Tick( true );
            }
            var butterflies = map.GetComponentsInChildren<AmbientFlyer>( true ).Where( e => IsAlive( map, e ) ).ToArray();
            Assert.IsEmpty( Fireflies( map ), "fireflies by day" );
            UnityEngine.Debug.Log( $"{Tag} by day: {butterflies.Length} butterflies" );
            Assert.Greater( butterflies.Length, 0, "no butterflies in the meadow by day" );

            // Dusk: the butterflies are gone at once, the fireflies come.
            map.EntitySpawner.Tick( false );
            Assert.IsEmpty( butterflies.Where( e => IsAlive( map, e ) ), "butterflies stayed after dusk" );
            for( int i = 0; i < 400; i++ ) {
                map.EntitySpawner.Tick( false );
            }
            var fireflies = Fireflies( map );
            Assert.IsEmpty( map.GetComponentsInChildren<AmbientFlyer>( true ).Where( e => IsAlive( map, e ) ), "butterflies by night" );
            UnityEngine.Debug.Log( $"{Tag} by night: {fireflies.Length} firefly swarms" );
            Assert.Greater( fireflies.Length, 0, "no fireflies by night" );

            // Dawn: the fireflies fade out (a couple of seconds), then they are gone too.
            map.EntitySpawner.Tick( true );
            await UniTask.Delay( 2600 );
            Assert.IsEmpty( fireflies.Where( e => IsAlive( map, e ) ), "fireflies stayed after dawn" );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Deer_StandsUpright_IdlesAndRunsFromThePlayer( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();

            var deerPrefab = map.MapType.perlinNoiseConfig.Biomes.SelectMany( b => b.Entities ).Select( d => d.prefab ).OfType<Deer>().FirstOrDefault();
            Assert.NotNull( deerPrefab, "no biome lists a deer" );

            // Half a metre east of the start: inland, and nearer than the deer's alert distance.
            var position = player.transform.position + Vector3.right * 0.5f;
            Assert.IsTrue( map.IsFreeGround( position ), "the spot next to the start is not free ground" );
            var deer = UnityEngine.Object.Instantiate( deerPrefab, position, Quaternion.identity, map.transform );
            var animancer = deer.GetComponent<Animancer.AnimancerComponent>();
            deer.Construct( map, player.transform );
            Assert.AreEqual( "Deer_Idle", animancer.States.Current.Clip.name, "a rested deer plays the idle clip" );

            await UniTask.Delay( 1500 );

            var away = deer.transform.position - player.transform.position;
            away.y = 0f;
            Assert.Greater( away.magnitude, 1.1f, "the deer did not run away from the player" );
            Assert.AreEqual( "Deer_Run", animancer.States.Current.Clip.name, "a frightened deer plays the run clip" );
            Assert.Greater( Vector3.Dot( deer.transform.up, Vector3.up ), 0.99f, "the deer is knocked over" );
            Assert.Greater( Vector3.Dot( deer.transform.forward, away.normalized ), 0.5f, "the deer runs backwards" );
            Assert.AreEqual( position.y, deer.transform.position.y, 0.001f, "the deer sank or floated" );
            UnityEngine.Object.Destroy( deer.gameObject );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Hen_StandsUpright_IdlesAndIsNotScaredByThePlayer( ) => UniTask.ToCoroutine( async ( ) => {
            var mapManager = await BootNewGame();
            var map = mapManager.CurrentMap;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();

            var henPrefab = map.MapType.perlinNoiseConfig.Biomes.SelectMany( b => b.Entities ).Select( d => d.prefab ).OfType<Hen>().FirstOrDefault();
            Assert.NotNull( henPrefab, "no biome lists a hen" );

            var position = player.transform.position + Vector3.right * 0.3f;
            Assert.IsTrue( map.IsFreeGround( position ), "the spot next to the start is not free ground" );
            var hen = UnityEngine.Object.Instantiate( henPrefab, position, Quaternion.identity, map.transform );
            var animancer = hen.GetComponent<Animancer.AnimancerComponent>();
            hen.Construct( map, player.transform );
            Assert.AreEqual( "Hen_Idle", animancer.States.Current.Clip.name );

            await UniTask.Delay( 700 );

            Assert.AreEqual( "Hen_Idle", animancer.States.Current.Clip.name, "a hen next to the player is not frightened" );
            Assert.Greater( Vector3.Dot( hen.transform.up, Vector3.up ), 0.99f, "the hen is knocked over" );
            Assert.Less( ( hen.transform.position - position ).magnitude, 0.05f, "the hen ran away" );
            UnityEngine.Object.Destroy( hen.gameObject );
        } );
    }
}
