using Hearthglade.Gameplay.Events;
using UnityEngine;
using DG.Tweening;
using VContainer;

namespace Hearthglade.Gameplay.UI.HUD {
   public class HUD : MonoBehaviour {
      protected GameObject content;
      protected RectTransform contentRectTransform;
      protected virtual Vector2 positionOnClose { get => new( 0, -3000 ); }
      protected virtual Vector2 positionOnOpen { get => Vector2.zero; }
      protected float animationTime = 0.2f;
      protected virtual bool hideOnPause => false;
      protected virtual bool hideOnAnyMenuShow => false;

      protected GameEvents gameEvents;

      [ Inject ]
      public void Construct( GameEvents gameEvents ) {
         this.gameEvents = gameEvents;
      }

      private void Awake () {
         var contentTransform = transform.Find( "Content" );
         
         if ( contentTransform ) {
            content = contentTransform.gameObject;
            contentRectTransform = content.GetComponent<RectTransform>();
         }
         else {
            content = new GameObject( "Content" );
            contentRectTransform = content.AddComponent<RectTransform>();
            content.transform.SetParent( this.transform, false );
         }

      }
      protected virtual void OnEnable() {
         if(hideOnPause) {
            gameEvents.onGamePause += () => Hide();
            gameEvents.onGameResume += () => Show();
         }
         if(hideOnAnyMenuShow) {
            gameEvents.onMenuShow += () => Hide();
            gameEvents.onMenuHide += () => Show();
         }
      }

      protected virtual void OnDisable() {
         if(hideOnPause) {
            gameEvents.onGamePause -= () => Hide();
            gameEvents.onGameResume -= () => Show();
         }
         if(hideOnAnyMenuShow) {
            gameEvents.onMenuShow -= () => Hide();
            gameEvents.onMenuHide -= () => Show();
         }
      }

      protected void Show() {
         contentRectTransform.DOAnchorPos( positionOnOpen, animationTime );
      }

      protected void Hide() {
         contentRectTransform.DOAnchorPos( positionOnClose, animationTime );
      }
   }
}
