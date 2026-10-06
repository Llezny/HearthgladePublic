using Hearthglade.Gameplay.Audio;
using Hearthglade.Gameplay.Player.Stats;
using UnityEngine;

namespace Hearthglade.Gameplay.Items.UsableItems
{
    [CreateAssetMenu(fileName = "UsableItem", menuName = "ScriptableObjects/Items/UsableItem")]
    public class UsableItem : FoodItemSO, IUsableItem {

        [Header( "Usable item" )]
        public AudioSO SfxOnUse;
        public float ThirstHealing {get => thirstHealing; }
        public float HungerHealing {get => hungerHealing; }
        public float HealthHealing {get => healthHealing; }

        [SerializeField] float thirstHealing;
        [SerializeField] float hungerHealing;
        [SerializeField] float healthHealing;


        public AudioSO UseSound => SfxOnUse;
    }
}
