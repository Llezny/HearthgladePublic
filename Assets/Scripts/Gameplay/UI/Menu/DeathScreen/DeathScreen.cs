using Hearthglade.Gameplay.Events;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.Menu.DeathScreen
{
    public class DeathScreen : Common.Menu {

        private Animator animator;
        private GameObject contentContainer;

        private void Awake(){
            contentContainer = this.transform.Find( "Content" ).gameObject;
            animator = contentContainer.GetComponent<Animator>();
            animator.SetBool( "isPlayerDead", false );
            contentContainer.SetActive( false );
        }

        private void OnEnable(){
            gameEvents.onPlayerDeath += ShowDeathScreen;
        }

        private void OnDisable(){
            gameEvents.onPlayerDeath -= ShowDeathScreen;
        }

        public void ShowDeathScreen(){
            OpenMenu();
            UnityEngine.Debug.Log( "Player died!" );
          //  Player.Controller.Player.instance.playerController.LockControl(); TODO use DI to get player controller
            contentContainer.SetActive( true );
            animator.SetBool( "isPlayerDead", true );
        }
    }
}
