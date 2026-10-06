using Hearthglade.Gameplay.Common.SceneLoader;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Common {
    public class PauseMenu : Menu {

        [ SerializeField ] Button CloseMenuButton;
        [ SerializeField ] Button SaveButton;
        [ SerializeField ] Button ExitButton;
    
        private SaveManager saveManager;
    
        [ Inject ]
        public void Construct( SaveManager saveManager ) {
            this.saveManager = saveManager;
        }

        private void OnEnable() {
            CloseMenuButton.onClick.AddListener(Close);
            SaveButton.onClick.AddListener(saveManager.SaveGame);
            ExitButton.onClick.AddListener(LoadMainMenu);
        }

        private void OnDisable() {
            CloseMenuButton.onClick.RemoveListener(Close);
            SaveButton.onClick.RemoveListener(saveManager.SaveGame);
            ExitButton.onClick.RemoveListener(LoadMainMenu);
        }

        override public void OpenMenu() {
            base.OpenMenu();
            gameEvents.GamePause();
        }

        public override void CloseMenu( bool checkIfFadeOutShroud = true ){
            base.CloseMenu( checkIfFadeOutShroud );
            gameEvents.GameResume();
        }

        public void Close(){
            this.CloseMenu();
        }

        public void LoadMainMenu(){
            SceneLoader.Instance.LoadScene( "MainMenu" );
        }
    }
}
