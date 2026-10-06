using System.Collections.Generic;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.Menu.MainMenu
{
    public class GamesListMenu : MonoBehaviour {
        [ SerializeField ] SaveMenuItem saveMenuItemPrefab = null;
        [ SerializeField ] Transform saveMenuItemHolder = null;
        [ SerializeField ] MainMenuManager mainMenuManager;

        private void OnEnable( ) {
            mainMenuManager.OnSaveHeadersLoaded += SpawnGameList;
        }

        private void OnDisable( ) {
            mainMenuManager.OnSaveHeadersLoaded -= SpawnGameList;
        }

        private void SpawnGameList( List<SaveHeader> saveHeaders ) {
            foreach ( var saveHeader in saveHeaders ) {
                var saveMenuItem = Instantiate(
                    saveMenuItemPrefab,
                    Vector3.zero,
                    Quaternion.identity,
                    saveMenuItemHolder.transform 
                );
                saveMenuItem.UpdateUI(saveHeader);
            }
        }
    }
}
