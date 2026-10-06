using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>Where a resource may grow relative to water (a liquid biome within <see cref="ResourceField.WaterRadius"/> cells).</summary>
    public enum WaterCondition : byte {
        Any = 0,
        NearWater = 1,
        AwayFromWater = 2
    }

    /// <summary>One spawnable resource kind of a biome, as configured in the biome asset.</summary>
    public struct ResourceRule {
        public int Count;
        public float Probability;
        public float SpawnRange;
        public float MinRotation;
        public float MaxRotation;

        /// <summary>
        /// The probability is multiplied by a smooth ramp of the density field (0..1): 0 below <see cref="DensityFrom"/>,
        /// 1 above <see cref="DensityTo"/>. From above To gives the opposite ramp (clearings). Equal values = ignore density.
        /// </summary>
        public float DensityFrom;
        public float DensityTo;

        public WaterCondition Water;

        /// <summary>The resource does not grow closer to the start than this many cells (0 = anywhere).</summary>
        public float MinStartDistance;

        /// <summary>Over this many further cells the probability rises from 0 to full (0 = a hard step).</summary>
        public float StartDistanceRamp;

        /// <summary>
        /// Above 0 the rule makes deposits instead of single spawns: the map is cut into squares of this many cells,
        /// each holds a deposit with <see cref="Probability"/>, and a deposit is <see cref="ClusterMin"/>..<see cref="ClusterMax"/>
        /// objects scattered within <see cref="ClusterRadius"/> cells of its centre. <see cref="Count"/> and
        /// <see cref="SpawnRange"/> are not used.
        /// </summary>
        public int ClusterSpacing;
        public int ClusterMin;
        public int ClusterMax;
        public float ClusterRadius;

        /// <summary>
        /// Objects of this rule keep at least this many cells from each other (0 = they may overlap). Enforced by
        /// <see cref="ResourcePlanner"/>, not by the roller, because it needs to know the neighbouring cells.
        /// </summary>
        public float MinSpacing;

        /// <summary>
        /// The resource only grows where the climate suits it: full chance inside the temperature and humidity ranges (world scale
        /// -1..1), falling linearly to 0 over <see cref="ResourceRoller.ClimateMargin"/> outside. Off unless set.
        /// </summary>
        public bool UseClimate;
        public float TemperatureFrom, TemperatureTo;
        public float HumidityFrom, HumidityTo;

        /// <summary>
        /// Above 0 the rule has its own patch field (see <see cref="ResourceField.PatchAt"/>): it grows only in patches of about this
        /// many cells (lattice unit of the noise) that together cover <see cref="PatchCoverage"/> of the map, so each kind forms
        /// its own thickets instead of being sprinkled evenly. The chance inside a patch is the rule's own.
        /// </summary>
        public float PatchScale;
        public float PatchCoverage;
    }

    /// <summary>What the map knows about one cell that resource rules may depend on.</summary>
    public struct ResourceContext {
        /// <summary>Density field in 0..1, spread evenly over the map (see <see cref="ResourceField"/>).</summary>
        public float Density;
        public float StartDistance;
        public bool NearWater;

        /// <summary>Climate of the cell on the world scale -1..1 (what <see cref="ResourceRule.UseClimate"/> reads).</summary>
        public float Temperature;
        public float Humidity;

        /// <summary>A cell with no conditions to speak of: mid density, far from the start, away from water.</summary>
        public static ResourceContext Neutral => new ResourceContext { Density = 0.5f, StartDistance = 1e9f, NearWater = false };
    }

    public struct ResourcePlacement {
        /// <summary>Index into the rules list.</summary>
        public int RuleIndex;

        /// <summary>Offset from the block centre in world units.</summary>
        public float OffsetX;
        public float OffsetZ;
        public float RotationY;
    }

    public static class ResourceRoller {

        private const int ClusterChannel = 0x5EED;

        /// <summary>
        /// Rolls which resources of one table appear on one block. The result depends only on the arguments (the map
        /// seed, the cell and what the map knows about it), so the same seed always grows the same trees in the same
        /// spots, no matter in which order or on which frame blocks are generated.
        /// </summary>
        /// <param name="weight">Share (0..1] of this table in the cell; where biomes blend, each table grows thinner.</param>
        /// <param name="tableSalt">Tells apart the tables of one cell (each biome has its own random stream).</param>
        /// <param name="cellSize">World size of a cell: cluster offsets are given in cells.</param>
        public static void Roll( IReadOnlyList<ResourceRule> rules, float weight, in ResourceContext context, int mapSeed, int tableSalt,
            int gridX, int gridY, float cellSize, List<ResourcePlacement> result, ResourceField patches = null ) {

            result.Clear();
            if( rules == null || rules.Count == 0 || weight <= 0f ) {
                return;
            }
            int cellSeed = tableSalt == 0 ? SeedMixer.Derive( mapSeed, gridX, gridY ) : SeedMixer.Derive( SeedMixer.Derive( mapSeed, tableSalt ), gridX, gridY );
            var random = new DeterministicRandom( cellSeed );
            for( int i = 0; i < rules.Count; i++ ) {
                var rule = rules[ i ];
                float factor = weight * Factor( rule, context );
                if( factor > 0f && rule.PatchScale > 0f && patches != null ) {
                    factor *= PatchFactor( rule, patches.PatchAt( tableSalt, i, rule, gridX, gridY ) );
                }
                if( rule.ClusterSpacing > 0 ) {
                    AppendDeposits( i, rule, factor, mapSeed, tableSalt, gridX, gridY, cellSize, result );
                    continue;
                }
                float probability = rule.Probability * factor;
                for( int j = 0; j < rule.Count; j++ ) {
                    if( random.NextFloat() >= probability ) {
                        continue;
                    }
                    float rotation = random.Range( rule.MinRotation, rule.MaxRotation );
                    float angle = random.NextFloat() * ( float ) ( 2 * Math.PI );
                    float distance = ( float ) Math.Sqrt( random.NextFloat() ) * rule.SpawnRange;
                    result.Add( new ResourcePlacement {
                        RuleIndex = i,
                        OffsetX = ( float ) Math.Cos( angle ) * distance,
                        OffsetZ = ( float ) Math.Sin( angle ) * distance,
                        RotationY = rotation
                    } );
                }
            }
        }

        /// <summary>A cell that is not part of any blend and has no conditions: what the plain rules used to mean.</summary>
        public static void Roll( IReadOnlyList<ResourceRule> rules, int mapSeed, int gridX, int gridY, List<ResourcePlacement> result ) {
            Roll( rules, 1f, ResourceContext.Neutral, mapSeed, 0, gridX, gridY, 1f, result );
        }

        /// <summary>Distance outside a rule's climate range at which its chance has fallen to 0.</summary>
        public const float ClimateMargin = 0.3f;

        /// <summary>Width, in shares of the map, of the soft edge of a patch.</summary>
        public const float PatchEdge = 0.12f;

        /// <summary>Multiplier (0..1) for a patch field value (0..1, spread evenly): 1 inside the rule's patches, 0 outside.</summary>
        public static float PatchFactor( in ResourceRule rule, float patch ) {
            float coverage = Clamp01( rule.PatchCoverage );
            if( coverage >= 1f ) {
                return 1f;
            }
            return Smooth( ( patch - ( 1f - coverage ) ) / PatchEdge + 0.5f );
        }

        /// <summary>Multiplier (0..1) for the climate of a cell: 1 inside the range, 0 a margin outside it.</summary>
        public static float ClimateFit( in ResourceRule rule, in ResourceContext context ) {
            return AxisFit( context.Temperature, rule.TemperatureFrom, rule.TemperatureTo )
                * AxisFit( context.Humidity, rule.HumidityFrom, rule.HumidityTo );
        }

        private static float AxisFit( float value, float from, float to ) {
            float distance = value < from ? from - value : value > to ? value - to : 0f;
            return Math.Max( 0f, 1f - distance / ClimateMargin );
        }

        /// <summary>Multiplier (0..1) of the rule's probability in this cell: water, density and distance from the start.</summary>
        public static float Factor( in ResourceRule rule, in ResourceContext context ) {
            if( rule.Water == WaterCondition.NearWater && !context.NearWater ) {
                return 0f;
            }
            if( rule.Water == WaterCondition.AwayFromWater && context.NearWater ) {
                return 0f;
            }
            float factor = 1f;
            if( rule.UseClimate ) {
                factor *= ClimateFit( rule, context );
            }
            if( rule.DensityFrom != rule.DensityTo ) {
                factor *= Smooth( ( context.Density - rule.DensityFrom ) / ( rule.DensityTo - rule.DensityFrom ) );
            }
            if( rule.MinStartDistance > 0f ) {
                factor *= rule.StartDistanceRamp > 0f
                    ? Clamp01( ( context.StartDistance - rule.MinStartDistance ) / rule.StartDistanceRamp )
                    : ( context.StartDistance >= rule.MinStartDistance ? 1f : 0f );
            }
            return factor;
        }

        // A deposit is a pure function of its square of the map, so any cell can ask which objects of the deposits
        // around it fall inside it, without a neighbour having to be generated first.
        private static void AppendDeposits( int ruleIndex, in ResourceRule rule, float acceptance, int mapSeed, int tableSalt,
            int gridX, int gridY, float cellSize, List<ResourcePlacement> result ) {

            if( acceptance <= 0f || rule.Probability <= 0f ) {
                return;
            }
            int spacing = rule.ClusterSpacing;
            int reach = ( int ) Math.Ceiling( Math.Max( 0f, rule.ClusterRadius ) / spacing );
            int squareX = FloorDiv( gridX, spacing );
            int squareY = FloorDiv( gridY, spacing );
            int channel = SeedMixer.Derive( mapSeed, ClusterChannel + tableSalt * 64 + ruleIndex );
            int min = Math.Max( 1, rule.ClusterMin );
            int max = Math.Max( min, rule.ClusterMax );

            for( int dy = -reach; dy <= reach; dy++ ) {
                for( int dx = -reach; dx <= reach; dx++ ) {
                    int sx = squareX + dx, sy = squareY + dy;
                    int squareSeed = SeedMixer.Derive( channel, sx, sy );

                    // Most squares hold no deposit: decide that without allocating a generator.
                    if( ( SeedMixer.Mix( ( uint ) squareSeed, 1u ) >> 8 ) * ( 1f / 16777216f ) >= rule.Probability ) {
                        continue;
                    }
                    var random = new DeterministicRandom( squareSeed );
                    float centerX = sx * spacing + random.NextFloat() * spacing - 0.5f;
                    float centerY = sy * spacing + random.NextFloat() * spacing - 0.5f;
                    int count = min + random.NextInt( max - min + 1 );
                    for( int n = 0; n < count; n++ ) {
                        float angle = random.NextFloat() * ( float ) ( 2 * Math.PI );
                        float distance = ( float ) Math.Sqrt( random.NextFloat() ) * rule.ClusterRadius;
                        float accept = random.NextFloat();
                        float rotation = random.Range( rule.MinRotation, rule.MaxRotation );
                        float px = centerX + ( float ) Math.Cos( angle ) * distance;
                        float py = centerY + ( float ) Math.Sin( angle ) * distance;
                        if( ( int ) Math.Floor( px + 0.5f ) != gridX || ( int ) Math.Floor( py + 0.5f ) != gridY || accept >= acceptance ) {
                            continue;
                        }
                        result.Add( new ResourcePlacement {
                            RuleIndex = ruleIndex,
                            OffsetX = ( px - gridX ) * cellSize,
                            OffsetZ = ( py - gridY ) * cellSize,
                            RotationY = rotation
                        } );
                    }
                }
            }
        }

        private static int FloorDiv( int value, int divisor ) {
            int quotient = value / divisor;
            return ( value % divisor != 0 && ( value < 0 ) ) ? quotient - 1 : quotient;
        }

        private static float Clamp01( float value ) {
            return value < 0f ? 0f : ( value > 1f ? 1f : value );
        }

        private static float Smooth( float t ) {
            t = Clamp01( t );
            return t * t * ( 3f - 2f * t );
        }
    }
}
