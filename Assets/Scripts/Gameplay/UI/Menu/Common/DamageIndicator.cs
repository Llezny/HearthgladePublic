using Hearthglade.Gameplay.Events;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Common
{
    public class DamageIndicator : MonoBehaviour {

        private GameEvents gameEvents;

        [ Inject ]
        public void Construct( GameEvents gameEvents ) {
            this.gameEvents = gameEvents;
        }

        private Animator anim;
        private Image img;

        void Awake() {
            anim = this.GetComponent<Animator>();
            img = this.GetComponent<Image>();
            img.enabled = false;
        }

        void OnEnable() {
            gameEvents.onPlayerTakingDamage += () => {
                img.enabled = true;
                anim.SetBool( "LowHealth", true );
            };

            gameEvents.onPlayerNotTakingDamage += () => {
                img.enabled = false;
                anim.SetBool( "LowHealth", false );
            };
        }
    }
}
