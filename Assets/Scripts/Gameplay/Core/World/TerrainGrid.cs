using System;

namespace Hearthglade.Core.World {

    /// <summary>Biome ids as stored in the terrain grid. The Unity-side BiomeId enum mirrors these values.</summary>
    public static class TerrainBiome {
        public const int Grass = 0;
        public const int Dirt = 1;
        public const int Sand = 2;
        public const int Stone = 3;
        public const int Taiga = 4;
        public const int Forest = 5;
        public const int Swamp = 6;
        public const int Tundra = 7;
    }

    /// <summary>What the terrain visuals need to know about one block.</summary>
    public struct TerrainCell {
        /// <summary>A block exists here (ground or not).</summary>
        public bool Present;

        /// <summary>A block exists here and it is ground (not e.g. a water/"buyable" cell).</summary>
        public bool Ground;

        /// <summary>A block exists here and the player may walk on it.</summary>
        public bool Walkable;
        public bool Wall;
        public bool Elevated;

        /// <summary>
        /// True when a floor tile placed here flattens away the <see cref="Elevated"/> visual bump
        /// (docs/BUILDING_SYSTEM_PLAN.md section 9 - a floor tile visually levels the grass/sand height step
        /// under it instead of copying it). Purely a rendering override on this derived grid: never touches
        /// Ground/Walkable/Biome, and never touches the block's own <c>BlockSO.IsElevated</c> data.
        /// </summary>
        public bool Flattened;
        public byte Biome;

        /// <summary>Ground that is not a wall: the cells that get a top surface, cliffs and grass.</summary>
        public bool Solid => Ground && !Wall;

        /// <summary>Height of the top surface, or null when the cell has none (not solid).</summary>
        public float? TopY( float baseTopY, float elevatedRise ) {
            if( !Solid ) {
                return null;
            }
            return baseTopY + ( Elevated && !Flattened ? elevatedRise : 0f );
        }
    }

    /// <summary>
    /// Compact map-wide terrain description (a byte per cell) that chunk visuals are computed from, instead of
    /// looking blocks up one by one through GameObjects. Written on the main thread as blocks are created.
    /// </summary>
    public sealed class TerrainGrid {

        private const byte GroundFlag = 1;
        private const byte WallFlag = 2;
        private const byte ElevatedFlag = 4;
        private const byte PresentFlag = 8;
        private const byte WalkableFlag = 16;
        private const byte FlattenedFlag = 32;

        private readonly byte[] flags;
        private readonly byte[] biomes;

        public readonly int Size;

        public TerrainGrid( int size ) {
            if( size < 0 ) {
                throw new ArgumentOutOfRangeException( nameof( size ) );
            }
            Size = size;
            flags = new byte[ size * size ];
            biomes = new byte[ size * size ];
        }

        public bool InBounds( int x, int y ) {
            return x >= 0 && y >= 0 && x < Size && y < Size;
        }

        public void Set( int x, int y, TerrainCell cell ) {
            if( !InBounds( x, y ) ) {
                throw new ArgumentOutOfRangeException( $"Cell ({x}, {y}) is outside a {Size}x{Size} grid" );
            }
            flags[ y * Size + x ] = Pack( cell );
            biomes[ y * Size + x ] = cell.Biome;
        }

        /// <summary>Cells outside the grid, and cells never set, are empty.</summary>
        public TerrainCell Get( int x, int y ) {
            if( !InBounds( x, y ) ) {
                return default;
            }
            int i = y * Size + x;
            return Unpack( flags[ i ], biomes[ i ] );
        }

        /// <summary>
        /// Sets or clears only the <see cref="TerrainCell.Flattened"/> bit of a cell, leaving every other flag
        /// and the biome untouched - used to flatten the grass/sand visual bump under a placed floor tile
        /// (docs/BUILDING_SYSTEM_PLAN.md section 9) without touching the block data it was derived from.
        /// A no-op on a cell that was never set (there is nothing to flatten there yet).
        /// </summary>
        public void SetFlattened( int x, int y, bool flattened ) {
            if( !InBounds( x, y ) ) {
                throw new ArgumentOutOfRangeException( $"Cell ({x}, {y}) is outside a {Size}x{Size} grid" );
            }
            int i = y * Size + x;
            flags[ i ] = flattened ? ( byte ) ( flags[ i ] | FlattenedFlag ) : ( byte ) ( flags[ i ] & ~FlattenedFlag );
        }

