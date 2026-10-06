using Hearthglade.Gameplay.Environment;

namespace Hearthglade.Gameplay.Trade
{
    /// <summary>
    /// A house, stall or crate of a port. It is a scene object only so that a point of interest can place it by prefab name
    /// (see PoiBuilder); it has a collider but cannot be interacted with or gathered.
    /// </summary>
    public class PortProp : SceneObject
    {
        public override bool CanInteract()
        {
            return false;
        }
    }
}
