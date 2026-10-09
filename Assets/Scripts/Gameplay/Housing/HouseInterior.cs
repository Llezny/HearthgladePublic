using System;
using System.Collections.Generic;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Housing
{
    /// <summary>
    /// The inside of a house: a small diorama room that is spawned high above the island when the player goes in and destroyed when they
    /// come out (docs/HOME_ISLAND_PLAN.md). It is also the walk area of the player while they are in it, and it knows which cells of
    /// the build grid its floor is made of (the origin of the room sits half a cell off the grid origin so that the walls fall on cell edges).
    /// </summary>
    public class HouseInterior : MonoBehaviour, IWalkArea
    {
        private const float Cell = MapGenerator.TILE_X_OFFSET;

        // How much of a cell the furniture of the house has to cover for the cell to count as taken.
        private const float TakenFraction = 0.25f;

        [ Tooltip( "Where the player stands after coming in (in front of the door)." ) ]
        [ SerializeField ] private Transform spawnPoint;

        [ Tooltip( "The middle of the floor. A bigger room keeps its north-east corner where the smaller one had it, so its middle is off this object." ) ]
        [ SerializeField ] private Transform floorCentre;

        [ Tooltip( "Half the size of the walkable floor (x east, z north), around the middle of the floor." ) ]
        [ SerializeField ] private Vector2 roomHalfSize = new Vector2( 1.47f, 1.1f );

        [ Tooltip( "How far the player's pivot stays from walls and furniture, in metres." ) ]
        [ SerializeField ] private float playerMargin = 0.12f;

        [ Tooltip( "Trigger boxes of the furniture that is part of the house; only their footprint on the floor is used." ) ]
        [ SerializeField ] private List<BoxCollider> obstacles = new List<BoxCollider>();

        private readonly List<WalkRect> fixedRects = new List<WalkRect>();
        private WalkRect room;
        private InteriorWalkArea area;

        public Transform SpawnPoint => spawnPoint;

        /// <summary>The cells of the floor, from the south-west one to the north-east one (both included).</summary>
        public Vector2Int CellMin { get; private set; }
        public Vector2Int CellMax { get; private set; }

        /// <summary>The cell in front of the door, which furniture must leave free.</summary>
        public Vector2Int EntryCell { get; private set; }

        /// <summary>Height of the floor in the world.</summary>
        public float FloorY => transform.position.y;

        /// <summary>Raised by the door inside when the player taps it.</summary>
        public Action ExitRequested { get; set; }

        private void Awake( )
        {
            var centre = floorCentre != null ? floorCentre.position : transform.position;
            room = new WalkRect( centre.x - roomHalfSize.x, centre.z - roomHalfSize.y, centre.x + roomHalfSize.x, centre.z + roomHalfSize.y );
            foreach ( var obstacle in obstacles )
            {
                if ( obstacle != null )
                {
                    fixedRects.Add( FootprintOf( obstacle ) );
                }
            }
            CellMin = new Vector2Int( Mathf.RoundToInt( room.MinX / Cell + 0.5f ), Mathf.RoundToInt( room.MinZ / Cell + 0.5f ) );
            CellMax = new Vector2Int( Mathf.RoundToInt( room.MaxX / Cell - 0.5f ), Mathf.RoundToInt( room.MaxZ / Cell - 0.5f ) );
            var spawn = spawnPoint != null ? spawnPoint.position : centre;
            EntryCell = new Vector2Int( GridMath.WorldToGrid( spawn.x, Cell ), GridMath.WorldToGrid( spawn.z, Cell ) );
            SetFurniture( null );
        }

        /// <summary>The cells that the furniture of the house itself covers.</summary>
        public IEnumerable<(int, int)> FixedCells( )
        {
            for ( int z = CellMin.y; z <= CellMax.y; z++ )
            {
                for ( int x = CellMin.x; x <= CellMax.x; x++ )
                {
                    var cell = CellRect( x, z, 1, 1 );
                    foreach ( var rect in fixedRects )
                    {
                        float overlapX = Mathf.Min( rect.MaxX, cell.MaxX ) - Mathf.Max( rect.MinX, cell.MinX );
                        float overlapZ = Mathf.Min( rect.MaxZ, cell.MaxZ ) - Mathf.Max( rect.MinZ, cell.MinZ );
                        if ( overlapX > Cell * TakenFraction && overlapZ > Cell * TakenFraction )
                        {
                            yield return ( x, z );
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>The floor area of a block of cells, in the world.</summary>
        public static WalkRect CellRect( int x, int z, int sizeX, int sizeZ )
        {
            return new WalkRect( ( x - 0.5f ) * Cell, ( z - 0.5f ) * Cell, ( x + sizeX - 0.5f ) * Cell, ( z + sizeZ - 0.5f ) * Cell );
        }

        /// <summary>The middle of a block of cells, on the floor.</summary>
        public Vector3 FloorCentre( int x, int z, int sizeX, int sizeZ )
        {
            return new Vector3( ( x + ( sizeX - 1 ) * 0.5f ) * Cell, FloorY, ( z + ( sizeZ - 1 ) * 0.5f ) * Cell );
        }

        /// <summary>Tells the walk area which cells the furniture the player put down covers (solid pieces only: rugs can be walked over).</summary>
        public void SetFurniture( IReadOnlyList<WalkRect> placed )
        {
            var all = new List<WalkRect>( fixedRects );
            if ( placed != null )
            {
                all.AddRange( placed );
            }
            area = new InteriorWalkArea( room, all, playerMargin );
        }

        public bool CanMoveTo( Vector3 from, Vector3 to )
        {
            return area.CanMoveTo( to.x, to.z );
        }

        public void RequestExit( )
        {
            ExitRequested?.Invoke();
        }

        // Read from the box's own numbers, not Collider.bounds: a collider that was just created has no bounds before the next physics step.
        private static WalkRect FootprintOf( BoxCollider box )
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            var half = box.size * 0.5f;
            for ( int i = 0; i < 8; i++ )
            {
                var corner = box.center + new Vector3( ( i & 1 ) == 0 ? -half.x : half.x, ( i & 2 ) == 0 ? -half.y : half.y, ( i & 4 ) == 0 ? -half.z : half.z );
                var world = box.transform.TransformPoint( corner );
                minX = Mathf.Min( minX, world.x );
                maxX = Mathf.Max( maxX, world.x );
                minZ = Mathf.Min( minZ, world.z );
                maxZ = Mathf.Max( maxZ, world.z );
            }
            return new WalkRect( minX, minZ, maxX, maxZ );
        }
    }
}
