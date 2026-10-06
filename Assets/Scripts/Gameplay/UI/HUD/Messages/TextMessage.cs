using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.HUD.Messages
{
    public class TextMessage : MonoBehaviour, IMessage {
        private const float HIDE_TEXT_DURATION = 0.4f;

        private static readonly Color FADE_OUT_COLOR = new Color( 1, 1, 1, 0 );
        private Action showNextPopup;

        [ SerializeField ] public TextMeshProUGUI messageText;

        public void Show( Action showNextPopupCallback ) {
            this.showNextPopup = showNextPopupCallback; 
            this.transform.position = Player.Controller.Player.instance.transform.position + new Vector3(0, 0.5f, 0);
            this.transform.DOMoveY( this.transform.position.y + 0.5f, 1.5f ).onComplete += HidePopup;
        }

        private void HidePopup() {
            messageText.DOColor( FADE_OUT_COLOR, HIDE_TEXT_DURATION ).onComplete += DespawnPopup;
        }

        private void DespawnPopup( ) {
            Lean.Pool.LeanPool.Despawn( this );
            showNextPopup.Invoke( );
            showNextPopup = null;
        }
    }
}
