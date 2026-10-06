using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common.Service.Factory;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using DG.Tweening;
using System.Diagnostics.CodeAnalysis;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public class InventoryGridView : MonoBehaviour, IInventoryGridView {
        [ SerializeField ] Transform backpackSlotsContainer = null;
        [ SerializeField ] GameObject scrollableItemSlotPrefab = null;
        [ SerializeField ] ScrollRect itemListScroll;

        public ItemContainer Container { get; private set; }
        public RectTransform RectTransform { get => this.GetComponent<RectTransform>(); }
        public ScrollRect ItemListScroll => itemListScroll;

        
        // Dependencies
        private IInventorySlotViewFactory factoryService;
        private InventoryService inventoryService;
        private SlotViewGroup slotViews;
        //


        [ Inject ]
        public void Construct( IInventorySlotViewFactory factoryService, InventoryService inventoryService ) {
            this.factoryService = factoryService;
            this.inventoryService = inventoryService;
            // Global keyword the UI shaders need for clipping inside scroll views.
            Shader.EnableKeyword( "UNITY_UI_CLIP_RECT" );
            this.Setup( this.inventoryService.Container );
            // More rows from the ship tree: the slots of the view are made again.
            this.inventoryService.Container.Resized += ( ) => this.Setup( this.inventoryService.Container );
        }

        public void Setup( ItemContainer container ) {
            Container = container;
            slotViews?.Dispose();
            slotViews = factoryService.Populate( Container, scrollableItemSlotPrefab, backpackSlotsContainer, ItemListScroll );
        }

        public void Show() {
            RectTransform.gameObject.SetActive ( true );
            RectTransform.DOScale( Vector3.one, 0.2f );
            DOVirtual.Float( 0.9f, 1, 0.5f, (num) => { ItemListScroll.verticalNormalizedPosition = num; });
        }

        public void Hide() {
            RectTransform.gameObject.SetActive ( false );
            RectTransform.DOScale( Vector3.zero, 0.2f );
        }
    }
} 
