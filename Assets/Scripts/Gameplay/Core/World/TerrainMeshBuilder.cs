using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Builds the terrain surface of one chunk (one quad per ground cell plus the grass-lip overhang on
    /// every edge that has a lower or missing neighbour) and the cliff walls below those edges.
    /// Pure geometry: reads a TerrainWindow, returns MeshBuffers.
    /// </summary>
    public static class TerrainMeshBuilder {

        private const float HeightEpsilon = 0.0001f;

        // ---- shared cell rules -------------------------------------------------------------------------

        internal static float? TopY( TerrainWindow window, int gridX, int gridY, float baseTopY, float elevatedRise ) {
            return window.Get( gridX, gridY ).TopY( baseTopY, elevatedRise );
        }

        internal static bool IsSolid( TerrainWindow window, int gridX, int gridY ) {
            return window.Get( gridX, gridY ).Solid;
        }

        /// <summary>True when the neighbour has no top surface, or a lower one.</summary>
        internal static bool NeighborIsLowerOrEmpty( TerrainWindow window, int gridX, int gridY, float myTopY, float baseTopY, float elevatedRise ) {
            var neighbour = TopY( window, gridX, gridY, baseTopY, elevatedRise );
            return neighbour == null || neighbour.Value < myTopY - HeightEpsilon;
        }

        /// <summary>True when the neighbour has no top surface, or one at a different height.</summary>
        internal static bool NeighborDifferent( TerrainWindow window, int gridX, int gridY, float myTopY, float baseTopY, float elevatedRise ) {
            var neighbour = TopY( window, gridX, gridY, baseTopY, elevatedRise );
            if( neighbour == null ) {
                return true;
            }
            return Math.Abs( neighbour.Value - myTopY ) > HeightEpsilon;
        }

        /// <summary>
        /// Whether one corner of a cell's quad may extend its UV toward a diagonal neighbour to blend
        /// with it. False when either of the two AXIS neighbours touching this corner (not the diagonal
        /// one) is missing/non-solid (a real cliff/wall/void edge) or sits at a different height (the
        /// ElevatedRise biome-transition lip): blending across a height step looks wrong regardless of
        /// whether the biome also changes there, so each corner clamps to its own texel independently
        /// rather than the whole cell going flat.
        /// The diagonal neighbour's HEIGHT is deliberately not checked: it doesn't touch either edge
        /// this corner sits on, and blocking on it too would kill blending along a perfectly matching
        /// edge just because some unrelated cell one step further away happens to differ.
        /// Its BIOME is checked, but only to reject it as a lone outlier: the corner's UV sits exactly
        /// at the point where this cell, both axis neighbours and the diagonal one share a texel, so
        /// bilinear sampling there gives the diagonal ~25% weight even at a single point. On a smooth,
        /// gradual boundary that's fine (the diagonal shares a biome with one of the axis neighbours).
        /// But at any "step" of a jagged/staircase boundary - the normal shape of a grid-based biome
        /// edge - the diagonal can be a biome that neither axis neighbour (nor this cell) has anything
        /// to do with; reaching for it there smears a disproportionate, diamond-shaped "ghost" of that
        /// unrelated biome into an otherwise uniform area. So the diagonal is only allowed to contribute
        /// when it shares its biome with this cell or with one of the two axis neighbours.
        /// </summary>
        private static bool CornerCanBlend( TerrainWindow window, int gridX, int gridY, float myTopY, float baseTopY, float elevatedRise, byte myBiome, int dx, int dy ) {
            if( NeighborDifferent( window, gridX + dx, gridY, myTopY, baseTopY, elevatedRise )
                || NeighborDifferent( window, gridX, gridY + dy, myTopY, baseTopY, elevatedRise ) ) {
                return false;
            }

            var diagonal = window.Get( gridX + dx, gridY + dy );
            if( !diagonal.Ground ) {
                return true; // missing diagonal: SplatBuilder's nearest-fill already gives it a sane biome
            }
            var axis1 = window.Get( gridX + dx, gridY );
            var axis2 = window.Get( gridX, gridY + dy );
            return diagonal.Biome == myBiome || diagonal.Biome == axis1.Biome || diagonal.Biome == axis2.Biome;
        }

        // ---- top surface -------------------------------------------------------------------------------

        public static MeshBuffers BuildTop( TerrainWindow window, bool[] cellExists, int chunkOriginX, int chunkOriginY, TerrainVisualParams p ) {
            int chunkSize = p.ChunkSize;
            int texSize = chunkSize + 2;
            float tileSize = p.TileSize;
            float half = tileSize * 0.5f;
            float baseTopY = p.BaseTopY;
            float elevatedRise = p.ElevatedRise;
            float overhang = p.Overhang;
            float overhangDrop = p.OverhangDrop;
            float innerScale = p.InnerOverhangScale;

            int cellCount = chunkSize * chunkSize;
            var mesh = new MeshBuffers( cellCount * 4, cellCount * 6 );
            bool overhangEnabled = overhang > 0.0001f;

            var north = new Float3( 0, 0, 1 );
            var south = new Float3( 0, 0, -1 );
            var east = new Float3( 1, 0, 0 );
            var west = new Float3( -1, 0, 0 );

            for( int cj = 0; cj < chunkSize; cj++ ) {
                for( int ci = 0; ci < chunkSize; ci++ ) {
                    if( !cellExists[ cj * chunkSize + ci ] ) {
                        continue;
                    }

                    int gridX = chunkOriginX + ci;
                    int gridY = chunkOriginY + cj;
                    float cellTopY = TopY( window, gridX, gridY, baseTopY, elevatedRise ) ?? baseTopY;
                    byte cellBiome = window.Get( gridX, gridY ).Biome;

                    float cx = gridX * tileSize;
                    float cz = gridY * tileSize;
                    float x0 = cx - half;
                    float x1 = cx + half;
                    float z0 = cz - half;
                    float z1 = cz + half;

                    // Each corner independently spans toward its diagonal neighbour's texel (so biomes
                    // blend smoothly) unless that would cross a height step, in which case it clamps to
                    // this cell's own texel (see CornerCanBlend).
                    var ownUV = new Float2( ( ci + 1.5f ) / texSize, ( cj + 1.5f ) / texSize );
                    Float2 uvSW = CornerCanBlend( window, gridX, gridY, cellTopY, baseTopY, elevatedRise, cellBiome, -1, -1 )
                        ? new Float2( ( ci + 1.0f ) / texSize, ( cj + 1.0f ) / texSize ) : ownUV;
                    Float2 uvSE = CornerCanBlend( window, gridX, gridY, cellTopY, baseTopY, elevatedRise, cellBiome, 1, -1 )
                        ? new Float2( ( ci + 2.0f ) / texSize, ( cj + 1.0f ) / texSize ) : ownUV;
                    Float2 uvNW = CornerCanBlend( window, gridX, gridY, cellTopY, baseTopY, elevatedRise, cellBiome, -1, 1 )
                        ? new Float2( ( ci + 1.0f ) / texSize, ( cj + 2.0f ) / texSize ) : ownUV;
                    Float2 uvNE = CornerCanBlend( window, gridX, gridY, cellTopY, baseTopY, elevatedRise, cellBiome, 1, 1 )
                        ? new Float2( ( ci + 2.0f ) / texSize, ( cj + 2.0f ) / texSize ) : ownUV;

                    int sw = mesh.AddVertex( new Float3( x0, cellTopY, z0 ), Float3.Up, uvSW );
                    int se = mesh.AddVertex( new Float3( x1, cellTopY, z0 ), Float3.Up, uvSE );
                    int nw = mesh.AddVertex( new Float3( x0, cellTopY, z1 ), Float3.Up, uvNW );
                    int ne = mesh.AddVertex( new Float3( x1, cellTopY, z1 ), Float3.Up, uvNE );

                    mesh.AddTriangle( sw, nw, ne );
                    mesh.AddTriangle( sw, ne, se );

                    if( !overhangEnabled ) {
                        continue;
                    }

                    var outerUV = new Float2( ( ci + 1.5f ) / texSize, ( cj + 1.5f ) / texSize );

                    bool nEmpty = !IsSolid( window, gridX, gridY + 1 );
                    bool sEmpty = !IsSolid( window, gridX, gridY - 1 );
                    bool eEmpty = !IsSolid( window, gridX + 1, gridY );
                    bool wEmpty = !IsSolid( window, gridX - 1, gridY );

                    // A floor tile flattens the neighbour's HEIGHT (docs/BUILDING_SYSTEM_PLAN.md section 9),
                    // but the lip below is still a decorative shelf pushed OUT over that neighbour's own
                    // footprint - exactly where the floor mesh sits. A real cliff step still gets its lip;
                    // a step that only exists because the neighbour was flattened for a floor tile gets a
                    // plain riser instead (still emitted by BuildCliffs), never this horizontal overhang.
                    bool nLower = ( nEmpty || NeighborIsLowerOrEmpty( window, gridX, gridY + 1, cellTopY, baseTopY, elevatedRise ) ) && !window.Get( gridX, gridY + 1 ).Flattened;
                    bool sLower = ( sEmpty || NeighborIsLowerOrEmpty( window, gridX, gridY - 1, cellTopY, baseTopY, elevatedRise ) ) && !window.Get( gridX, gridY - 1 ).Flattened;
                    bool eLower = ( eEmpty || NeighborIsLowerOrEmpty( window, gridX + 1, gridY, cellTopY, baseTopY, elevatedRise ) ) && !window.Get( gridX + 1, gridY ).Flattened;
                    bool wLower = ( wEmpty || NeighborIsLowerOrEmpty( window, gridX - 1, gridY, cellTopY, baseTopY, elevatedRise ) ) && !window.Get( gridX - 1, gridY ).Flattened;

                    // A full lip where the neighbour is missing (map edge, lake), a reduced one on an
                    // internal step between two existing cells.
                    float nOh = nEmpty ? overhang : overhang * innerScale;
                    float nDr = nEmpty ? overhangDrop : overhangDrop * innerScale;
                    float sOh = sEmpty ? overhang : overhang * innerScale;
                    float sDr = sEmpty ? overhangDrop : overhangDrop * innerScale;
                    float eOh = eEmpty ? overhang : overhang * innerScale;
                    float eDr = eEmpty ? overhangDrop : overhangDrop * innerScale;
                    float wOh = wEmpty ? overhang : overhang * innerScale;
                    float wDr = wEmpty ? overhangDrop : overhangDrop * innerScale;

                    var vNW = mesh.Vertices[ nw ];
                    var vNE = mesh.Vertices[ ne ];
                    var vSE = mesh.Vertices[ se ];
                    var vSW = mesh.Vertices[ sw ];

                    if( nLower ) EmitLip( mesh, vNW, vNE, north, nOh, nDr, outerUV );
                    if( eLower ) EmitLip( mesh, vNE, vSE, east, eOh, eDr, outerUV );
                    if( sLower ) EmitLip( mesh, vSE, vSW, south, sOh, sDr, outerUV );
                    if( wLower ) EmitLip( mesh, vSW, vNW, west, wOh, wDr, outerUV );

                    if( nLower && eLower ) {
                        bool outer = nEmpty || eEmpty;
                        EmitCorner( mesh, vNE, north, east, outer ? overhang : overhang * innerScale, outer ? overhangDrop : overhangDrop * innerScale, outerUV );
                    }
                    if( nLower && wLower ) {
                        bool outer = nEmpty || wEmpty;
                        EmitCorner( mesh, vNW, west, north, outer ? overhang : overhang * innerScale, outer ? overhangDrop : overhangDrop * innerScale, outerUV );
                    }
                    if( sLower && eLower ) {
                        bool outer = sEmpty || eEmpty;
                        EmitCorner( mesh, vSE, east, south, outer ? overhang : overhang * innerScale, outer ? overhangDrop : overhangDrop * innerScale, outerUV );
                    }
                    if( sLower && wLower ) {
                        bool outer = sEmpty || wEmpty;
                        EmitCorner( mesh, vSW, south, west, outer ? overhang : overhang * innerScale, outer ? overhangDrop : overhangDrop * innerScale, outerUV );
                    }
                }
            }
            return mesh;
        }

        // A flat strip pushed outwards from an edge plus a short vertical face dropping from its outer rim.
        private static void EmitLip( MeshBuffers mesh, Float3 innerLeft, Float3 innerRight, Float3 outward, float overhang, float drop, Float2 outerUV ) {
            var outOffset = outward * overhang;
            var leftTop = innerLeft + outOffset;
            var rightTop = innerRight + outOffset;
            var leftBot = leftTop - new Float3( 0, drop, 0 );
            var rightBot = rightTop - new Float3( 0, drop, 0 );

            int hLeftInner = mesh.AddVertex( innerLeft, Float3.Up, outerUV );
            int hRightInner = mesh.AddVertex( innerRight, Float3.Up, outerUV );
            int hLeftTop = mesh.AddVertex( leftTop, Float3.Up, outerUV );
            int hRightTop = mesh.AddVertex( rightTop, Float3.Up, outerUV );
            mesh.AddTriangle( hLeftInner, hLeftTop, hRightTop );
            mesh.AddTriangle( hLeftInner, hRightTop, hRightInner );

            int vLeftTop = mesh.AddVertex( leftTop, outward, outerUV );
            int vRightTop = mesh.AddVertex( rightTop, outward, outerUV );
            int vLeftBot = mesh.AddVertex( leftBot, outward, outerUV );
            int vRightBot = mesh.AddVertex( rightBot, outward, outerUV );
            mesh.AddTriangle( vLeftTop, vLeftBot, vRightBot );
            mesh.AddTriangle( vLeftTop, vRightBot, vRightTop );
        }

        // A flat hypotenuse from A to B only chamfers the corner; bulging the fan out to a quarter-circle
        // arc (still capped at `overhang`, same max extent as before) is what actually reads as rounded.
        private const int CornerArcSegments = 3;

        // Fills the gap where two lips meet at a convex corner with a small rounded (arced) fan instead of
        // the sharp diamond-shaped spike a single straight fill would make.
        private static void EmitCorner( MeshBuffers mesh, Float3 innerPos, Float3 outwardA, Float3 outwardB, float overhang, float drop, Float2 outerUV ) {
            var arcTop = new Float3[ CornerArcSegments + 1 ];
            for( int i = 0; i <= CornerArcSegments; i++ ) {
                float angle = ( float ) ( i * ( Math.PI * 0.5 ) / CornerArcSegments );
                var dir = outwardA * ( float ) Math.Cos( angle ) + outwardB * ( float ) Math.Sin( angle );
                arcTop[ i ] = innerPos + dir * overhang;
            }

            int hInner = mesh.AddVertex( innerPos, Float3.Up, outerUV );
            var hArc = new int[ CornerArcSegments + 1 ];
            for( int i = 0; i <= CornerArcSegments; i++ ) {
                hArc[ i ] = mesh.AddVertex( arcTop[ i ], Float3.Up, outerUV );
            }
            for( int i = 0; i < CornerArcSegments; i++ ) {
                mesh.AddTriangle( hInner, hArc[ i ], hArc[ i + 1 ] );
            }

            var dropOffset = new Float3( 0, drop, 0 );
            for( int i = 0; i < CornerArcSegments; i++ ) {
                var top0 = arcTop[ i ];
                var top1 = arcTop[ i + 1 ];
                var bot0 = top0 - dropOffset;
                var bot1 = top1 - dropOffset;
                var faceNormal = ( ( top0 - innerPos ) + ( top1 - innerPos ) ).Normalized();

                int vTop0 = mesh.AddVertex( top0, faceNormal, outerUV );
                int vTop1 = mesh.AddVertex( top1, faceNormal, outerUV );
                int vBot0 = mesh.AddVertex( bot0, faceNormal, outerUV );
                int vBot1 = mesh.AddVertex( bot1, faceNormal, outerUV );
                mesh.AddTriangle( vTop0, vBot0, vBot1 );
                mesh.AddTriangle( vTop0, vBot1, vTop1 );
            }
        }

        // ---- cliffs ------------------------------------------------------------------------------------

        /// <summary>Vertical walls under every edge that borders a lower cell (down to it) or nothing (deep).</summary>
        public static MeshBuffers BuildCliffs( TerrainWindow window, int chunkOriginX, int chunkOriginY, TerrainVisualParams p ) {
            var mesh = new MeshBuffers();
            float tileSize = p.TileSize;
            float half = tileSize * 0.5f;
            float deepBotY = p.BaseTopY - p.CliffDepth;

            for( int cj = 0; cj < p.ChunkSize; cj++ ) {
                for( int ci = 0; ci < p.ChunkSize; ci++ ) {
                    int gridX = chunkOriginX + ci;
                    int gridY = chunkOriginY + cj;
                    var myTopOpt = TopY( window, gridX, gridY, p.BaseTopY, p.ElevatedRise );
                    if( myTopOpt == null ) {
                        continue;
                    }
                    float myTopY = myTopOpt.Value;
                    float cx = gridX * tileSize;
                    float cz = gridY * tileSize;

                    EmitDirectionalCliff( mesh, window, gridX, gridY, gridX, gridY + 1, cx, cz, half, myTopY, deepBotY, p, 0, +1 );
                    EmitDirectionalCliff( mesh, window, gridX, gridY, gridX, gridY - 1, cx, cz, half, myTopY, deepBotY, p, 0, -1 );
                    EmitDirectionalCliff( mesh, window, gridX, gridY, gridX + 1, gridY, cx, cz, half, myTopY, deepBotY, p, +1, 0 );
                    EmitDirectionalCliff( mesh, window, gridX, gridY, gridX - 1, gridY, cx, cz, half, myTopY, deepBotY, p, -1, 0 );
                }
            }
            return mesh;
        }

        private static void EmitDirectionalCliff(
            MeshBuffers mesh, TerrainWindow window, int gridX, int gridY, int neighbourX, int neighbourY,
            float cx, float cz, float half, float myTopY, float deepBotY,
            TerrainVisualParams p, int dirX, int dirZ ) {

            var neighbourTop = TopY( window, neighbourX, neighbourY, p.BaseTopY, p.ElevatedRise );
            if( neighbourTop == null ) {
                // A true deep drop (map edge, coastline): jagged rock.
                EmitWall( mesh, gridX, gridY, cx, cz, half, myTopY, deepBotY, dirX, dirZ, jagged: true );
            }
            else if( neighbourTop.Value < myTopY - HeightEpsilon ) {
                // A shallow step between two existing ground cells (e.g. the grass-mound lip over
                // sand/dirt, `ElevatedRise` ~0.2) - too small a face for "eroded rock" to read as
                // anything but a broken seam, so it stays a flat little riser, same as before.
                EmitWall( mesh, gridX, gridY, cx, cz, half, myTopY, neighbourTop.Value, dirX, dirZ, jagged: false );
            }
        }

        // A rock face is jagged, not a flat rectangle: a few interior columns are pushed in/out along the
        // wall's own normal by a small deterministic hash-based amount (reuses GrassBuilder's position hash).
        // The two end columns are left exactly on the flat rectangle so they still butt up seamlessly against
        // a neighbouring wall segment or a rounded corner fan (see EmitCorner) - only the interior "erodes".
        // Only applied to true deep cliffs (see EmitDirectionalCliff) - shallow internal steps stay flat.
        private const int CliffJitterColumns = 3;
        private const float CliffJitterAmount = 0.06f;

        private static void EmitWall( MeshBuffers mesh, int gridX, int gridY, float cx, float cz, float half, float topY, float botY, int dirX, int dirZ, bool jagged ) {
            var n = new Float3( dirX, 0, dirZ );
            Float3 leftTop, rightTop, leftBot, rightBot;

            if( dirZ > 0 ) {
                leftTop = new Float3( cx - half, topY, cz + half );
                rightTop = new Float3( cx + half, topY, cz + half );
                leftBot = new Float3( cx - half, botY, cz + half );
                rightBot = new Float3( cx + half, botY, cz + half );
            }
            else if( dirZ < 0 ) {
                leftTop = new Float3( cx + half, topY, cz - half );
                rightTop = new Float3( cx - half, topY, cz - half );
                leftBot = new Float3( cx + half, botY, cz - half );
                rightBot = new Float3( cx - half, botY, cz - half );
            }
            else if( dirX > 0 ) {
                leftTop = new Float3( cx + half, topY, cz + half );
                rightTop = new Float3( cx + half, topY, cz - half );
                leftBot = new Float3( cx + half, botY, cz + half );
                rightBot = new Float3( cx + half, botY, cz - half );
            }
            else {
                leftTop = new Float3( cx - half, topY, cz - half );
                rightTop = new Float3( cx - half, topY, cz + half );
                leftBot = new Float3( cx - half, botY, cz - half );
                rightBot = new Float3( cx - half, botY, cz + half );
            }

            if( !jagged ) {
                int flat = mesh.AddVertex( leftTop, n, new Float2( 0, 1 ) );
                mesh.AddVertex( rightTop, n, new Float2( 1, 1 ) );
                mesh.AddVertex( leftBot, n, new Float2( 0, 0 ) );
                mesh.AddVertex( rightBot, n, new Float2( 1, 0 ) );
                mesh.AddTriangle( flat + 0, flat + 2, flat + 3 );
                mesh.AddTriangle( flat + 0, flat + 3, flat + 1 );
                return;
            }

            var prevTop = leftTop;
            var prevBot = leftBot;
            for( int col = 1; col <= CliffJitterColumns; col++ ) {
                float t = ( float ) col / CliffJitterColumns;
                Float3 curTop, curBot;
                if( col == CliffJitterColumns ) {
                    curTop = rightTop;
                    curBot = rightBot;
                }
                else {
                    var baseTop = leftTop + ( rightTop - leftTop ) * t;
                    var baseBot = leftBot + ( rightBot - leftBot ) * t;
                    curTop = baseTop + n * JitterOffset( gridX, gridY, dirX, dirZ, col * 2 );
                    curBot = baseBot + n * JitterOffset( gridX, gridY, dirX, dirZ, col * 2 + 1 );
                }

                int b = mesh.AddVertex( prevTop, n, new Float2( t - 1f / CliffJitterColumns, 1 ) );
                mesh.AddVertex( curTop, n, new Float2( t, 1 ) );
                mesh.AddVertex( prevBot, n, new Float2( t - 1f / CliffJitterColumns, 0 ) );
                mesh.AddVertex( curBot, n, new Float2( t, 0 ) );
                mesh.AddTriangle( b + 0, b + 2, b + 3 );
                mesh.AddTriangle( b + 0, b + 3, b + 1 );

                prevTop = curTop;
                prevBot = curBot;
            }
        }

        private static float JitterOffset( int gridX, int gridY, int dirX, int dirZ, int salt ) {
            uint h = GrassBuilder.Hash( gridX, gridY, dirX, dirZ, salt );
            float unit = h / ( float ) uint.MaxValue;
            return ( unit - 0.5f ) * 2f * CliffJitterAmount;
        }
    }
}
