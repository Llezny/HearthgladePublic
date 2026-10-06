using System;
using DG.Tweening;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.HUD.Messages
{
    public class ItemQuantityChanged : MonoBehaviour, IMessage {
        
        private const string PREFAB_NAME = nameof(ItemQuantityChanged);
        private const float HIDE_ICON_DURATION = 0.3f;
        private const float HIDE_TEXT_DURATION = 0.4f;

        private static readonly Color FADE_OUT_COLOR = new Color( 1, 1, 1, 0 );
        
        private Sprite sprite;
        private int quantity;
        private Action showNextPopup;

        [ SerializeField ] public Image itemIcon;
        [ SerializeField ] public TextMeshProUGUI messageText;

        public void Show( Action showNextPopupCallback ) {
            this.showNextPopup = showNextPopupCallback; 
            this.transform.position = Player.Controller.Player.instance.transform.position + new Vector3(0, 0.5f, 0);
            this.transform.DOMoveY( this.transform.position.y + 0.5f, 1.5f ).onComplete += HidePopup;
        }

        private void HidePopup() {
            itemIcon.DOColor( FADE_OUT_COLOR, HIDE_ICON_DURATION );
            messageText.DOColor( FADE_OUT_COLOR, HIDE_TEXT_DURATION ).onComplete += DespawnPopup;
        }

        private void DespawnPopup( ) {
            Lean.Pool.LeanPool.Despawn( this );
            showNextPopup.Invoke( );
            showNextPopup = null;
        }
    }
}
