using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Scatters grass tufts (single flat alpha-cutout cards, clump texture from GrassTufts.mat) over the
    /// ground cells of one chunk as a single merged mesh. The card is not a dynamic per-frame billboard -
    /// the gameplay camera's rotation is fixed (FollowCamera only translates), so every tuft is built once
    /// standing vertically and yawed to face the camera's fixed horizontal direction (see
    /// TerrainVisualParams.GrassCameraForward). A billboard-cross (two crossed quads) was tried first to look
    /// passable from any angle, but with a near-isometric camera that never rotates, a single camera-facing
    /// card looks better and is half the geometry. Density follows a smooth noise mask (bare patches), and
    /// every tuft's position comes from a hash of (chunk, cell, index), so a chunk always grows exactly the
    /// same grass.
    /// </summary>
    public static class GrassBuilder {

        public sealed class GrassMesh {
            public MeshBuffers Mesh;

            /// <summary>Tufts are tiny and numerous, so bounds are set from the chunk footprint, not the vertices.</summary>
            public Float3 BoundsCenter;
            public Float3 BoundsSize;
        }

        /// <summary>Whole chunk as one mesh. Returns null when the chunk gets no grass at all.</summary>
        public static GrassMesh Build( TerrainWindow window, int chunkX, int chunkY, TerrainVisualParams p ) {
            return BuildRegion( window, chunkX, chunkY, p, 0, p.ChunkSize, 0, p.ChunkSize );
        }

        /// <summary>
        /// The chunk split into square blocks of about <see cref="TerrainVisualParams.GrassCullCellSize"/> tiles,
        /// one mesh each, so the camera frustum culls grass at block rather than chunk granularity (a chunk is
        /// much bigger than what the fixed gameplay camera sees). Tuft placement depends only on the absolute
        /// cell, so the grass is identical to <see cref="Build"/>. Null when the chunk gets no grass at all.
        /// </summary>
        public static GrassMesh[] BuildCells( TerrainWindow window, int chunkX, int chunkY, TerrainVisualParams p ) {
            int size = p.ChunkSize;
            int pieces = Math.Max( 1, ( size + Math.Max( 1, p.GrassCullCellSize ) - 1 ) / Math.Max( 1, p.GrassCullCellSize ) );
            var cells = new System.Collections.Generic.List<GrassMesh>( );
            for( int pj = 0; pj < pieces; pj++ ) {
                for( int pi = 0; pi < pieces; pi++ ) {
                    var cell = BuildRegion( window, chunkX, chunkY, p, pi * size / pieces, ( pi + 1 ) * size / pieces, pj * size / pieces, ( pj + 1 ) * size / pieces );
                    if( cell != null ) {
                        cells.Add( cell );
                    }
                }
            }
            return cells.Count == 0 ? null : cells.ToArray( );
        }

        /// <summary>Cells ci0..ci1-1 / cj0..cj1-1 (chunk-local) of the chunk; null when they get no grass.</summary>
        private static GrassMesh BuildRegion( TerrainWindow window, int chunkX, int chunkY, TerrainVisualParams p, int ci0, int ci1, int cj0, int cj1 ) {
            if( !p.EnableGrass ) {
                return null;
            }

            int maxTufts = 0;
            for( int biome = 0; biome < p.GrassByBiome.Length; biome++ ) {
                maxTufts = Math.Max( maxTufts, p.GrassByBiome[ biome ].TuftsPerTile );
            }
            if( maxTufts == 0 ) {
                return null;
            }

            int chunkSize = p.ChunkSize;
            int originX = chunkX * chunkSize;
            int originY = chunkY * chunkSize;
            float tileSize = p.TileSize;
            float tuftHalfWidth = p.GrassWidth * 0.5f;
            float noiseThreshold = p.GrassNoiseThreshold;
            float noiseRange = Math.Max( 0.001f, 1f - noiseThreshold );
            var noise = new GradientNoise( p.GrassNoiseSeed );

            // Cards stand vertically (Up) and yaw to face the camera's fixed horizontal direction - only
            // the horizontal component of GrassCameraForward matters, its downward pitch is irrelevant to
            // a vertical card's facing.
            var camForward = p.GrassCameraForward;
            var facing = new Float3( camForward.X, 0f, camForward.Z ).Normalized();
            if( facing.X == 0f && facing.Z == 0f ) {
                facing = new Float3( 0f, 0f, 1f );
            }
            var cardNormal = facing * -1f;
            var cardRight = Float3.Cross( Float3.Up, facing ).Normalized();

            int regionCells = ( ci1 - ci0 ) * ( cj1 - cj0 );
            var mesh = new MeshBuffers( regionCells * maxTufts * 4, regionCells * maxTufts * 6 );

            for( int cj = cj0; cj < cj1; cj++ ) {
                for( int ci = ci0; ci < ci1; ci++ ) {
                    int gridX = originX + ci;
                    int gridY = originY + cj;

                    var cell = window.Get( gridX, gridY );
                    if( !cell.Solid ) {
                        continue;
                    }
                    // A floor tile flattens (docs/BUILDING_SYSTEM_PLAN.md section 9) AND clears the grass on
                    // its own cell - tufts sticking up through a placed floor would look broken regardless of
                    // the (now suppressed) height bump.
                    if( cell.Flattened ) {
                        continue;
                    }
                    var style = p.GrassFor( cell.Biome );
                    if( style.TuftsPerTile <= 0 ) {
                        continue;
                    }

                    float topY = cell.TopY( p.BaseTopY, p.ElevatedRise ).Value;

                    // No grass on cells next to a step or an edge: it would poke through the overhang.
                    if( TerrainMeshBuilder.NeighborDifferent( window, gridX + 1, gridY, topY, p.BaseTopY, p.ElevatedRise ) ) continue;
                    if( TerrainMeshBuilder.NeighborDifferent( window, gridX - 1, gridY, topY, p.BaseTopY, p.ElevatedRise ) ) continue;
                    if( TerrainMeshBuilder.NeighborDifferent( window, gridX, gridY + 1, topY, p.BaseTopY, p.ElevatedRise ) ) continue;
                    if( TerrainMeshBuilder.NeighborDifferent( window, gridX, gridY - 1, topY, p.BaseTopY, p.ElevatedRise ) ) continue;

                    // Nor next to a floor tile, even when it happens to sit at the same height (a floor on
                    // non-elevated ground has no height step to trip the check above): a tuft's margin/width
                    // jitter and its fixed camera-facing card direction (not grid-aligned) can otherwise still
                    // paint a blade over the neighbouring floor's edge (docs/BUILDING_SYSTEM_PLAN.md section 9).
                    if( window.Get( gridX + 1, gridY ).Flattened ) continue;
                    if( window.Get( gridX - 1, gridY ).Flattened ) continue;
                    if( window.Get( gridX, gridY + 1 ).Flattened ) continue;
                    if( window.Get( gridX, gridY - 1 ).Flattened ) continue;

                    double raw = noise.Octaves( gridX * ( double ) p.GrassNoiseScale, gridY * ( double ) p.GrassNoiseScale, 3, 2.0, 0.5 );
                    float n01 = ( float ) ( ( raw + 1.0 ) * 0.5 );
                    if( n01 < noiseThreshold ) {
                        continue;
                    }

                    float remapped = ( n01 - noiseThreshold ) / noiseRange;
                    float curve = ( float ) Math.Pow( Clamp01( remapped ), p.GrassNoiseExponent );
                    int count = ( int ) Math.Round( curve * style.TuftsPerTile );
                    if( count <= 0 ) {
                        continue;
                    }

                    float cx = gridX * tileSize;
                    float cz = gridY * tileSize;

                    // Alpha is the wind-sway weight: 0 at the base, 255 at the tip.
                    var tintBase = Scale( style.Tint, p.GrassBaseDarken, 0 );
                    var tintTip = Scale( style.TipLighten, p.GrassTipBrighten, 255 );

                    for( int k = 0; k < count; k++ ) {
                        uint h = Hash( chunkX, chunkY, gridX, gridY, k );
                        float u = ( h & 0xFFFFu ) / 65535f;
                        float v = ( ( h >> 16 ) & 0xFFFFu ) / 65535f;

                        // NOTE: deriving further randoms via Frac(h * someConstant) (as this used to do) is
                        // broken for ~99.6% of hashes: h is a uint up to ~4.3 billion, and once h * constant
                        // exceeds float32's ~16.7M integer-precision ceiling, the product has no representable
                        // fractional bits left at all - x - floor(x) is exactly 0.0, not a pseudo-random
                        // fraction. That collapsed roll/height/width/color jitter to their minimum value for
                        // almost every tuft (visible as "all tufts the same length"). Fix: mask each extra
                        // hash's bits down to a small range first (like u/v already did correctly), using a
                        // separately-salted hash per property for independence.
                        uint hHeight = Hash( chunkX, chunkY, gridX, gridY, k + 20011 );
                        uint hWidth = Hash( chunkX, chunkY, gridX, gridY, k + 30013 );
                        uint hVariant = Hash( chunkX, chunkY, gridX, gridY, k + 50021 );

                        // Variant picks one atlas column, the top bit mirrors it for free extra variety.
                        int variantCount = Math.Max( 1, p.GrassVariants );
                        int variant = ( int ) ( ( hVariant & 0xFFFFu ) % ( uint ) variantCount );
                        bool mirror = ( hVariant & 0x10000u ) != 0;
                        float u0 = variant / ( float ) variantCount;
                        float u1 = ( variant + 1 ) / ( float ) variantCount;
                        if( mirror ) {
                            float swap = u0;
                            u0 = u1;
                            u1 = swap;
                        }

                        // Height and width vary independently so some tufts read as tall-and-thin or
                        // short-and-wide, not just uniformly bigger/smaller clones of each other. GrassHeight
                        // is the tall end (short variants go down to 40% of it), not the middle of the range.
                        float heightRand = 0.4f + 0.6f * ( ( hHeight & 0xFFFFu ) / 65535f );
                        float widthRand = 0.75f + 0.5f * ( ( hWidth & 0xFFFFu ) / 65535f );
                        float px = cx + ( u - 0.5f ) * tileSize * p.GrassTileMargin;
                        float pz = cz + ( v - 0.5f ) * tileSize * p.GrassTileMargin;
                        float height = p.GrassHeight * heightRand;
                        float halfW = tuftHalfWidth * widthRand;

                        // Pieces whose recipe clears grass take out every tuft their zone touches, per tuft rather
                        // than per cell, so the cleared patch follows the piece (and its radius) instead of the grid.
                        if( IsInClearZone( window, px / tileSize, pz / tileSize, halfW / tileSize ) ) {
                            continue;
                        }

                        EmitTuft( mesh, px, topY, pz, halfW, height, cardRight, cardNormal, tintBase, tintTip, u0, u1 );
                    }
                }
            }

            if( mesh.IsEmpty ) {
                return null;
            }

            // Tufts are tiny and numerous, so bounds come from the region's footprint, not the vertices. A
            // cell's tufts sit at gridX * tileSize (its centre); they reach at most half a tile beyond that
            // (GrassTileMargin <= 1) plus half a card (widest is 1.25x), plus a little for wind sway.
            float reach = p.GrassWidth * 1.25f + 0.2f;
            float centerX = ( originX + ( ci0 + ci1 - 1 ) * 0.5f ) * tileSize;
            float centerZ = ( originY + ( cj0 + cj1 - 1 ) * 0.5f ) * tileSize;
            float tuftTop = p.GrassHeight + p.ElevatedRise;
            return new GrassMesh {
                Mesh = mesh,
                BoundsCenter = new Float3( centerX, p.BaseTopY + tuftTop * 0.5f, centerZ ),
                BoundsSize = new Float3( ( ci1 - ci0 ) * tileSize + reach, tuftTop + 0.2f, ( cj1 - cj0 ) * tileSize + reach )
            };
        }

        private static bool IsInClearZone( TerrainWindow window, float cellX, float cellZ, float cardHalfWidthCells ) {
            var zones = window.GrassClearZones;
            for( int i = 0; i < zones.Count; i++ ) {
                if( zones[ i ].Touches( cellX, cellZ, cardHalfWidthCells ) ) {
                    return true;
                }
            }
            return false;
        }

        // One flat card per tuft, standing vertically (up) and facing the camera's fixed direction (right
        // x up = -normal). The GrassTufts shader carves the clump silhouette out of this quad via its UV
        // (v=0 base, v=1 tip) and the material's clump texture.
        private static void EmitTuft( MeshBuffers mesh, float px, float py, float pz, float halfWidth, float height,
            Float3 right, Float3 normal, Rgba32 baseColor, Rgba32 tipColor, float u0, float u1 ) {

            var basePos = new Float3( px, py, pz );
            var baseL = basePos - right * halfWidth;
            var baseR = basePos + right * halfWidth;
            var tip = basePos + Float3.Up * height;
            var tipR = tip + right * halfWidth;
            var tipL = tip - right * halfWidth;

            int b = mesh.Vertices.Count;
            mesh.Vertices.Add( baseL );
            mesh.Vertices.Add( baseR );
            mesh.Vertices.Add( tipR );
            mesh.Vertices.Add( tipL );
            mesh.Normals.Add( normal );
            mesh.Normals.Add( normal );
            mesh.Normals.Add( normal );
            mesh.Normals.Add( normal );
            mesh.Uvs.Add( new Float2( u0, 0f ) );
            mesh.Uvs.Add( new Float2( u1, 0f ) );
            mesh.Uvs.Add( new Float2( u1, 1f ) );
            mesh.Uvs.Add( new Float2( u0, 1f ) );
            mesh.Colors.Add( baseColor );
            mesh.Colors.Add( baseColor );
            mesh.Colors.Add( tipColor );
            mesh.Colors.Add( tipColor );
            mesh.AddTriangle( b, b + 2, b + 1 );
            mesh.AddTriangle( b, b + 3, b + 2 );
        }

        public static uint Hash( int chunkX, int chunkY, int cellX, int cellY, int k ) {
            unchecked {
                uint h = 2166136261u;
                h = ( h ^ ( uint ) chunkX ) * 16777619u;
                h = ( h ^ ( uint ) chunkY ) * 16777619u;
                h = ( h ^ ( uint ) cellX ) * 16777619u;
                h = ( h ^ ( uint ) cellY ) * 16777619u;
                h = ( h ^ ( uint ) k ) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return h;
            }
        }

        private static float Clamp01( float x ) {
            return x < 0f ? 0f : ( x > 1f ? 1f : x );
        }

        private static byte ToByte( float channel ) {
            int value = ( int ) Math.Round( channel * 255f );
            return ( byte ) ( value < 0 ? 0 : ( value > 255 ? 255 : value ) );
        }

        private static Rgba32 Scale( Float3 color, float factor, byte alpha ) {
            return new Rgba32( ToByte( color.X * factor ), ToByte( color.Y * factor ), ToByte( color.Z * factor ), alpha );
        }
    }
}
