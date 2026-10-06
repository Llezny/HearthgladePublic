using System;

namespace Hearthglade.Core.World {

    /// <summary>Everything the main thread needs to turn one chunk's terrain into Unity objects.</summary>
    public sealed class ChunkVisualData {
        public int ChunkX;
        public int ChunkY;

        /// <summary>False when the chunk has no ground cell at all; then nothing has to be created.</summary>
        public bool HasTerrain;

        public SplatResult Splat;
        public MeshBuffers Top;

        /// <summary>Null when the chunk has no cliffs or cliffs are disabled.</summary>
        public MeshBuffers Cliffs;

        /// <summary>One mesh per block of tiles (see GrassBuilder.BuildCells); null when the chunk has no grass or grass is disabled.</summary>
        public GrassBuilder.GrassMesh[] Grass;
    }

    public static class ChunkVisualComputer {

        /// <summary>
        /// The window must cover the chunk plus a margin of <see cref="WindowMargin"/> cells around it (see
        /// <see cref="WindowOrigin"/> and <see cref="WindowSize"/>): neighbours decide seams, cliffs, overhangs
        /// and how far the biome blend is sampled. Pure and thread-safe.
        /// </summary>
        public static ChunkVisualData Compute( TerrainWindow window, int chunkX, int chunkY, TerrainVisualParams p ) {
            int originX = chunkX * p.ChunkSize;
            int originY = chunkY * p.ChunkSize;

            var data = new ChunkVisualData { ChunkX = chunkX, ChunkY = chunkY };
            data.Splat = SplatBuilder.Build( window, originX, originY, p.ChunkSize, Math.Max( 0, p.BiomeBlendRadius ) );
            if( !data.Splat.AnyGround ) {
                return data;
            }
            data.HasTerrain = true;
            data.Top = TerrainMeshBuilder.BuildTop( window, data.Splat.CellExists, originX, originY, p );

            if( p.EnableCliffs ) {
                var cliffs = TerrainMeshBuilder.BuildCliffs( window, originX, originY, p );
                data.Cliffs = cliffs.IsEmpty ? null : cliffs;
            }
            data.Grass = GrassBuilder.BuildCells( window, chunkX, chunkY, p );
            return data;
        }

        /// <summary>First cell (per axis) of the window a chunk needs.</summary>
        public static int WindowOrigin( int chunk, int chunkSize, int margin = 1 ) => chunk * chunkSize - margin;

        public static int WindowSize( int chunkSize, int margin = 1 ) => chunkSize + margin * 2;

        /// <summary>Margin (cells per side beyond the chunk) the window needs: 1 for seams/cliffs/overhangs, plus the biome blend radius.</summary>
        public static int WindowMargin( TerrainVisualParams p ) => 1 + Math.Max( 0, p.BiomeBlendRadius );
    }
}
