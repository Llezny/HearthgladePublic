using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hearthglade.Core.World {

    /// <summary>A box in height/temperature/humidity space, half-open like <see cref="BiomeRule"/>.</summary>
    public struct ClimateRegion {
        public float HeightFrom, HeightTo;
        public float TemperatureFrom, TemperatureTo;
        public float HumidityFrom, HumidityTo;

        /// <summary>Share of the whole climate space (the cube [-1, 1)^3) this region takes.</summary>
        public double Volume => ( HeightTo - HeightFrom ) * ( TemperatureTo - TemperatureFrom ) * ( HumidityTo - HumidityFrom ) / 8.0;

        public override string ToString( ) {
            return string.Format( CultureInfo.InvariantCulture,
                "height [{0:0.##}, {1:0.##}) temperature [{2:0.##}, {3:0.##}) humidity [{4:0.##}, {5:0.##})",
                HeightFrom, HeightTo, TemperatureFrom, TemperatureTo, HumidityFrom, HumidityTo );
        }
    }

    public struct BiomeRuleOverlap {
        /// <summary>The earlier rule; it wins in <see cref="TerrainGenerator.FindBiome"/>.</summary>
        public int Winner;
        public int Shadowed;
        public ClimateRegion Example;
    }

    public sealed class BiomeRuleReport {
        /// <summary>Regions of the climate space no rule matches: cells there get no biome.</summary>
        public readonly List<ClimateRegion> Gaps = new List<ClimateRegion>();

        /// <summary>Pairs of rules that both match somewhere; only the first of them is ever used there.</summary>
        public readonly List<BiomeRuleOverlap> Overlaps = new List<BiomeRuleOverlap>();

        /// <summary>Share of the climate space (0..1) that matches no rule.</summary>
        public double GapVolume { get; internal set; }

        public bool HasGaps => Gaps.Count > 0;
    }

    /// <summary>
    /// Checks a biome list for holes and shadowed rules. Terrain layers are equalised to [-1, 1), so that
    /// cube is the whole domain: a cell that lands in a hole gets no block, which used to be found only
    /// while a map was being generated.
    /// </summary>
    public static class BiomeRuleValidator {

        public static BiomeRuleReport Validate( IReadOnlyList<BiomeRule> rules ) {
            if( rules == null ) {
                throw new ArgumentNullException( nameof( rules ) );
            }

            // Every rule edge is a breakpoint, so a rule matches all of an interval between two breakpoints or
            // none of it, and testing each interval's midpoint is exact.
            var heightCuts = Cuts( rules, r => r.HeightFrom, r => r.HeightTo );
            var temperatureCuts = Cuts( rules, r => r.TemperatureFrom, r => r.TemperatureTo );
            var humidityCuts = Cuts( rules, r => r.HumidityFrom, r => r.HumidityTo );

            var report = new BiomeRuleReport();
            var matching = new List<int>();
            var reportedPairs = new HashSet<long>();

            for( int h = 0; h < heightCuts.Count - 1; h++ ) {
                for( int t = 0; t < temperatureCuts.Count - 1; t++ ) {
                    for( int m = 0; m < humidityCuts.Count - 1; m++ ) {
                        var region = new ClimateRegion {
                            HeightFrom = heightCuts[ h ], HeightTo = heightCuts[ h + 1 ],
                            TemperatureFrom = temperatureCuts[ t ], TemperatureTo = temperatureCuts[ t + 1 ],
                            HumidityFrom = humidityCuts[ m ], HumidityTo = humidityCuts[ m + 1 ]
                        };
                        double ph = ( region.HeightFrom + region.HeightTo ) / 2;
                        double pt = ( region.TemperatureFrom + region.TemperatureTo ) / 2;
                        double pm = ( region.HumidityFrom + region.HumidityTo ) / 2;

                        matching.Clear();
                        for( int i = 0; i < rules.Count; i++ ) {
                            if( rules[ i ].Matches( ph, pt, pm ) ) {
                                matching.Add( i );
                            }
                        }

                        if( matching.Count == 0 ) {
                            report.Gaps.Add( region );
                            report.GapVolume += region.Volume;
                            continue;
                        }
                        for( int a = 0; a < matching.Count; a++ ) {
                            for( int b = a + 1; b < matching.Count; b++ ) {
                                long pair = ( long ) matching[ a ] * rules.Count + matching[ b ];
                                if( reportedPairs.Add( pair ) ) {
                                    report.Overlaps.Add( new BiomeRuleOverlap { Winner = matching[ a ], Shadowed = matching[ b ], Example = region } );
                                }
                            }
                        }
                    }
                }
            }
            return report;
        }

        private static List<float> Cuts( IReadOnlyList<BiomeRule> rules, Func<BiomeRule, float> from, Func<BiomeRule, float> to ) {
            var cuts = new SortedSet<float> { -1f, 1f };
            foreach( var rule in rules ) {
                foreach( var edge in new[] { from( rule ), to( rule ) } ) {
                    if( edge > -1f && edge < 1f ) {
                        cuts.Add( edge );
                    }
                }
            }
            return new List<float>( cuts );
        }
    }
}
