using Hearthglade.Core.Items;

namespace Hearthglade.Gameplay.UI.Menu.Inventory
{
    public class ItemContainerView : Common.Menu {
        protected ItemContainer Container { get; private set; }
        public virtual void Setup( ItemContainer container ) {
            Container = container;
        }

    }
} 
