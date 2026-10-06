using System.Collections.Generic;
using Hearthglade.Gameplay.Common;
using UnityEngine;

[CreateAssetMenu(fileName = "MessagesDictionary", menuName = "ScriptableObjects/MessagesDictionary")]
    public class MessagesDictionarySO : ScriptableObject {
        public List<Pair<Hearthglade.MessageType, GameObject>> messages = new();
    }