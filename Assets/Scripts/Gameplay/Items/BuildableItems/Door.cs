using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Map;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Items.BuildableItems
{
    // A placed door: unlike a plain BuildingPiece (wall, ...) it swings open/closed on interaction. The
    // frame (posts + lintel) lives on this object and never moves; doorAnimation swings the child leaf
    // transform, whose own BoxCollider blocks/clears the opening visually - but actual player movement is
    // gated by Map.CanMoveTo/EdgeGrid (see PlayerController.Walk), which has no idea the leaf rotated, so
    // without also touching EdgeGrid here an "open" door still blocked movement exactly like a closed wall.
    // The door stays registered in EdgeGrid the whole time (see BuildEdgeGrid.SetOpen) rather than being
    // removed/re-placed - removing it used to also make room-enclosure detection (PlayerController) treat an
    // open doorway as a gap to the outside, so standing inside with the door open stopped counting as "in a
    // room" at all.
    public class Door : SceneObject {
        [ SerializeField ] DoorAnimation doorAnimation;

        private MapManager mapManager;

        [ Inject ]
        public void Construct( MapManager mapManager ) {
            this.mapManager = mapManager;
        }

        public override void InteractionStart() {
            bool open = doorAnimation.ChangeState();
            SetEdgeOpen( open );
        }

        private void SetEdgeOpen( bool open ) {
            var edgeGrid = mapManager?.CurrentMap?.EdgeGrid;
            if( edgeGrid == null ) {
                return;
            }
            var ( x, z, side ) = MapHelper.WorldPositionToEdge( transform.position );
            int y = Mathf.RoundToInt( transform.position.y / MapGenerator.TILE_X_OFFSET );
            if( edgeGrid.TryGetPieceId( x, z, y, side, out var pieceId ) ) {
                edgeGrid.SetOpen( pieceId, open );
            }
        }
    }
}
