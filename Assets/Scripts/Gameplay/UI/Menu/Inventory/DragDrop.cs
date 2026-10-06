using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hearthglade.Gameplay.UI.Menu.Inventory
{
    public class DragDrop : MonoBehaviour, IEndDragHandler, IDragHandler {
        [SerializeField] RectTransform itemImage = null;

        public virtual void OnDrag(PointerEventData eventData){
            itemImage.position = eventData.position;
        }

        public virtual void OnEndDrag(PointerEventData eventData){
            itemImage.DOAnchorPos( Vector2.zero, 0.2f );
            var from = itemImage.parent.GetComponent<InventorySlotView>();
            if( from != null ) {
                from.TryDropOnto( eventData );
            }
        }
    }
}
