using UnityEditor;
using UnityEditor.SceneManagement;

namespace Hearthglade.EditorTools
{
    // THROWAWAY: starts the Game scene in play mode for Hearthglade.Gameplay.Debug.HouseShot (batch mode, with graphics, no -quit).
    public static class HouseShotRunner
    {
        public static void Run()
        {
            System.Environment.SetEnvironmentVariable( "HOUSE_SHOT", System.Environment.GetEnvironmentVariable( "HOUSE_SHOT_DIR" ) );
            EditorSceneManager.OpenScene( "Assets/Scenes/Game.unity", OpenSceneMode.Single );
            EditorApplication.EnterPlaymode();
        }
    }
}
