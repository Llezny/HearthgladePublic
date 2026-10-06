using Hearthglade.Core.Expedition;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Expeditions
{
    /// <summary>
    /// Keeps how far the player has sailed in each direction and saves it. A registered entry point so it exists from
    /// the start of the game: a saveable that nothing resolved would be missing from the save and its progress lost.
    /// The number of completed expeditions stays with <c>PortService</c>, which the thresholds of the ports read.
    /// </summary>
    public sealed class ExpeditionService : IStartable, ISaveable
    {
        private readonly MessagePopup messagePopup;
        private readonly ExpeditionProgress progress = new ExpeditionProgress();

        public ExpeditionProgress Progress => progress;

        public ExpeditionService(SaveManager saveManager, MessagePopup messagePopup)
        {
            this.messagePopup = messagePopup;
            saveManager.RegisterISavable(this);
            if (saveManager.TryGetState<ExpeditionService>(out var saved))
            {
                RestoreState(saved);
            }
        }

        public void Start()
        {
        }

        /// <summary>An expedition island was left for home: a new depth record opens the next depth of that direction.</summary>
        /// <returns>True when it was a new depth record.</returns>
        public bool Complete(ExpeditionTarget target)
        {
            if (!progress.Complete(target))
            {
                return false;
            }
            if (progress.NextDepth(target.Direction) > target.Depth)
            {
                var text = $"You can now sail {target.Direction} to depth {progress.NextDepth(target.Direction)}";
                messagePopup.AddTextMessageToQueue(ref text);
            }
            return true;
        }

        /// <summary>For the console: marks a depth of a direction as done (and every shallower one).</summary>
        public void SetMaxDepth(Direction direction, int depth)
        {
            var data = progress.ToData();
            data.MaxDepth[(int)direction] = depth;
            progress.Load(data);
        }

        public object CaptureState()
        {
            return progress.ToData();
        }

        public void RestoreState(object state)
        {
            var data = state is JToken token
                ? token.ToObject<ExpeditionProgressData>()
                : JsonConvert.DeserializeObject<ExpeditionProgressData>(state.ToString());
            progress.Load(data);
        }
    }
}
