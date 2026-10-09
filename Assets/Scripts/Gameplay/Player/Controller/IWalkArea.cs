using UnityEngine;

namespace Hearthglade.Gameplay.Player.Controller
{
    /// <summary>
    /// Decides where the player may walk when the map's terrain does not (inside a house). While the player has one, it replaces
    /// <see cref="Map.Map.CanMoveTo"/>.
    /// </summary>
    public interface IWalkArea
    {
        bool CanMoveTo( Vector3 from, Vector3 to );
    }
}