        private readonly System.Collections.Generic.List<GrassClearZone> grassClearZones = new System.Collections.Generic.List<GrassClearZone>();

        /// <summary>Registers an area building pieces keep grass out of; chunk visuals must be invalidated by the caller.</summary>
        public void AddGrassClearZone( GrassClearZone zone ) {
            grassClearZones.Add( zone );
        }

        /// <summary>
        /// Copies the square of cells [originX, originX + size) x [originY, originY + size) so a worker thread
        /// can read a stable snapshot while the main thread keeps changing the grid.
        /// </summary>
        public TerrainWindow CopyWindow( int originX, int originY, int size ) {
            var window = new TerrainWindow( originX, originY, size );
            for( int y = 0; y < size; y++ ) {
                for( int x = 0; x < size; x++ ) {
                    window.Set( x, y, Get( originX + x, originY + y ) );
                }
            }
            // A tuft near the window edge belongs to this chunk but its zone may start in the next one.
            foreach( var zone in grassClearZones ) {
                if( zone.Overlaps( originX - 1f, originY - 1f, originX + size, originY + size, 1f ) ) {
                    window.AddGrassClearZone( zone );
                }
            }
            return window;
        }

        internal static TerrainCell Unpack( byte flag, byte biome ) {
            return new TerrainCell {
                Present = ( flag & PresentFlag ) != 0,
                Ground = ( flag & GroundFlag ) != 0,
                Walkable = ( flag & WalkableFlag ) != 0,
                Wall = ( flag & WallFlag ) != 0,
                Elevated = ( flag & ElevatedFlag ) != 0,
                Flattened = ( flag & FlattenedFlag ) != 0,
                Biome = biome
            };
        }

        internal static byte Pack( TerrainCell cell ) {
            return ( byte ) ( ( cell.Present ? PresentFlag : 0 ) | ( cell.Ground ? GroundFlag : 0 ) | ( cell.Walkable ? WalkableFlag : 0 )
                            | ( cell.Wall ? WallFlag : 0 ) | ( cell.Elevated ? ElevatedFlag : 0 ) | ( cell.Flattened ? FlattenedFlag : 0 ) );
        }
    }

    /// <summary>An immutable-by-convention square copy of part of the grid, addressed with global cell coordinates.</summary>
    public sealed class TerrainWindow {

        private readonly byte[] flags;
        private readonly byte[] biomes;

        public readonly int OriginX;
        public readonly int OriginY;
        public readonly System.Collections.Generic.List<GrassClearZone> GrassClearZones = new System.Collections.Generic.List<GrassClearZone>();

        public void AddGrassClearZone( GrassClearZone zone ) {
            GrassClearZones.Add( zone );
        }

        public readonly int Size;

        public TerrainWindow( int originX, int originY, int size ) {
            OriginX = originX;
            OriginY = originY;
            Size = size;
            flags = new byte[ size * size ];
            biomes = new byte[ size * size ];
        }

        /// <summary>Sets a cell by window-local coordinates (0..Size-1).</summary>
        public void Set( int localX, int localY, TerrainCell cell ) {
            flags[ localY * Size + localX ] = TerrainGrid.Pack( cell );
            biomes[ localY * Size + localX ] = cell.Biome;
        }

        /// <summary>Cell by global coordinates; anything outside the window is empty.</summary>
        public TerrainCell Get( int gridX, int gridY ) {
            int x = gridX - OriginX;
            int y = gridY - OriginY;
            if( x < 0 || y < 0 || x >= Size || y >= Size ) {
                return default;
            }
            return TerrainGrid.Unpack( flags[ y * Size + x ], biomes[ y * Size + x ] );
        }
    }
}
