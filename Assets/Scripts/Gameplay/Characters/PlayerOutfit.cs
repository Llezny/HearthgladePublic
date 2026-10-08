using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Characters
{
    /// <summary>
    /// Dresses the player's character in what they wear: an item with a worn part puts it on the matching slot of the <see cref="CharacterView"/>,
    /// taking it off brings back the part the player's own appearance has there.
    /// </summary>
    public class PlayerOutfit : MonoBehaviour
    {
        [SerializeField] CharacterView view;

        private EquipmentService equipment;

        [Inject]
        public void Construct(EquipmentService equipment)
        {
            this.equipment = equipment;
            equipment.OnEquipped += Equip;
            equipment.OnUnequipped += Unequip;
        }

        private void OnDestroy()
        {
            if (equipment != null)
            {
                equipment.OnEquipped -= Equip;
                equipment.OnUnequipped -= Unequip;
            }
        }

        private void Equip(ItemSO item, SlotType slotType)
        {
            if (item != null && item.WornPart != null)
            {
                view.Wear(item.WornPart);
            }
        }

        private void Unequip(ItemSO item, SlotType slotType)
        {
            if (item == null || item.WornPart == null || view.Appearance == null)
            {
                return;
            }
            var own = view.Appearance.Get(item.WornPart.Slot);
            if (own != null)
            {
                view.Wear(own);
            }
        }
    }
}
