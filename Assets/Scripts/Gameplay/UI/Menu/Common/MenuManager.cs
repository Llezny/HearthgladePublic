using System.Collections.Generic;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.UI.HUD;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Common
{
    public class MenuManager : MonoBehaviour {
        public static MenuManager instance = null;
        public static bool isGamePaused = false;
        public List<Menu> openedMenu = new List<Menu>();

        private GameEvents gameEvents;

        [ Inject ]
        public void Construct( GameEvents gameEvents ) {
            this.gameEvents = gameEvents;
        }

        public void OpenMenu( Menu menu, bool closeAll = true ) {

            if(!isGamePaused) {
                isGamePaused = true;
            }
            if( closeAll ){
                CloseAll( false, menu );
            }
            gameEvents.OnMenuShow();
            openedMenu.Add(menu);
        }

        public void CloseAll() {
            CloseAll( true );
        }

        public void CloseAll( bool fadeOutShroud = false, Menu menu = null ) {
            var menuCount = openedMenu.Count;
            while( menuCount > 0){
                menuCount--;
                var type = menu?.GetType();
                if( menu == null || ( !openedMenu[menuCount].compatibleMenus?.Contains( type ) ?? true ) ) {
                    openedMenu[menuCount].CloseMenu( false );
                }

            }
        }

        public void CloseMenu( Menu menu, bool fadeOutShroud = true ){
            var menuCount = openedMenu.Count;
            if(menuCount == 1) {
                isGamePaused = false;
            }
            openedMenu.Remove(menu);
        }

        void Awake(){
            if(instance == null) {
                instance = this;
            }
            else {
                GameObject.Destroy(this);
            }
            isGamePaused = false;
        }
    }
}
