using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Audio;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Hearthglade.PlayModeTests {

    /// <summary>
    /// Boots the real Game scene and checks the audio services: they come from the root scope (AppScope, which the
    /// game scene's scope is a child of), the music of the map the player is on starts - loaded through Addressables -
    /// and every sound effect keeps its own volume and pitch.
    /// Run with: Unity -runTests -testPlatform PlayMode (the Editor must be closed).
    /// </summary>
    public class AudioIntegrationTests {

        private static async UniTask<GameplayScope> BootNewGame() {
            var container = ResourceLoader.LoadSaveContainer();
            container.SaveHeader = new SaveHeader { NewGame = true, PlayerName = "audio-test", Seed = 12345, SaveDate = "" };
            await SceneManager.LoadSceneAsync( "Game" );
            var scope = UnityEngine.Object.FindFirstObjectByType<GameplayScope>();
            Assert.NotNull( scope, "GameplayScope not found in the Game scene" );
            var mapManager = scope.Container.Resolve<MapManager>();
            float start = Time.realtimeSinceStartup;
            while( !mapManager.IsMapReady ) {
                if( Time.realtimeSinceStartup - start > 120f ) {
                    Assert.Fail( "Timed out waiting for MapManager.IsMapReady" );
                }
                await UniTask.Yield();
            }
            return scope;
        }

        private static AudioSource[] SourcesOf<T>() where T : Component {
            return UnityEngine.Object.FindFirstObjectByType<T>().GetComponentsInChildren<AudioSource>( true );
        }

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator MusicOfTheMap_Plays_FromTheRootScopeServices( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var music = scope.Container.Resolve<IMusicPlayer>();
            var mapManager = scope.Container.Resolve<MapManager>();

            Assert.AreSame( music, scope.Parent.Container.Resolve<IMusicPlayer>( ), "the music player lives in the root scope, not in the game scene" );
            var playlist = mapManager.CurrentMap.MapType.Music;
            Assert.NotNull( playlist, "the Home map has a playlist" );
            Assert.AreSame( playlist, music.CurrentPlaylist );

            // The song is an Addressable: it is loaded asynchronously, so it takes a moment to be on a source.
            // The menu song that played until now may still be fading out on another source, so look for the map's.
            Assert.Greater( playlist.Tracks.Count, 0 );
            float start = Time.realtimeSinceStartup;
            AudioSource playing = null;
            while( playing == null ) {
                playing = SourcesOf<MusicPlayer>().FirstOrDefault( s => s.clip != null && s.clip.name.StartsWith( "main_island" ) );
                if( Time.realtimeSinceStartup - start > 60f ) {
                    Assert.Fail( "No song of the Home playlist was loaded and started within 60 seconds" );
                }
                await UniTask.Yield();
            }
            Assert.Greater( playing.clip.length, 1f );
            Assert.Less( playing.volume, 0.5f, "the song has just started, so it is still fading in" );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator AskingForThePlaylistThatPlays_ChangesNothing_AndStopSilencesIt( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var music = scope.Container.Resolve<IMusicPlayer>();
            var playlist = music.CurrentPlaylist;
            Assert.NotNull( playlist );

            music.Play( playlist );
            music.Play( null );
            Assert.AreSame( playlist, music.CurrentPlaylist, "the same playlist again, or none, leaves the music as it is" );

            music.Stop( 0f );
            Assert.IsNull( music.CurrentPlaylist );

            // The song that was playing is cut at once; a song of the playlist before it may still be fading out.
            float start = Time.realtimeSinceStartup;
            while( SourcesOf<MusicPlayer>().Any( s => s.isPlaying ) ) {
                if( Time.realtimeSinceStartup - start > 10f ) {
                    Assert.Fail( "Music still plays 10 seconds after Stop" );
                }
                await UniTask.Yield();
            }
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator EverySoundKeepsItsOwnVolumeAndPitch( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var sfx = scope.Container.Resolve<ISfxPlayer>();
            sfx.Volume = 0.5f;

            var quiet = Sound( "quiet", volume: 0.4f, pitch: 0.8f );
            var loud = Sound( "loud", volume: 1f, pitch: 1.5f );
            sfx.Play( quiet );
            sfx.Play( loud );

            var sources = SourcesOf<SfxPlayer>();
            var quietSource = sources.Single( s => s.clip != null && s.clip.name == "quiet" );
            var loudSource = sources.Single( s => s.clip != null && s.clip.name == "loud" );
            Assert.AreEqual( 0.4f * 0.5f, quietSource.volume, 1e-4f, "the sound's volume times the sound effects volume" );
            Assert.AreEqual( 0.8f, quietSource.pitch, 1e-4f );
            Assert.AreEqual( 1f * 0.5f, loudSource.volume, 1e-4f );
            Assert.AreEqual( 1.5f, loudSource.pitch, 1e-4f, "the second sound did not change the pitch of the first" );
            Assert.AreNotSame( quietSource, loudSource );

            sfx.Play( null );
            sfx.Play( ScriptableObject.CreateInstance<AudioSO>() );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator ABurstOfTheSameSound_IsPlayedOnce( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var sfx = scope.Container.Resolve<ISfxPlayer>();
            var sound = Sound( "burst", volume: 1f, pitch: 1f );
            sound.MinInterval = 5f;

            for( int i = 0; i < 4; i++ ) {
                sfx.Play( sound );
            }

            Assert.AreEqual( 1, SourcesOf<SfxPlayer>().Count( s => s.clip != null && s.clip.name == "burst" ) );
        } );

        private static AudioSO Sound( string name, float volume, float pitch ) {
            var sound = ScriptableObject.CreateInstance<AudioSO>();
            sound.Clip = AudioClip.Create( name, 4410, 1, 44100, false );
            sound.Volume = volume;
            sound.Pitch = pitch;
            return sound;
        }
    }
}
