using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Hearthglade.PlayModeTests {

    /// <summary>
    /// Boots the real Game scene and checks the murmur (the speech bubble over the player's head): it appears with its texts,
    /// follows the player on screen, is replaced by the next murmur and goes away. Also checks that a scenery tree refuses to be
    /// gathered with a message to murmur.
    /// Run with: Unity -runTests -testPlatform PlayMode (the Editor must be closed).
    /// </summary>
    public class MurmurIntegrationTests {

        private static async UniTask<GameplayScope> BootNewGame() {
            var container = ResourceLoader.LoadSaveContainer();
            container.SaveHeader = new SaveHeader { NewGame = true, PlayerName = "murmur-test", Seed = 12345, SaveDate = "" };
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

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Murmur_ShowsItsTexts_FollowsThePlayer_IsReplaced_AndHides( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var murmurService = scope.Container.Resolve<MurmurService>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();

            murmurService.Show( "The door is locked", "Requires: Rusty Key" );
            await UniTask.Yield();
            await UniTask.Yield();
            var bubble = UnityEngine.Object.FindFirstObjectByType<MurmurBubble>( FindObjectsInactive.Include );
            Assert.NotNull( bubble, "the bubble is spawned on the first murmur" );
            Assert.IsTrue( bubble.IsShown );
            Assert.IsTrue( bubble.gameObject.activeInHierarchy );
            Assert.AreEqual( "The door is locked", bubble.TitleText );
            Assert.AreEqual( "Requires: Rusty Key", bubble.SubtitleText );

            // The tail's tip is above the player: same screen x, higher on the screen.
            var canvas = bubble.GetComponentInParent<Canvas>();
            Vector2 tip = RectTransformUtility.WorldToScreenPoint( MurmurBubble.UiCamera( canvas ), bubble.transform.position );
            Vector3 feet = Camera.main.WorldToScreenPoint( player.transform.position );
            Assert.AreEqual( feet.x, tip.x, 2f, "the bubble is over the player" );
            Assert.Greater( tip.y, feet.y, "the bubble is above the player's feet" );

            // The camera keeps the player in the middle of the screen, so to see the bubble really follow, point it at a spot off to the side.
            var landmark = new GameObject( "landmark" );
            try {
                landmark.transform.position = player.transform.position + new Vector3( 1f, 0f, -1f );
                bubble.Show( "Over there", null, 5f, landmark.transform, Camera.main );
                await UniTask.Yield();
                await UniTask.Yield();
                Vector2 sideTip = RectTransformUtility.WorldToScreenPoint( MurmurBubble.UiCamera( canvas ), bubble.transform.position );
                Vector3 landmarkOnScreen = Camera.main.WorldToScreenPoint( landmark.transform.position );
                Assert.Greater( Mathf.Abs( landmarkOnScreen.x - feet.x ), 20f, "the landmark is off to the side" );
                Assert.AreEqual( landmarkOnScreen.x, sideTip.x, 2f, "the bubble follows what it is pointed at" );
            }
            finally {
                UnityEngine.Object.Destroy( landmark );
            }

            murmurService.Show( "Inventory full" );
            Assert.AreSame( bubble, UnityEngine.Object.FindFirstObjectByType<MurmurBubble>( FindObjectsInactive.Include ), "there is one bubble" );
            Assert.AreEqual( "Inventory full", bubble.TitleText );
            Assert.IsNull( bubble.SubtitleText, "no subtitle, no second line" );

            murmurService.Hide();
            Assert.IsFalse( bubble.IsShown );
        } );

        [ Test ]
        public void SceneryResource_RefusesToBeGathered_WithAMessage( ) {
            var go = new GameObject( "scenery tree" );
            try {
                var resource = go.AddComponent<Hearthglade.Gameplay.Resource.Resource>();
                Assert.IsNull( resource.RefusalMessage, "a real resource has nothing to refuse" );

                typeof( Hearthglade.Gameplay.Resource.Resource )
                    .GetField( "isProp", BindingFlags.NonPublic | BindingFlags.Instance )
                    .SetValue( resource, true );
                Assert.IsFalse( resource.CanInteract() );
                Assert.IsFalse( string.IsNullOrEmpty( resource.RefusalMessage ), "a scenery resource explains itself" );
            }
            finally {
                UnityEngine.Object.DestroyImmediate( go );
            }
        }

        [ Test ]
        public void ReadingTime_GrowsWithTheText_WithinLimits( ) {
            float shortText = MurmurService.ReadingTime( "Hi", null );
            float longText = MurmurService.ReadingTime( new string( 'a', 40 ), new string( 'b', 30 ) );
            float huge = MurmurService.ReadingTime( new string( 'a', 1000 ), null );
            Assert.GreaterOrEqual( shortText, 2f );
            Assert.Greater( longText, shortText );
            Assert.LessOrEqual( huge, 6f );
        }
    }
}
