using Hearthglade.Gameplay.Environment;

namespace Hearthglade.Gameplay.Housing
{
    /// <summary>The door inside a house: tapping it takes the player back out.</summary>
    public class HouseExit : SceneObject, IInteractable
    {
        public string TooltipTitle => "Door";
        public string TooltipDescription => "Tap to go out";
        public float InteractionDistance => 0.35f;

        public override void InteractionStart()
        {
            var interior = GetComponentInParent<HouseInterior>();
            if ( interior != null )
            {
                interior.RequestExit();
            }
        }
    }
}
