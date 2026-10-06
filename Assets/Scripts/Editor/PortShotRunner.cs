using UnityEditor;
using UnityEditor.SceneManagement;

namespace Hearthglade.EditorTools
{
    // THROWAWAY: starts the Game scene in play mode for Hearthglade.Gameplay.Debug.PortShot (batch mode, with graphics, no -quit).
    public static class PortShotRunner
    {
        public static void Run()
        {
            System.Environment.SetEnvironmentVariable( "PORT_SHOT", System.Environment.GetEnvironmentVariable( "PORT_SHOT_DIR" ) );
            EditorSceneManager.OpenScene( "Assets/Scenes/Game.unity", OpenSceneMode.Single );
            EditorApplication.EnterPlaymode();
        }
    }
}
