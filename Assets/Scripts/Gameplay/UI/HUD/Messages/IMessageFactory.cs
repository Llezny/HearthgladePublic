using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.UI.HUD.Messages;

public interface IMessageFactory {
    public IMessage SpawnItemQuantityChangedMessage( ItemSO itemSo, int quantity );
    public IMessage SpawnTextMessage( ref string text );
    public MurmurBubble SpawnMurmurBubble();
}