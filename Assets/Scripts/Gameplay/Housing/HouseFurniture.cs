using System.Collections.Generic;
using Hearthglade.Gameplay.Environment;

namespace Hearthglade.Gameplay.Housing
{
    /// <summary>
    /// A piece of furniture the player put into the house. A tap does its interaction (none yet: they will be added piece by piece); holding a
    /// finger on it opens a dropdown with what can be done to the piece.
    /// </summary>
    public class HouseFurniture : SceneObject, IInteractable, IContextActions
    {
        private HouseService service;
        private string title;
        private float reach;
        private ContextAction[] actions;

        public int PieceId { get; private set; }

        // Set by HouseService, which spawns the piece (it is not made by the map, so nothing injects it).
        public void Init( HouseService service, int pieceId, string title, float reach )
        {
            this.service = service;
            PieceId = pieceId;
            this.title = title;
            this.reach = reach;
            actions = new[]
            {
                new ContextAction( "Pick up", ( ) => service.PickUp( this ) ),
                new ContextAction( "Destroy", ( ) => service.Destroy( this ) ),
            };
        }

        public string TooltipTitle => title;
        public string TooltipDescription => "";

        // The player cannot stand on the piece, so the reach is its half length plus the margin the walk area keeps.
        public float InteractionDistance => reach;

        public IReadOnlyList<ContextAction> ContextActions => actions;
    }
}
