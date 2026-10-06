using UnityEditor;
using UnityEditor.SceneManagement;

namespace Hearthglade.EditorTools
{
    // THROWAWAY: starts the Game scene in play mode for Hearthglade.Gameplay.Debug.ExpeditionShot (batch mode, with graphics, no -quit).
    public static class ExpeditionShotRunner
    {
        public static void Run()
        {
            System.Environment.SetEnvironmentVariable( "EXPEDITION_SHOT", System.Environment.GetEnvironmentVariable( "EXPEDITION_SHOT_DIR" ) );
            EditorSceneManager.OpenScene( "Assets/Scenes/Game.unity", OpenSceneMode.Single );
            EditorApplication.EnterPlaymode();
        }
    }
}
