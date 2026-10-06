using System;
using Lean.Touch;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.Common
{
    public class ContextButton : MonoBehaviour {

        [ SerializeField ] Image buttonBackround = null;
        [ SerializeField ] TextMeshProUGUI buttonText = null;

        private Action callback = null;
        private bool isFirstTouch = false;

        public void SetupButton( Vector3 position, Action callback, string buttonText = "" ) {
            this.callback = callback;
            Lean.Touch.LeanTouch.OnFingerTap += HandleTouch;
            Lean.Touch.LeanTouch.OnFingerDown += HandleDragDrop;
            buttonBackround.rectTransform.position = position;
            this.buttonText.text = buttonText;
        }

        private void HandleDragDrop( LeanFinger finger ) {
            var buttonRectTransform = buttonBackround.GetComponent<RectTransform>();
            if(!RectTransformUtility.RectangleContainsScreenPoint( buttonRectTransform, finger.ScreenPosition ) ) {
                GameObject.Destroy( this.gameObject );
            }
        }


        private void HandleTouch( LeanFinger finger ) {
            if( !isFirstTouch ) {
                isFirstTouch = true;
                return;
            }
            var buttonRectTransform = buttonBackround.GetComponent<RectTransform>();
            if(RectTransformUtility.RectangleContainsScreenPoint( buttonRectTransform, finger.ScreenPosition ) ) {
                this.callback();
            }
            GameObject.Destroy( this.gameObject );
        }

        private void OnDestroy() {
            Lean.Touch.LeanTouch.OnFingerTap -= HandleTouch;
            Lean.Touch.LeanTouch.OnFingerDown -= HandleDragDrop;
        }
    }
}
