using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.Menu.Inventory
{
    // The slot views built for one container; disposing destroys them (and with them their slot subscriptions).
    public sealed class SlotViewGroup : IDisposable {
        private readonly List<InventorySlotView> views = new();

        public IReadOnlyList<InventorySlotView> Views => views;

        public void Add( InventorySlotView view ) {
            views.Add( view );
        }

        public void Dispose() {
            foreach( var view in views ) {
                if( view != null ) {
                    UnityEngine.Object.Destroy( view.gameObject );
                }
            }
            views.Clear();
        }
    }
}
