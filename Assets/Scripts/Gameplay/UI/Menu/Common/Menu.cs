using System;
using System.Collections.Generic;
using DG.Tweening;
using Hearthglade.Gameplay.Events;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Common
{
   public abstract class Menu : MonoBehaviour {
      protected Transform content;
      protected RectTransform contentRectTransform;
      protected bool isOpen;
      protected float animationTime = 0.2f;
      protected virtual bool hideContentOnClose { get => true; }
      protected virtual float hideContentTime { get => animationTime * 2; }
      protected virtual Vector2 menuOpenStartPos{ get => new Vector2( 0, -3000 ); }
      protected virtual Vector2 menuOpenTargetPosition { get => Vector2.zero; }
      protected virtual Vector2 menuCloseTargetPosition {  get => new Vector2( 0, -3000 ); }
      public virtual List<Type> compatibleMenus { get; set; }
      protected virtual bool activeOnStart => false;

      public Action OnClose;
      public Action OnOpen;

      protected GameEvents gameEvents;

      [ Inject ]
      public void Construct( GameEvents gameEvents ) {
         this.gameEvents = gameEvents;
      }

      protected virtual void Start (){
         isOpen = false;
         content = transform.Find( "Content" );
         contentRectTransform = content.GetComponent<RectTransform>();
         content.gameObject.SetActive( activeOnStart );
      }
      public virtual void OpenMenu(){
         if( isOpen ) {
            CloseMenu( fadeOutShroud: true );
            return;
         }
         content.gameObject.SetActive( true );
         MenuManager.instance.OpenMenu( this );
         isOpen = true;
         var tween = contentRectTransform.DOAnchorPos( menuOpenTargetPosition, animationTime );
         OnOpen?.Invoke();
      }

      public virtual void CloseMenu( bool fadeOutShroud = false ) {
         if( !isOpen )
            return;
      
         var tween = contentRectTransform.DOAnchorPos( menuCloseTargetPosition, animationTime );
         gameEvents.OnMenuHide();
         tween.onComplete += () => {
            MenuManager.instance.CloseMenu( this, fadeOutShroud );
            isOpen = false;
            contentRectTransform.localPosition = menuOpenStartPos;
         };
         OnClose?.Invoke();
         if( this.hideContentOnClose ) {
            tween.onComplete += () => content.gameObject.SetActive( false );
         }
      }
   }
}
