using DG.Tweening;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.Menu.Inventory
{
    public class Slot : MonoBehaviour{
        protected static Vector3 zoomingScale = new Vector3(1.2f,1.2f,1.2f);
        protected RectTransform slotRectTransform;
        public virtual void OnMouseOverSlot(){
            slotRectTransform.DOScale(zoomingScale,0.1f);
        }
        public virtual void OnMouseExitSlot(){
            slotRectTransform.DOScale(Vector3.one,0.1f);
        }
        protected virtual void Awake(){
            slotRectTransform = GetComponent<RectTransform>();
        }
    }
}
