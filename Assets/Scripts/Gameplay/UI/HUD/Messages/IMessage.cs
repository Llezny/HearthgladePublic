using System;

namespace Hearthglade.Gameplay.UI.HUD.Messages
{
    public interface IMessage {
        void Show( Action onComplete );
    }
}
