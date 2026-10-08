using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Audio {

    /// <summary>Starts the menu music when the app starts. The game scene replaces it with the music of its map.</summary>
    public sealed class MenuMusicStarter : IStartable {
        private readonly IMusicPlayer music;
        private readonly AudioConfigSO config;

        [ Inject ]
        public MenuMusicStarter( IMusicPlayer music, AudioConfigSO config ) {
            this.music = music;
            this.config = config;
        }

        public void Start( ) {
            music.Play( config.MenuMusic );
        }
    }
}
