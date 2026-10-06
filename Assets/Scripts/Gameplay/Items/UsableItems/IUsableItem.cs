using Hearthglade.Gameplay.Audio;

namespace Hearthglade.Gameplay.Items.UsableItems
{
    public interface IUsableItem {
        AudioSO UseSound { get; }
        public float ThirstHealing {get; }
        public float HungerHealing {get; }
        public float HealthHealing {get; }
    }
}
