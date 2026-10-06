using System.Collections.Generic;
using System.Linq;
using CaptureTheFlag.Common;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common.Service;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hearthglade.Gameplay.Common.SceneLoader {
    public class SceneLoader : PersistentSingleton<SceneLoader> {
        [SerializeField] private List<GameScene> gameScenes;

        protected void Start( ) {
            gameScenes = ResourceLoader.LoadScenes( ).ToList(  );
        }

        public void LoadScene( GameScene scene ) {
            LoadScene( scene.SceneName.ToString(  ) );
        }

        public void LoadScene( string sceneName ) {
            LoadSceneAsync( sceneName ).Forget( );
        }

        public async UniTask LoadSceneAsync( string sceneName ) {
            LoadingScreen.Instance.Show( );
            await LoadingScreen.Instance.WaitForFadeInAsync( );
            await SceneManager.LoadSceneAsync( sceneName, LoadSceneMode.Single ).ToUniTask( );
            if ( !HasGameplayScope( SceneManager.GetActiveScene( ) ) ) {
                LoadingScreen.Instance.Hide( );
            }
        }

        private static bool HasGameplayScope( Scene scene ) {
            return scene.GetRootGameObjects( )
                .Any( go => go.GetComponentInChildren<GameplayScope>( ) != null );
        }
    }
}
