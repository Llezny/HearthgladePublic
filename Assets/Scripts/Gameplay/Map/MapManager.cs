using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Events;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.UI.HUD;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Hearthglade.Gameplay.Map {
    public class MapManager : ISaveable, ITickable, IDisposable {

        public const string HomeMapName = "Home";
        public const float MapTickTime = 1f;
        public const int NotGeneratedMapId = 0;

        public Map HomeMap => MapsDictionary[HomeMapId];
        public int HomeMapId { get; private set; } = NotGeneratedMapId;

        public Map CurrentMap => MapsDictionary[CurrentMapId];
        public int CurrentMapId { get; private set; } = NotGeneratedMapId;

        public Dictionary<int, Map> MapsDictionary { get; private set; } = new( );

        public bool IsMapReady { get; private set; }

        /// <summary>A map was generated for the first time (not loaded from a save).</summary>
        public event Action<Map> MapGenerated;

        /// <summary>The player arrived on a map after a change of maps, once it is ready.</summary>
        public event Action<Map> MapEntered;

        // Number of maps generated so far in this game (saved); it is part of every new map's seed.
        public int MapsGenerated { get; private set; }

        private SaveManager saveManager;
        private MapGenerator mapGenerator;
        private ILoadingScreen loadingScreen;
        private References references;

        private MapEvent loadedMapEvent;
        private MapEvent leftMapEvent;

        private const float TickRateInSeconds = GameConfig.SECONDS_PER_TICK;
        private float timeToTick;
        private readonly ClockManager clockManager;

        [ Inject ]
        public MapManager( MapGenerator mapGenerator, ILoadingScreen loadingScreenService, SaveManager saveManager, References references, ClockManager clockManager ) {
            this.clockManager = clockManager;
            this.mapGenerator = mapGenerator;
            this.loadingScreen = loadingScreenService;
            this.saveManager = saveManager;
            this.references = references;
            saveManager.RegisterISavable(this);
            timeToTick = TickRateInSeconds;
        }

        public void Tick() {
            if ( !IsMapReady ) {
                return;
            }
            // Chunk streaming reacts to the player every frame, not once per tick: with a one second tick
            // the player could be a full second past a chunk border before the next chunks began to load.
            CurrentMap.UpdateChunks();
            if ( timeToTick > 0 ) {
                timeToTick -= Time.deltaTime;
                return;
            }
            timeToTick = TickRateInSeconds;
            CurrentMap.UpdateEntities( clockManager.IsDay );
        }

        private void LoadMapEvents( ) {
            loadedMapEvent = ResourceLoader.LoadAll<LoadedMapEvent>( ResourceLoader.EVENTS_PATH )[0];
            leftMapEvent = ResourceLoader.LoadAll<LeftMapEvent>( ResourceLoader.EVENTS_PATH )[0];
        }

        public void CleanMapEvents( ) {
            leftMapEvent?.RemoveAllListeners( );
            loadedMapEvent?.RemoveAllListeners( );
        }

        // Disposed by the container with its scope. A finalizer used to do this, but finalizers run on a
        // background thread where touching UnityEngine objects is not allowed.
        public void Dispose( ) {
            CleanMapEvents( );
        }

        // Called by RestoreOrCreateMapStep as part of the GameLoadPipeline; no longer hides the
        // loading screen itself (GameLoadCoordinator does that once the whole pipeline is done).
        public async UniTask SetupAsync( IProgress<float> progress, CancellationToken ct = default ) {
            LoadMapEvents( );
            await RestoreOrCreateStateAsync( progress, ct );
            IsMapReady = true;
            loadedMapEvent.Raise( CurrentMap );
            leftMapEvent.RegisterListener( leftMap => UnityEngine.Debug.Log( $"Left {leftMap.GetMapInfo(  )}" ) );
            loadedMapEvent.RegisterListener( loadedMap => UnityEngine.Debug.Log( $"Entered {loadedMap.GetMapInfo(  )}" ) );
        }

        public object CaptureState( ) {
            return new MapManagerSaveData( this );
        }

        public void RestoreState( object state ) {
            RestoreStateAsync( state, null ).Forget( );
        }

        private async UniTask RestoreStateAsync( object state, IProgress<float> progress, CancellationToken ct = default ) {
            // The save file is parsed once into JTokens; converting the token directly avoids
            // re-serializing and re-parsing the (multi-megabyte) map state through a string.
            var data = state is JToken token
                ? token.ToObject<MapManagerSaveData>( )
                : JsonConvert.DeserializeObject<MapManagerSaveData>( state.ToString( ) );
            this.CurrentMapId = data.CurrentMapId;
            this.HomeMapId = data.HomeMapId;
            // Saves that predate the counter have 0 in it; they still hold at least these many maps.
            this.MapsGenerated = Math.Max( data.MapsGenerated, data.MapModels.Count );

            int totalMaps = data.MapModels.Count;
            int loadedMaps = 0;
            // Every map is loaded through its own await, so they interleave instead of the previous
            // map's chunk streaming/Addressables work finishing before the next one even starts.
            await UniTask.WhenAll( data.MapModels.Select( async map => {
                await mapGenerator.LoadMapAsync(
                    map,
                    map.mapId == data.CurrentMapId,
                    m => MapsDictionary.Add( map.mapId, m ),
                    ct );
                loadedMaps++;
                progress?.Report( totalMaps == 0 ? 1f : ( float ) loadedMaps / totalMaps );
            } ) );
        }

        private async UniTask RestoreOrCreateStateAsync( IProgress<float> progress, CancellationToken ct ) {
            if( saveManager.TryGetState<MapManager>( out var gameState) ) {
                await RestoreStateAsync( gameState, progress, ct );
            }
            else {
                await GenerateHomeMapAsync( ct );
                CurrentMap.SetPlayerAndEntryPosition( NotGeneratedMapId );
                await CurrentMap.ChunkManager.ForceRefreshChunksAsync();
                progress?.Report( 1f );
            }
        }

        private async UniTask GenerateHomeMapAsync( CancellationToken ct ) {
            await mapGenerator.GenerateNewMapAsync( HomeMapName, MapsGenerated++, newMap => {
                this.CurrentMapId = newMap.MapId;
                this.HomeMapId = newMap.MapId;
                this.MapsDictionary.Add( newMap.MapId, newMap );
            }, ct );
        }


        private void DestroyMap( Map mapToDestroy ) {
            var mapHolder = mapToDestroy.gameObject;
            MapsDictionary.Remove( mapToDestroy.MapId );
            Object.Destroy( mapHolder );
            leftMapEvent.UnregisterListener( DestroyMap );
        }

        public void ChangeMapToHome( MapEntryBase mapEntryBase ) {
            mapEntryBase.DestinationMapId = HomeMapId;
            ChangeMap( mapEntryBase );
        }

        public void ChangeMap( MapEntryBase mapEntryBase ) {
            DestroyOldMapOnLoadIfNeeded( mapEntryBase, MapsDictionary[CurrentMapId] );
            loadingScreen.LoadAsync( ChangeMapAsync ).Forget( );

            async UniTask ChangeMapAsync( ) {
                IsMapReady = false;
                var oldMapId = CurrentMap.MapId;
                CurrentMap.gameObject.SetActive(false);
                CurrentMap.EntryPosAtPreviousMap = Player.Controller.Player.instance.transform.position; // TODO remove this in the future
                CurrentMap.HasEntryAtPreviousMap = true;
                if ( mapEntryBase.DestinationMapId == 0 ) {
                    await GoToNewMapAsync( mapEntryBase );
                }
                else {
                    await GoToExistingMapAsync( mapEntryBase );
                }
                leftMapEvent.Raise( MapsDictionary[oldMapId] );
                loadedMapEvent.Raise( CurrentMap );
                IsMapReady = true;
                MapEntered?.Invoke( CurrentMap );
            }
        }

        private void DestroyOldMapOnLoadIfNeeded( MapEntryBase mapEntryBase, Map mapToDestroy ) {
            if ( mapEntryBase is Ship && !mapToDestroy.MapType.KeepsAfterLeaving ) {
                leftMapEvent.RegisterListener( DestroyMap );
            }
        }

        private async UniTask GoToNewMapAsync( MapEntryBase mapEntry ) {

            var oldMap = CurrentMap;
            var oldMapId = oldMap.MapId;
            var oldMapIsHome = oldMap.MapType.IsHome;

            // A persistent map (a port) is the same island whenever and however it is first reached: its seed has a fixed ordinal.
            bool isPersistent = mapGenerator.TryGetMapType( mapEntry.DestinationMapType, out var destinationType ) && destinationType.IsPersistent;
            int ordinal = isPersistent ? 0 : MapsGenerated++;

            Map newMap = null;
            await mapGenerator.GenerateNewMapAsync( mapEntry.DestinationMapType, ordinal, m => {
                newMap = m;
                CurrentMapId = m.MapId;
                MapsDictionary.Add( m.MapId, m );
            }, default, mapEntry.DestinationTrip );
            if( newMap != null ) {
                MapGenerated?.Invoke( newMap );
            }

            if ( !oldMapIsHome ) {
                mapEntry.DestinationMapId = newMap.MapId;
            }

            //Todo make this more efficiently
            oldMap.TryOverrideAdditionalDataOfSceneObject( mapEntry.gameObject , new SceneObjectModel( mapEntry.transform ).additionalData );

            CurrentMap.SetPlayerAndEntryPosition( oldMapId );
            await CurrentMap.ChunkManager.ForceRefreshChunksAsync();

        }

        private async UniTask GoToExistingMapAsync( MapEntryBase mapEntry ) {
            CurrentMapId = mapEntry.DestinationMapId;
            CurrentMap.SetPlayerAndEntryPosition( CurrentMapId, setEntryPosition:false );
            CurrentMap.gameObject.SetActive( true );
            await CurrentMap.ChunkManager.ForceRefreshChunksAsync();
        }

        // Turns a block into a "Wooden" one. The block is found by the grid position of the object that was
        // picked (its name is "(x,y)" - this used to parse it as "x,y" and throw).
        public void ChangeBiomeOfBlock( GameObject blockObject ){
            if( blockObject == null || !references.TryGetGameObject( "Wooden", out var blockPrefab ) ) {
                return;
            }
            mapGenerator.ReplaceBlock( CurrentMap, MapHelper.WorldPositionToBlockIndex( blockObject.transform.position ), blockPrefab );
        }
    }
}
