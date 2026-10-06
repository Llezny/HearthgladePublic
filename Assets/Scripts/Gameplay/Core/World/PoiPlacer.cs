using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>What a kind of point of interest (an abandoned camp, ruins, ...) needs from its site.</summary>
    public struct PoiRule {

        /// <summary>How many sites of this kind the map gets (a hash of the seed picks a number in between); the terrain may allow fewer.</summary>
        public int MinCount;
        public int MaxCount;

        /// <summary>Every cell within this many cells of the centre must be walkable ground, free and connected to the start; nothing else grows there.</summary>
        public float ClearRadius;

        /// <summary>Biome indices (of the map's biome list) the centre may lie in; null or empty = any ground.</summary>
        public int[] Biomes;

        public float MinStartDistance;

        /// <summary>0 = no upper limit.</summary>
        public float MaxStartDistance;

        /// <summary>Least distance between the centres of this site and any other site.</summary>
        public float MinSpacing;

        /// <summary>Near water means a liquid cell within <see cref="ResourceField.WaterRadius"/> cells of the centre; the whole clear disc must be ground, so this needs a ClearRadius below that.</summary>
        public WaterCondition Water;

        /// <summary>
        /// A hand-composed site: exactly one, at a fixed offset from the player's start and with a fixed rotation. Count, biome,
        /// distance, spacing and water rules are ignored; the centre must be ground connected to the start, the clear disc
        /// only keeps resources away (a harbour may reach into the sea).
        /// </summary>
        public bool Anchored;

        /// <summary>Anchored sites: cells from the player's start (x east, z north) to the centre.</summary>
        public int AnchorX, AnchorZ;

        /// <summary>Anchored sites: degrees around the vertical axis.</summary>
        public float AnchorRotation;

        /// <summary>Ground painted around the site (paths, plazas); null = none.</summary>
        public PoiPathSegment[] Paths;
    }

    /// <summary>A stripe of painted ground, in cells from the centre of the site before the site is turned. From == To paints a disc.</summary>
    public struct PoiPathSegment {
        public float FromX, FromZ, ToX, ToZ;

        /// <summary>Cells within this distance of the segment are painted.</summary>
        public float HalfWidth;

        /// <summary>Terrain biome id (<c>BiomeId</c>) the ground looks like.</summary>
        public byte Look;
    }

    public struct PoiPlacement {

        /// <summary>Index into the rules list.</summary>
        public int Type;
        public int CellX;
        public int CellY;

        /// <summary>Degrees around the vertical axis; the site's pieces are turned by it.</summary>
        public float RotationY;

        /// <summary>Distance from the player's start in cells: the farther, the better the loot.</summary>
        public float StartDistance;
    }

    /// <summary>
    /// Picks the sites of points of interest on a generated map. Deterministic for a seed and a terrain. Every site it
    /// places is reserved in the <see cref="ResourceField"/>, so plan resources (<see cref="ResourcePlanner"/>) after placing sites.
    /// </summary>
    public static class PoiPlacer {

        private const int OrderChannel = 700;
        private const int CountChannel = 800;
        private const int RotationChannel = 900;

        public static List<PoiPlacement> Place( ResourceField field, IReadOnlyList<PoiRule> rules, int seed ) {
            if( field == null ) {
                throw new ArgumentNullException( nameof( field ) );
            }
            var placed = new List<PoiPlacement>();
            if( rules == null || rules.Count == 0 ) {
                return placed;
            }

            int size = field.Size;
            // A site must be reachable on foot from the start, or its loot would be out of the player's reach.
            var reachable = GridFlood.ReachableFrom( size, field.StartX, field.StartZ, ( x, z ) => field.IsGround( x, z ) );

            // Anchored sites first: they take their fixed place before anything is picked at random around them.
            for( int type = 0; type < rules.Count; type++ ) {
                if( rules[ type ].Anchored ) {
                    PlaceAnchored( field, rules[ type ], type, reachable, placed );
                }
            }

            for( int type = 0; type < rules.Count; type++ ) {
                var rule = rules[ type ];
                if( rule.Anchored ) {
                    continue;
                }
                int max = Math.Max( rule.MinCount, rule.MaxCount );
                int wanted = rule.MinCount + ( int ) ( SeedMixer.Mix( ( uint ) SeedMixer.Derive( seed, CountChannel + type ), 1u ) % ( uint ) ( max - rule.MinCount + 1 ) );
                if( wanted <= 0 ) {
                    continue;
                }

                // Try the cells in a seed-dependent order and take the first ones that fit.
                var cells = new int[ size * size ];
                var keys = new uint[ cells.Length ];
                int typeSeed = SeedMixer.Derive( seed, OrderChannel + type );
                for( int i = 0; i < cells.Length; i++ ) {
                    cells[ i ] = i;
                    keys[ i ] = SeedMixer.Mix( SeedMixer.Mix( ( uint ) typeSeed, ( uint ) ( i % size ) ), ( uint ) ( i / size ) );
                }
                Array.Sort( keys, cells );

                int found = 0;
                foreach( int cell in cells ) {
                    int x = cell % size, z = cell / size;
                    if( !Fits( field, rule, type, rules, placed, reachable, x, z ) ) {
                        continue;
                    }
                    float rotation = ( SeedMixer.Mix( ( uint ) SeedMixer.Derive( seed, RotationChannel + type, x ), ( uint ) z ) >> 8 ) * ( 360f / 16777216f );
                    placed.Add( new PoiPlacement { Type = type, CellX = x, CellY = z, RotationY = rotation, StartDistance = field.ContextAt( x, z ).StartDistance } );
                    field.Reserve( x, z, rule.ClearRadius );
                    if( ++found >= wanted ) {
                        break;
                    }
                }
            }
            return placed;
        }

        private static void PlaceAnchored( ResourceField field, in PoiRule rule, int type, bool[] reachable, List<PoiPlacement> placed ) {
            int x = field.StartX + rule.AnchorX, z = field.StartZ + rule.AnchorZ;
            if( !field.IsGround( x, z ) || !reachable[ z * field.Size + x ] ) {
                return;
            }
            placed.Add( new PoiPlacement { Type = type, CellX = x, CellY = z, RotationY = rule.AnchorRotation, StartDistance = field.ContextAt( x, z ).StartDistance } );
            field.Reserve( x, z, rule.ClearRadius );
        }

        private static bool Fits( ResourceField field, in PoiRule rule, int type, IReadOnlyList<PoiRule> rules, List<PoiPlacement> placed, bool[] reachable, int x, int z ) {
            int size = field.Size;
            if( !field.IsGround( x, z ) || !reachable[ z * size + x ] ) {
                return false;
            }
            if( rule.Biomes != null && rule.Biomes.Length > 0 && Array.IndexOf( rule.Biomes, field.BiomeAt( x, z ) ) < 0 ) {
                return false;
            }
            var context = field.ContextAt( x, z );
            if( context.StartDistance < rule.MinStartDistance || ( rule.MaxStartDistance > 0f && context.StartDistance > rule.MaxStartDistance ) ) {
                return false;
            }
            if( ( rule.Water == WaterCondition.NearWater && !context.NearWater ) || ( rule.Water == WaterCondition.AwayFromWater && context.NearWater ) ) {
                return false;
            }
            foreach( var other in placed ) {
                float dx = other.CellX - x, dz = other.CellY - z;
                float spacing = Math.Max( rule.MinSpacing, rules[ other.Type ].MinSpacing );
                if( dx * dx + dz * dz < spacing * spacing ) {
                    return false;
                }
            }

            int reach = ( int ) Math.Ceiling( rule.ClearRadius );
            float radiusSquared = rule.ClearRadius * rule.ClearRadius;
            for( int cz = z - reach; cz <= z + reach; cz++ ) {
                for( int cx = x - reach; cx <= x + reach; cx++ ) {
                    float dx = cx - x, dz = cz - z;
                    if( dx * dx + dz * dz > radiusSquared ) {
                        continue;
                    }
                    if( !field.IsGround( cx, cz ) || field.IsReserved( cx, cz ) || !reachable[ cz * size + cx ] ) {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
