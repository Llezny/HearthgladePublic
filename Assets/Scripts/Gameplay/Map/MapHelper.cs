using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    public class MapHelper {

        // Blocks sit on a plain square grid (GridToWorldPosition has no per-row shift), so this is the exact
        // inverse of it. It used to shift odd rows as if they were a hex grid, which made roughly a fifth of
        // every odd-row tile resolve to its left neighbour.
        public static SerializableVector2Int WorldPositionToBlockIndex(Vector3 worldPosition) {
            return new SerializableVector2Int(
                GridMath.WorldToGrid( worldPosition.x, MapGenerator.TILE_X_OFFSET ),
                GridMath.WorldToGrid( worldPosition.z, MapGenerator.TILE_Z_OFFSET ) );
        }

        public static SerializableVector2Int GetCurrentBlockIndex(){
            return WorldPositionToBlockIndex(Player.Controller.Player.instance.transform.position);
        }

        public static int GetRandomBlockIndex(bool makeBlockEmpty, int listCount) {
            return (makeBlockEmpty ? Random.Range(0, listCount ) : Random.Range(1, listCount ) );
        }

        public static Vector3 GridToWorldPosition( Vector2 blockGridPos ) {
            var posX = blockGridPos.x * MapGenerator.TILE_X_OFFSET;
            var posZ = blockGridPos.y * MapGenerator.TILE_Z_OFFSET;
            return new Vector3(posX, 0, posZ);
        }

        // An edge-anchored piece (wall/door) sits exactly on the boundary shared by two cells: one axis
        // coordinate is offset by half a cell (the edge's normal), the other lands on a whole cell coordinate
        // (its position along the edge). Whichever axis is nearer a half-cell fraction is the normal axis.
        public static (int x, int z, EdgeSide side) WorldPositionToEdge( Vector3 worldPosition ) {
            float gx = worldPosition.x / MapGenerator.TILE_X_OFFSET;
            float gz = worldPosition.z / MapGenerator.TILE_Z_OFFSET;
            float fracX = gx - Mathf.Floor( gx );
            float fracZ = gz - Mathf.Floor( gz );
            if( Mathf.Abs( fracX - 0.5f ) < Mathf.Abs( fracZ - 0.5f ) ) {
                return ( Mathf.FloorToInt( gx ), Mathf.RoundToInt( gz ), EdgeSide.PlusX );
            }
            return ( Mathf.RoundToInt( gx ), Mathf.FloorToInt( gz ), EdgeSide.PlusZ );
        }

        public static Vector3 EdgeToWorldPosition( int x, int z, EdgeSide side ) {
            return side == EdgeSide.PlusX
                ? new Vector3( ( x + 0.5f ) * MapGenerator.TILE_X_OFFSET, 0f, z * MapGenerator.TILE_Z_OFFSET )
                : new Vector3( x * MapGenerator.TILE_X_OFFSET, 0f, ( z + 0.5f ) * MapGenerator.TILE_Z_OFFSET );
        }
    }
}
