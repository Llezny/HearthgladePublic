using Hearthglade.Gameplay.Environment;

namespace Hearthglade.Gameplay.Items.BuildableItems
{
    // Marker SceneObject for a placed building piece (wall, door, floor tile, ...) with no interaction of its
    // own. Every saveable scene object needs a SceneObject component - BlockContent.SpawnSceneObject requires
    // one to respawn it from a save, and BlockContent.RegisterBuildingPiece needs the respawn to have
    // actually happened for the piece to occupy BuildGrid/EdgeGrid/FloorGrid again after a reload.
    public class BuildingPiece : SceneObject { }
}
