using System;
using System.Collections.Generic;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Items;
using UnityEngine;
using LeanPool = Lean.Pool.LeanPool;


namespace Hearthglade.Gameplay.UI.HUD.Messages {
    public class MessageFactory : IMessageFactory {
        private CanvasService canvasService;
        private MessagesDictionarySO messagesDictionarySO;
        private Dictionary<MessageType, GameObject> messagePrefabsDictionary = new();
        private Canvas worldSpaceCanvas;

        public MessageFactory(CanvasService canvasService, MessagesDictionarySO messagesDictionarySO ) {
            this.canvasService = canvasService;
            this.messagesDictionarySO = messagesDictionarySO;
            worldSpaceCanvas = canvasService.WorldSpaceCanvas;
            foreach(var msg in messagesDictionarySO.messages ) {
                if(!messagePrefabsDictionary.TryAdd(msg.Item1, msg.Item2)) {
                    UnityEngine.Debug.LogWarning($"Failed to cache message prefab! MessageType: {msg.Item1}, Prefab Name: {msg.Item2.name}");
                }
            }
        }
        
        public IMessage SpawnItemQuantityChangedMessage(ItemSO itemSo, int quantity) {
            var instance = LeanPool.Spawn( messagePrefabsDictionary[MessageType.QuantityChanged], worldSpaceCanvas.transform ).GetComponent<ItemQuantityChanged>();
            if( instance == null ) {
                UnityEngine.Debug.LogError( $"Failed to spawn {nameof(ItemQuantityChanged)}");
                return null;
            }
            var symbol = quantity < 0 ? "-" : "+";
            instance.itemIcon.color = Color.white;
            instance.messageText.color = Color.white;
            instance.itemIcon.sprite = itemSo.icon;
            instance.messageText.text = $"{symbol} {quantity} {itemSo.itemName}";
            return instance;
        }

        public IMessage SpawnTextMessage(ref string text) {
            var instance = LeanPool.Spawn( messagePrefabsDictionary[MessageType.Text], worldSpaceCanvas.transform ).GetComponent<TextMessage>();
            if( instance == null ) {
                UnityEngine.Debug.LogError( $"Failed to spawn {nameof(TextMessage)}");
                return null;
            }
            instance.messageText.text = text;
            return instance;
        }
    }

}
