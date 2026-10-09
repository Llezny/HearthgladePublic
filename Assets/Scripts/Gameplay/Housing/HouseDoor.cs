using Hearthglade.Gameplay.Environment;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Housing
{
    /// <summary>The front door of a house on the island: tapping it takes the player inside (<see cref="HouseService"/>).</summary>
    public class HouseDoor : SceneObject, IInteractable
    {
        [ Tooltip( "The room the player enters. A higher house level points to a bigger room." ) ]
        [ SerializeField ] private HouseInterior interiorPrefab;

        private HouseService houseService;

        public HouseInterior InteriorPrefab => interiorPrefab;

        [ Inject ]
        public void Construct( HouseService houseService )
        {
            this.houseService = houseService;
        }

        public string TooltipTitle => "Home";
        public string TooltipDescription => "Tap to go in";
        public float InteractionDistance => 0.35f;

        public override void InteractionStart()
        {
            houseService?.Enter( this );
        }
    }
}
