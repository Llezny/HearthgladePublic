using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Map.Visual;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Map {

    /// <summary>
    /// Unity side of chunk streaming. Which chunks should be active is decided by the pure
    /// <see cref="ChunkStreamingPlanner"/>; this class only owns the GameObjects and the async work.
    /// </summary>
    public class ChunkManager {

        public const int ChunkSize = 13;

        // Chunks within this distance of the player's chunk are active (1 = the 3x3 around the player).
        private const int ActiveRadius = 1;

        // Terrain visuals are computed in the background for chunks up to this distance from the player (the
        // active ring plus one more), so a chunk that becomes active already has its ground; visuals of chunks
        // beyond VisualKeepRadius are destroyed again (they are cheap to recompute).
        private const int PrebuildRadius = ActiveRadius + 1;
        private const int VisualKeepRadius = ActiveRadius + 2;

        // Scene objects (trees, rocks, ...) are instantiated on the main thread, so they are spread over
        // frames by time, not by count: most blocks have nothing to spawn, a few have several objects.
        private const double SceneObjectFrameBudgetMs = 2.0;

        public int MinChunkIndex => 0;
        public int MaxChunkIndex => targetMap.MapType.SizeInChunks - 1;


        public SerializableVector2Int LastVisitedChunk { get; private set; }
        public HashSet<SerializableVector2Int> LoadedChunks { get; private set; }

        private readonly Dictionary<SerializableVector2Int, Chunk> chunksDictionary;
        private readonly Map targetMap;
        private readonly PlayerController playerController;
        private readonly BlockContent content;
        private readonly ChunkStreamingPlanner planner;
        private readonly Dictionary<SerializableVector2Int, UniTask> visualTasks = new();
        private readonly List<ChunkCoord> ringBuffer = new();

        private readonly Queue<( SerializableVector2Int cell, SerializableVector2Int chunkId )> blocksToShowQueue;
        private bool isShowingBlocks;
        private readonly Queue<( SerializableVector2Int cell, SerializableVector2Int chunkId )> blocksToHideQueue = new();
        private bool isHidingBlocks;

        public ChunkManager( Map targetMap, PlayerController playerController, BlockContent content ) {
            this.targetMap = targetMap;
            this.content = content;
            this.chunksDictionary = new Dictionary<SerializableVector2Int, Chunk>();
            this.LoadedChunks = new HashSet<SerializableVector2Int>();
            this.playerController = playerController;
            this.planner = new ChunkStreamingPlanner( targetMap.MapType.SizeInChunks, ActiveRadius );
            this.LastVisitedChunk = PositionToChunkIndex( playerController.transform.position );
            this.blocksToShowQueue = new Queue<( SerializableVector2Int, SerializableVector2Int )>();
        }

        // Async work below stops as soon as the owning map is destroyed.
        private CancellationToken LifetimeToken => targetMap.GetCancellationTokenOnDestroy();

        /// <summary>While set, the chunks stay as they are however far the player is (inside a house, far above the island).</summary>
        public bool StreamingPaused { get; set; }

        public void Tick() {
            if ( StreamingPaused ) {
                return;
            }
            UpdateEnabledChunks();
        }

        private void UpdateEnabledChunks() {
            if( !planner.Update( ToCoord( PositionToChunkIndex( playerController.transform.position ) ) ) ) {
                return;
            }
            SyncLoadedChunks();

            // The planner reuses its lists, the async tasks below outlive this call.
            var chunksToShow = new List<SerializableVector2Int>( planner.ToShow.Count );
            foreach( var chunk in planner.ToShow ) {
                chunksToShow.Add( ToIndex( chunk ) );
            }
            var chunksToHide = new List<SerializableVector2Int>( planner.ToHide.Count );
            foreach( var chunk in planner.ToHide ) {
                chunksToHide.Add( ToIndex( chunk ) );
            }

            var ct = LifetimeToken;
            ShowChunksAsync( chunksToShow, ct ).Forget( );
            HideChunksAsync( chunksToHide, ct ).Forget( );
            PrebuildAround( planner.Center );
        }

        private void SyncLoadedChunks() {
            LastVisitedChunk = ToIndex( planner.Center );
            var loaded = new HashSet<SerializableVector2Int>();
            foreach( var chunk in planner.Loaded ) {
                loaded.Add( ToIndex( chunk ) );
            }
            LoadedChunks = loaded;
        }

        // The chunk a world position is in, decided by the block grid: a block always lands in the same
        // chunk as the position standing on it (no float division of positions by the chunk width).
        public static SerializableVector2Int PositionToChunkIndex( Vector3 position ) {
            var block = MapHelper.WorldPositionToBlockIndex( position );
            return ToIndex( ChunkMath.ChunkOf( block.x, block.y, ChunkSize ) );
        }

        public static SerializableVector2Int ChunkOfBlock( int gridX, int gridY ) {
            return ToIndex( ChunkMath.ChunkOf( gridX, gridY, ChunkSize ) );
        }

        private static ChunkCoord ToCoord( SerializableVector2Int index ) => new ChunkCoord( index.x, index.y );
        private static SerializableVector2Int ToIndex( ChunkCoord coord ) => new SerializableVector2Int( coord.X, coord.Y );

        // Returns true if chunk existed
        public bool GetOrAddChunk( SerializableVector2Int chunkIndex, out Chunk chunk ) {
            if( TryGetChunk( chunkIndex, out chunk  ) ) {
                return true;
            }
            CreateChunk( chunkIndex, out chunk );
            return false;
        }

        private void CreateChunk( SerializableVector2Int chunkIndex, out Chunk chunk ) {
            chunk = new Chunk {
                chunkGameObject = new GameObject( $"Chunk({chunkIndex})"),
                chunkId = chunkIndex
            };
            chunk.chunkGameObject.transform.SetParent( targetMap.transform );
            chunksDictionary.Add( chunkIndex, chunk );
        }

        // Returns null if chunk not found
        public bool TryGetChunk( SerializableVector2Int chunkIndex, out Chunk chunk ) {
            return chunksDictionary.TryGetValue( chunkIndex, out chunk  );
        }

        /// <summary>Registers a block (data only) with the chunk it belongs to.</summary>
        public void AddBlock( SerializableVector2Int cell ) {
            GetOrAddChunk( ChunkOfBlock( cell.x, cell.y ), out Chunk chunk );
            chunk.blocksInChunk.Add( cell );
        }

        public void RemoveBlock( SerializableVector2Int cell ) {
            if ( TryGetChunk( ChunkOfBlock( cell.x, cell.y ), out var chunk ) ) {
                chunk.blocksInChunk.Remove( cell );
            }
        }

        /// <summary>The object scene objects of that block are parented to (its chunk), or null.</summary>
        public Transform GetChunkTransform( SerializableVector2Int cell ) {
            return TryGetChunk( ChunkOfBlock( cell.x, cell.y ), out var chunk ) ? chunk.chunkGameObject.transform : null;
        }

        /// <summary>
        /// Call after a block was added, replaced or removed at runtime: drops the chunk's colliders and the
        /// block's own instance so they are created again from the new data (at once if the chunk is active).
        /// </summary>
        public void OnBlockChanged( SerializableVector2Int cell ) {
            if ( !TryGetChunk( ChunkOfBlock( cell.x, cell.y ), out var chunk ) ) {
                return;
            }
            if ( chunk.blockVisuals.Remove( cell, out var visual ) && visual != null ) {
                Lean.Pool.LeanPool.Despawn( visual );
            }
            DestroyColliders( chunk );
            if ( chunk.chunkGameObject.activeSelf ) {
                EnsureColliders( chunk );
                EnqueueBlocksToShow( chunk );
            }
        }

        public void SpawnChunk( SerializableVector2Int chunkIndex ) {
            if ( !TryGetChunk( chunkIndex, out var chunk ) ) {
                return;
            }
            ActivateChunk( chunk );
            // Colliders exist right away; the ground mesh follows when the background build finishes
            // (normally it was pre-built while the chunk was still outside the active ring).
            EnsureVisualAsync( chunk ).Forget( );
        }

        // Switches a chunk on and creates what has to exist while it is active: the ground colliders at once
        // (the player must never stand on a chunk without them), everything else through the paced queue.
        private void ActivateChunk( Chunk chunk ) {
            chunk.chunkGameObject.SetActive( true );
            EnsureColliders( chunk );
            EnqueueBlocksToShow( chunk );
        }

        private void EnsureColliders( Chunk chunk ) {
            if ( chunk.collidersObject == null ) {
                chunk.collidersObject = content.BuildColliders( chunk, targetMap );
            }
        }

        private static void DestroyColliders( Chunk chunk ) {
            if ( chunk.collidersObject != null ) {
                UnityEngine.Object.Destroy( chunk.collidersObject );
            }
            chunk.collidersObject = null;
        }

        // Runtime content of a chunk that is far from the player: colliders and the prefabs of blocks that are
        // visible themselves. Cheap to create again when the chunk comes back.
        private void DiscardContent( Chunk chunk ) {
            DestroyColliders( chunk );
            foreach ( var visual in chunk.blockVisuals.Values ) {
                if ( visual != null ) {
                    Lean.Pool.LeanPool.Despawn( visual );
                }
            }
            chunk.blockVisuals.Clear();
        }

        // ---- terrain visuals ----------------------------------------------------------------------------

        private static bool IsVisualEnabled( TerrainVisualSO config ) {
            return config != null && config.enableVisual && config.terrainMaterial != null;
        }

        /// <summary>Completes when the chunk's terrain visuals exist (starting their background build if needed).</summary>
        public UniTask EnsureVisualAsync( Chunk chunk ) {
            if ( chunk.visualState == ChunkVisualState.Built ) {
                return UniTask.CompletedTask;
            }
            if ( chunk.visualState == ChunkVisualState.Building && visualTasks.TryGetValue( chunk.chunkId, out var running ) ) {
                return running;
            }
            // Several callers can wait for the same build (the streaming window, a chunk being spawned), and a
            // Preserve()d task only lets the first of them register: the completion source has no such limit.
            var source = new UniTaskCompletionSource();
            visualTasks[ chunk.chunkId ] = source.Task;
            RunVisualBuild( chunk, source ).Forget( );
            return source.Task;
        }

        private async UniTaskVoid RunVisualBuild( Chunk chunk, UniTaskCompletionSource source ) {
            try {
                await BuildVisualAsync( chunk, LifetimeToken );
                source.TrySetResult();
            }
            catch ( System.OperationCanceledException ) {
                source.TrySetCanceled();
            }
            catch ( System.Exception exception ) {
                source.TrySetException( exception );
            }
        }

        private async UniTask BuildVisualAsync( Chunk chunk, CancellationToken ct ) {
            var config = TerrainVisualSO.Load();
            if ( !IsVisualEnabled( config ) ) {
                chunk.visualState = ChunkVisualState.Built;
                return;
            }

            chunk.visualState = ChunkVisualState.Building;
            try {
                var parameters = ChunkVisualPipeline.CreateParams( config );
                int chunkX = chunk.chunkId.x;
                int chunkY = chunk.chunkId.y;
                while ( true ) {
                    int version = chunk.visualVersion;
                    // Snapshot on the main thread; the worker never touches the live grid or any Unity object.
                    int margin = ChunkVisualComputer.WindowMargin( parameters );
                    var window = targetMap.Terrain.CopyWindow(
                        ChunkVisualComputer.WindowOrigin( chunkX, ChunkSize, margin ),
                        ChunkVisualComputer.WindowOrigin( chunkY, ChunkSize, margin ),
                        ChunkVisualComputer.WindowSize( ChunkSize, margin ) );
                    var arrays = await UniTask.RunOnThreadPool(
                        () => ChunkVisualPipeline.Compute( window, chunkX, chunkY, parameters ),
                        cancellationToken: ct );

                    if ( chunk.chunkGameObject == null ) {
                        return; // the map was destroyed meanwhile
                    }
                    if ( version != chunk.visualVersion ) {
                        continue; // the terrain changed while computing: compute again from the new state
                    }
                    ChunkVisualPipeline.Apply( chunk, arrays, config );
                    chunk.visualState = ChunkVisualState.Built;
                    return;
                }
            }
            catch {
                chunk.visualState = ChunkVisualState.NotBuilt;
                throw;
            }
            finally {
                visualTasks.Remove( chunk.chunkId );
            }
        }

        // Starts background builds for the ring around the player and frees visuals that are far away.
        private void PrebuildAround( ChunkCoord center ) {
            planner.GetRing( center, PrebuildRadius, ringBuffer );
            foreach ( var coord in ringBuffer ) {
                if ( TryGetChunk( ToIndex( coord ), out var chunk ) && chunk.visualState == ChunkVisualState.NotBuilt ) {
                    EnsureVisualAsync( chunk ).Forget( );
                }
            }
            foreach ( var chunk in chunksDictionary.Values ) {
                if ( ChunkMath.Distance( ToCoord( chunk.chunkId ), center ) <= VisualKeepRadius ) {
                    continue;
                }
                if ( chunk.visualState == ChunkVisualState.Built ) {
                    ChunkVisualPipeline.Discard( chunk );
                    chunk.visualState = ChunkVisualState.NotBuilt;
                }
                DiscardContent( chunk );
            }
        }

        /// <summary>
        /// Call after a block's terrain data changed: the visuals of its chunk and of the chunks next to it
        /// (their border cells look at it) are recomputed.
        /// </summary>
        public void InvalidateVisualsAround( int gridX, int gridY ) {
            var seen = new HashSet<SerializableVector2Int>();
            for ( int dx = -1; dx <= 1; dx++ ) {
                for ( int dy = -1; dy <= 1; dy++ ) {
                    var index = ToIndex( ChunkMath.ChunkOf( gridX + dx, gridY + dy, ChunkSize ) );
                    if ( !seen.Add( index ) || !TryGetChunk( index, out var chunk ) ) {
                        continue;
                    }
                    chunk.visualVersion++;
                    if ( chunk.visualState == ChunkVisualState.Built ) {
                        ChunkVisualPipeline.Discard( chunk );
                        chunk.visualState = ChunkVisualState.NotBuilt;
                    }
                    if ( chunk.visualState == ChunkVisualState.NotBuilt && planner.HasCenter
                         && ChunkMath.Distance( ToCoord( index ), planner.Center ) <= PrebuildRadius ) {
                        EnsureVisualAsync( chunk ).Forget( );
                    }
                }
            }
        }

        private void EnqueueBlocksToShow( Chunk chunk ) {
            foreach ( var cell in chunk.blocksInChunk ) {
                if ( !targetMap.Models.TryGetValue( cell, out var model ) ) {
                    continue;
                }
                bool needsOwnInstance = NeedsOwnInstance( chunk, cell );
                if ( !needsOwnInstance && !BlockContent.HasUnspawnedSceneObjects( model ) ) {
                    continue;
                }
                blocksToShowQueue.Enqueue( ( cell, chunk.chunkId ) );
            }

            if ( !isShowingBlocks && blocksToShowQueue.Count > 0 ) {
                UpdateBlocksAsync( LifetimeToken ).Forget();
            }
        }

        private async UniTask UpdateBlocksAsync( CancellationToken ct ) {
            isShowingBlocks = true;
            try {
                var stopwatch = new Stopwatch();
                while ( blocksToShowQueue.Count > 0 ) {
                    stopwatch.Restart();
                    do {
                        var ( cell, chunkId ) = blocksToShowQueue.Dequeue();
                        // The chunk may have been hidden while its blocks were still waiting in the queue.
                        if ( LoadedChunks.Contains( chunkId ) ) {
                            ShowBlockContent( cell, chunkId );
                        }
                    } while ( blocksToShowQueue.Count > 0 && stopwatch.Elapsed.TotalMilliseconds < SceneObjectFrameBudgetMs );
                    await UniTask.Yield( PlayerLoopTiming.Update, ct );
                }
            }
            finally {
                isShowingBlocks = false;
            }
        }

        // A block that is not ground (water) is not drawn by the terrain mesh: its own prefab is instantiated.
        private bool NeedsOwnInstance( Chunk chunk, SerializableVector2Int cell ) {
            var terrainCell = targetMap.Terrain.Get( cell.x, cell.y );
            return terrainCell.Present && !terrainCell.Ground && !chunk.blockVisuals.ContainsKey( cell );
        }

        private void ShowBlockContent( SerializableVector2Int cell, SerializableVector2Int chunkId ) {
            if ( !TryGetChunk( chunkId, out var chunk ) || !targetMap.Models.TryGetValue( cell, out var model ) ) {
                return;
            }
            if ( NeedsOwnInstance( chunk, cell ) ) {
                var kind = content.Catalog.Get( model.prefabName );
                var instance = kind != null ? content.SpawnBlockVisual( model, kind, chunk.chunkGameObject.transform ) : null;
                if ( instance != null ) {
                    chunk.blockVisuals[ cell ] = instance;
                }
            }
            content.ShowSceneObjects( model, chunk.chunkGameObject.transform, targetMap );
        }

        // Re-centres streaming on the player right now (after a map change or a load): chunks outside the
        // new window are hidden, the chunk under the player comes first. Completes when the ground of every
        // active chunk exists, so callers can keep the loading screen up until then.
        public async UniTask ForceRefreshChunksAsync() {
            var playerChunk = ToCoord( PositionToChunkIndex( playerController.transform.position ) );
            planner.Refresh( playerChunk );
            SyncLoadedChunks();

            blocksToShowQueue.Clear();
            foreach ( var chunk in chunksDictionary.Values ) {
                if ( !planner.Loaded.Contains( ToCoord( chunk.chunkId ) ) ) {
                    HideChunk( chunk );
                }
            }

            var ordered = new List<ChunkCoord>();
            planner.GetRing( playerChunk, ActiveRadius, ordered );
            var builds = new List<UniTask>( ordered.Count );
            foreach ( var chunkCoord in ordered ) {
                if ( !TryGetChunk( ToIndex( chunkCoord ), out var chunk ) ) {
                    continue;
                }
                ActivateChunk( chunk );
                builds.Add( EnsureVisualAsync( chunk ) );
            }
            await UniTask.WhenAll( builds );
            PrebuildAround( playerChunk );
        }

        private void HideChunk( Chunk chunk ) {
            if ( !chunk.chunkGameObject.activeSelf ) {
                return;
            }
            // The chunk goes inactive at once, so its objects stop rendering and simulating this frame. Handing
            // the ~500 pooled objects back (a SetParent and a deactivation each) is spread over frames: doing it
            // here in one go was the spike when crossing a chunk border.
            chunk.chunkGameObject.SetActive( false );
            foreach ( var cell in chunk.blocksInChunk ) {
                if ( targetMap.Models.TryGetValue( cell, out var model ) && BlockContent.HasSpawnedSceneObjects( model ) ) {
                    blocksToHideQueue.Enqueue( ( cell, chunk.chunkId ) );
                }
            }
            if ( !isHidingBlocks && blocksToHideQueue.Count > 0 ) {
                HideBlocksAsync( LifetimeToken ).Forget();
            }
        }

        private async UniTask HideBlocksAsync( CancellationToken ct ) {
            isHidingBlocks = true;
            try {
                var stopwatch = new Stopwatch();
                while ( blocksToHideQueue.Count > 0 ) {
                    stopwatch.Restart();
                    do {
                        var ( cell, chunkId ) = blocksToHideQueue.Dequeue();
                        // The player may have walked back in meanwhile: the chunk is active again and keeps its objects.
                        if ( !LoadedChunks.Contains( chunkId ) && targetMap.Models.TryGetValue( cell, out var model ) ) {
                            content.HideSceneObjects( model );
                        }
                    } while ( blocksToHideQueue.Count > 0 && stopwatch.Elapsed.TotalMilliseconds < SceneObjectFrameBudgetMs );
                    await UniTask.Yield( PlayerLoopTiming.Update, ct );
                }
            }
            finally {
                isHidingBlocks = false;
            }
        }

        private async UniTask HideChunksAsync( List<SerializableVector2Int> chunksToHide, CancellationToken ct ) {
            foreach ( var chunkIndex in chunksToHide ) {
                ct.ThrowIfCancellationRequested();
                // The player may have walked back in while an earlier chunk was being hidden.
                if ( LoadedChunks.Contains( chunkIndex ) || !TryGetChunk( chunkIndex, out var chunk ) ) {
                    continue;
                }
                HideChunk( chunk );
                await UniTask.Yield( PlayerLoopTiming.Update, ct );
            }
        }

        private async UniTask ShowChunksAsync( List<SerializableVector2Int> chunksToShow, CancellationToken ct ) {
            foreach ( var chunkIndex in chunksToShow ) {
                ct.ThrowIfCancellationRequested();
                if ( !LoadedChunks.Contains( chunkIndex ) ) {
                    continue;
                }
                SpawnChunk( chunkIndex );
                await UniTask.Yield( PlayerLoopTiming.Update, ct );
            }
        }
    }
}
