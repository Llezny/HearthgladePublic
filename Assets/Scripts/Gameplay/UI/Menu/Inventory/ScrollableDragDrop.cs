using System;
using DG.Tweening;
using Hearthglade.Gameplay.Common.Extension;
using Hearthglade.Gameplay.Common.Service;
using Lean.Touch;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/*  Add this component to the object which are using drag n drop and are part of scrollable list  */
namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public class ScrollableDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        public RectTransform Target;
        public ScrollRect MainScroll;
        public Transform ParentDuringDrag;
        
        private const float MAX_VERTICAL_NORMALIZED_POSITION = 1.05f;
        private const float MIN_VERTICAL_NORMALIZED_POSITION = -0.05f;
        private const float SCROLLING_SPEED = 0.5f;
        
        private float scrollingUpThreshold = float.MaxValue;
        private float scrollingDownThreshold = float.MinValue;
        private float scrollingDirection;
        
        private bool dragIconToTouch;
        private Image itemBackgroundImage;
        private Transform parentOnIdle;
        private InventorySlotView draggedSlot;
        private Transform slotParentBeforeDragDrop;

        public static InventorySlotView Create( InventorySlotView slot, ScrollRect inventoryScroll, Transform parentDuringDrag ) {
            if ( slot.TryGetComponent<DragDrop>( out var obj ) ||
                 slot.TryGetComponent<ScrollableDragDrop>( out var obj2 ) ) { // replace with interface
                UnityEngine.Debug.LogError($"Slot named {slot.name} already has drag & drop component");
                return slot;
            }

            var dragDrop = slot.gameObject.AddComponent<ScrollableDragDrop>( );
            dragDrop.MainScroll = inventoryScroll;
            dragDrop.Target = slot.SlotBackground.rectTransform;
            dragDrop.ParentDuringDrag = parentDuringDrag;

            dragDrop.itemBackgroundImage = dragDrop.Target.GetComponent<Image>( );
            dragDrop.parentOnIdle = dragDrop.itemBackgroundImage.rectTransform.parent;

            return slot;
        }

        public void OnBeginDrag(PointerEventData eventData) {
            LeanTouch.OnFingerUpdate += UpdateTouch;

            draggedSlot = Target.parent.GetComponent<InventorySlotView>( );
            slotParentBeforeDragDrop = itemBackgroundImage.transform.parent;
            itemBackgroundImage.rectTransform.SetParent(ParentDuringDrag);
            
            var rect = MainScroll.GetComponent<RectTransform>( );
            var rectSizeY = rect.rect.size.y;
            var offsetMinY = rect.offsetMin.y;
            scrollingDownThreshold = offsetMinY + 0.2f * rectSizeY;
            scrollingUpThreshold = offsetMinY + 0.8f * rectSizeY;
            
            MainScroll.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData) {
            dragIconToTouch = true;
            if ( itemBackgroundImage.color == Color.clear ) {
                MainScroll.OnDrag(eventData);
                return;
            }
            if ( eventData.position.y < scrollingDownThreshold && MainScroll.verticalNormalizedPosition > MIN_VERTICAL_NORMALIZED_POSITION ) {
                scrollingDirection = -SCROLLING_SPEED;
            }
            else if ( eventData.position.y > scrollingUpThreshold && MainScroll.verticalNormalizedPosition < MAX_VERTICAL_NORMALIZED_POSITION ) {
                scrollingDirection = SCROLLING_SPEED;
            }
            else {
                scrollingDirection = 0;
            }
        }
 
        public void OnEndDrag(PointerEventData eventData) {
            LeanTouch.OnFingerUpdate -= UpdateTouch;
            dragIconToTouch = false;
            
            if( draggedSlot != null ) {
                draggedSlot.TryDropOnto( eventData );
            }

            Target.DOMoveInTargetLocalSpace(slotParentBeforeDragDrop, Vector3.zero, 0).onComplete += GoToDefaultParent;
            MainScroll.OnEndDrag(eventData);
            slotParentBeforeDragDrop = null;
            draggedSlot = null;
        }

        private void GoToDefaultParent( ) {
            itemBackgroundImage.rectTransform.SetParent( parentOnIdle );
        }

        // The frame is reparented under the top canvas while dragging, so destroying the slot mid-drag
        // (menu closed/rebuilt) would leave it stranded there, still showing the old item.
        private void OnDestroy( ) {
            LeanTouch.OnFingerUpdate -= UpdateTouch;
            if ( Target != null ) {
                Target.DOKill( );
            }
            if ( itemBackgroundImage != null && itemBackgroundImage.rectTransform.parent != parentOnIdle ) {
                Destroy( itemBackgroundImage.gameObject );
            }
        }
        
        
        private void UpdateTouch( LeanFinger finger ) {
            if ( dragIconToTouch ) {
                Target.position = finger.ScreenPosition;
            }
            MainScroll.verticalNormalizedPosition = Mathf.Clamp( 
                MainScroll.verticalNormalizedPosition + Time.deltaTime * scrollingDirection, 
                MIN_VERTICAL_NORMALIZED_POSITION, 
                MAX_VERTICAL_NORMALIZED_POSITION 
            );
        }
    }
}

