using System.Collections.Generic;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.UI.HUD.Messages
{
    public class MessagePopup : MonoBehaviour {
    
        /*
       Message types:
        - itemsAdded
        - itemsRemoved
    */

        // Dependencies
        private InventoryService inventoryService;
        private IMessageFactory messageFactory;
        //
        [ Inject ]
        public void Construct( InventoryService inventoryService, IMessageFactory messageFactory ) {
            this.inventoryService = inventoryService;
            this.messageFactory = messageFactory;
            inventoryService.OnItemAdded += AddItemsAddedMessageToQueue;
            inventoryService.OnItemRejected += AddInventoryFullMessageToQueue;
        }   
        
        Queue<IMessage> messagesQueue = new Queue<IMessage>();
        bool isAlreadyPlayingAnim = false;

        public void AddItemsAddedMessageToQueue( ItemSO itemSo, int quantity ) {
            AddToQueue( messageFactory.SpawnItemQuantityChangedMessage( itemSo, quantity ) );
        }

        private void AddInventoryFullMessageToQueue( ItemSO itemSo, int quantity ) {
            var text = "Inventory full";
            AddTextMessageToQueue( ref text );
        }

        public void AddTextMessageToQueue( ref string text ) {
            AddToQueue( messageFactory.SpawnTextMessage( ref text ) );
        }

        public void AddToQueue( IMessage msg ) {
            messagesQueue.Enqueue( msg );
            PlayNextAnimation();
        }

        private void PlayNextAnimation(){
            if( isAlreadyPlayingAnim || messagesQueue.Count < 1) {
                return;
            }
            isAlreadyPlayingAnim = true;
            var msg = messagesQueue.Dequeue();
            msg.Show( OnShowComplete );
        }

        void OnShowComplete() {
            isAlreadyPlayingAnim = false;
            PlayNextAnimation();
        }
    }
}
