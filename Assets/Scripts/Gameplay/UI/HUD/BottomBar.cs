using DG.Tweening;
using Hearthglade.Gameplay.Events;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.UI.HUD
{
    public class BottomBar : MonoBehaviour {

        private Vector2 openPosition = new Vector2( 0, 180);
        private Vector2 closedPosition = new Vector2( 0, -2000);
        private float animationTime = 0.1f;

        private GameEvents gameEvents;

        [ Inject ]
        public void Construct( GameEvents gameEvents ) {
            this.gameEvents = gameEvents;
        }

        void Start() {
            var rectTransform = GetComponent<RectTransform>();
            gameEvents.onGamePause += () => rectTransform.DOAnchorPos( closedPosition, animationTime );
            gameEvents.onGameResume += () => rectTransform.DOAnchorPos( openPosition, animationTime );
        }
    }
}
