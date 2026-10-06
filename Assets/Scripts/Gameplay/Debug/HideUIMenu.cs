using System.Collections.Generic;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.UI.HUD;
using Hearthglade.Gameplay.UI.Menu.Common;
using UnityEngine;

namespace Hearthglade.Debug
{
    public class HideUIMenu : MonoBehaviour {
        private List<HUD> hiddenUIObjects = new( );
        private bool UIObjectsHidden = false;
        
        private void HideAllUIObjects( ) {
            hiddenUIObjects = new List<HUD>(FindObjectsByType<HUD>( FindObjectsSortMode.None));
            foreach ( var obj in hiddenUIObjects ) {
                obj.gameObject.SetActive( false );
            }
        }
        private void ShowAllUIObjects( ) {
            foreach ( var obj in hiddenUIObjects ) {
                obj.gameObject.SetActive( true );
            }
            hiddenUIObjects.Clear();
        }

        public void ToggleUIObjects( ) {
            UIObjectsHidden = !UIObjectsHidden;
            if ( UIObjectsHidden ) {
                ShowAllUIObjects( );
            }
            else {
                HideAllUIObjects( );
            }
        }
    }
}
