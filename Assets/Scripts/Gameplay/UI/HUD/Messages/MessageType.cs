using System;

namespace Hearthglade {
    [Serializable]
    public enum MessageType {
        Text = 0, // Simple text to display
        QuantityChanged = 1, // Message used to show increase/descrease of some item
        Murmur = 2, // Speech bubble over the player's head, see MurmurService
    }
}
