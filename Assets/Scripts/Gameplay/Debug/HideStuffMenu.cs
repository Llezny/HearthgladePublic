using System.Collections.Generic;
using Hearthglade.Gameplay.Environment;
using UnityEngine;

namespace Hearthglade.Debug
{
    public class HideStuffMenu : MonoBehaviour {
        private List<SceneObject> hiddenSceneObjects = new( );
        private bool sceneObjectsHidden;
        private void HideAllSceneObjects( ) {
            hiddenSceneObjects = new List<SceneObject>(FindObjectsByType<SceneObject>( FindObjectsSortMode.None));
            foreach ( var obj in hiddenSceneObjects ) {
                obj.gameObject.SetActive( false );
            }
        }
        private void ShowAllSceneObjects( ) {
            foreach ( var obj in hiddenSceneObjects ) {
                obj.gameObject.SetActive( true );
            }
            hiddenSceneObjects.Clear();
        }

        public void ToggleSceneObjects( ) {
            sceneObjectsHidden = !sceneObjectsHidden;
            if ( sceneObjectsHidden ) {
                ShowAllSceneObjects( );
            }
            else {
                HideAllSceneObjects( );
            }
        }
    }
}
