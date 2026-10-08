using System;
using Hearthglade.Gameplay.Common.Events;
using Hearthglade.Gameplay.Map;
using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Audio {

    /// <summary>
    /// Lives as long as the game scene: plays the music of the map the player is on (MapSO.Music) whenever a
    /// map is loaded, and puts the menu music back when the player leaves the game for the main menu.
    /// A map without a playlist leaves whatever plays now playing.
    /// </summary>
    public sealed class MapMusicController : IInitializable, IDisposable {
        private readonly IMusicPlayer music;
        private readonly AudioConfigSO config;
        private readonly LoadedMapEvent loadedMapEvent;
        private readonly MapManager mapManager;

        [ Inject ]
        public MapMusicController( IMusicPlayer music, AudioConfigSO config, LoadedMapEvent loadedMapEvent, MapManager mapManager ) {
            this.music = music;
            this.config = config;
            this.loadedMapEvent = loadedMapEvent;
            this.mapManager = mapManager;
        }

        public void Initialize( ) {
            loadedMapEvent.RegisterListener( PlayMusicOf );
            // The map may have been loaded before this controller started.
            if ( mapManager.IsMapReady ) {
                PlayMusicOf( mapManager.CurrentMap );
            }
        }

        public void Dispose( ) {
            loadedMapEvent.UnregisterListener( PlayMusicOf );
            music.Play( config.MenuMusic );
        }

        private void PlayMusicOf( Map.Map map ) {
            music.Play( map.MapType.Music );
        }
    }
}
