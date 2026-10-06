using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Core.World;

namespace Hearthglade.Core.Farming {

    // All plots of one map and every command on them. Knows nothing about Unity, the inventory or scenes:
    // time comes in as `now`, randomness as a parameter, results go back to the caller.
    public sealed class FarmModel {

        private sealed class Plot {
            public PlotType Type;
            public CropState Crop;
        }

        private readonly ICropCatalog catalog;
        private readonly Dictionary<PlotKey, Plot> plots = new();

        // Raised only by commands that changed a plot. Growth that happens by itself is found via NextChangeAt.
        public event Action<PlotKey> PlotChanged;

        // Something other than the player took produce: (plot, crop id, how many, who).
        public event Action<PlotKey, string, int, string> CropEaten;

        public int PlotCount => plots.Count;

        public FarmModel( ICropCatalog catalog ) {
            this.catalog = catalog;
        }

        public bool HasPlot( PlotKey key ) => plots.ContainsKey( key );

        // False when a plot already sits on that key.
        public bool AddPlot( PlotKey key, PlotType type ) {
            if( plots.ContainsKey( key ) ) {
                return false;
            }
            plots[ key ] = new Plot { Type = type };
            PlotChanged?.Invoke( key );
            return true;
        }

        // Idempotent registration, e.g. for beds from saves that predate the model. Never changes an existing plot.
        public void EnsurePlot( PlotKey key, PlotType type ) {
            AddPlot( key, type );
        }

        public bool RemovePlot( PlotKey key ) {
            if( !plots.Remove( key ) ) {
                return false;
            }
            PlotChanged?.Invoke( key );
            return true;
        }

        // climate = the cell's climate: a crop outside its good range grows slower. Null (unknown) means full speed.
        public PlantResult TryPlant( PlotKey key, ItemId cropId, long now, Climate? climate = null ) {
            if( !plots.TryGetValue( key, out var plot ) ) {
                return PlantResult.NoPlot;
            }
            if( plot.Crop != null ) {
                return PlantResult.Occupied;
            }
            if( !catalog.TryGet( cropId, out var def ) ) {
                return PlantResult.UnknownCrop;
            }
            if( def.PlotType != plot.Type ) {
                return PlantResult.WrongPlotType;
            }
            plot.Crop = CropGrowth.Plant( def, now, climate.HasValue ? CropClimate.SpeedPercent( def.Climate, climate.Value ) : 100 );
            PlotChanged?.Invoke( key );
            return PlantResult.Planted;
        }

        // Takes everything ripe. Annuals leave the plot empty, perennials keep growing.
        public HarvestResult TryHarvest( PlotKey key, long now, DeterministicRandom rng ) {
            if( !TryGetCrop( key, out var plot, out var def ) ) {
                return HarvestResult.Failed;
            }
            int ready = CropGrowth.ReadyYield( def, plot.Crop, now );
            if( ready <= 0 ) {
                return HarvestResult.Failed;
            }
            RemoveFruit( plot, def, now, ready );

            int seeds = !def.Seed.IsEmpty && def.SeedDropChance > 0f && rng.NextFloat() < def.SeedDropChance ? 1 : 0;
            PlotChanged?.Invoke( key );
            return new HarvestResult( def.Produce, ready, def.Seed, seeds );
        }

        // Something ate up to `count` ripe produce; returns how much was actually there.
        // An annual eaten while ripe is lost entirely, a perennial only loses the fruit.
        public int Eat( PlotKey key, long now, int count, string eater = null ) {
            if( count <= 0 || !TryGetCrop( key, out var plot, out var def ) ) {
                return 0;
            }
            int eaten = Math.Min( count, CropGrowth.ReadyYield( def, plot.Crop, now ) );
            if( eaten <= 0 ) {
                return 0;
            }
            string cropId = plot.Crop.CropId;
            RemoveFruit( plot, def, now, eaten );
            PlotChanged?.Invoke( key );
            CropEaten?.Invoke( key, cropId, eaten, eater );
            return eaten;
        }

        // The nearest plot within radiusCells (cell units, cell centres at whole numbers) whose ripe produce an animal
        // with this diet would eat. allow lets the caller skip plots, e.g. ones it just gave up on.
        public bool TryFindForageTarget( float xCells, float zCells, float radiusCells, ForageKind diet, long now,
            Func<PlotKey, bool> allow, out PlotKey target )
        {
            target = default;
            float best = radiusCells * radiusCells;
            bool found = false;
            foreach( var pair in plots ) {
                var crop = pair.Value.Crop;
                if( crop == null || !catalog.TryGet( new ItemId( crop.CropId ), out var def ) || ( def.ForageKind & diet ) == 0 ) {
                    continue;
                }
                float dx = pair.Key.X - xCells, dz = pair.Key.Z - zCells;
                float distance = dx * dx + dz * dz;
                if( distance > best || CropGrowth.ReadyYield( def, crop, now ) <= 0 || ( allow != null && !allow( pair.Key ) ) ) {
                    continue;
                }
                best = distance;
                target = pair.Key;
                found = true;
            }
            return found;
        }

        public PlotView Describe( PlotKey key, long now ) {
            if( !plots.TryGetValue( key, out var plot ) ) {
                return PlotView.Missing;
            }
            if( plot.Crop == null || !catalog.TryGet( new ItemId( plot.Crop.CropId ), out var def ) ) {
                return PlotView.Empty( plot.Type );
            }
            var ( stage, progress ) = CropGrowth.StageAt( def, plot.Crop, now );
            return new PlotView(
                true, plot.Type, false, def.Id, stage, def.StageCount, progress,
                CropGrowth.ReadyYield( def, plot.Crop, now ), def.MaxYield, CropGrowth.MinutesUntilRipe( def, plot.Crop, now ), plot.Crop.SpeedPercent
            );
        }

        // When the plot's stage or ready yield will next change by itself; null if never.
        public long? NextChangeAt( PlotKey key, long now ) {
            if( !TryGetCrop( key, out var plot, out var def ) ) {
                return null;
            }
            return CropGrowth.NextChangeAt( def, plot.Crop, now );
        }

        public FarmSnapshot ToSnapshot() {
            var snapshot = new FarmSnapshot();
            var keys = new List<PlotKey>( plots.Keys );
            keys.Sort();
            foreach( var key in keys ) {
                var plot = plots[ key ];
                snapshot.Plots.Add( new PlotSnapshot {
                    X = key.X,
                    Y = key.Y,
                    Z = key.Z,
                    Type = plot.Type,
                    CropId = plot.Crop?.CropId,
                    PlantedAtMinute = plot.Crop?.PlantedAtMinute ?? 0,
                    FruitClockStartMinute = plot.Crop?.FruitClockStartMinute ?? 0,
                    SpeedPercent = plot.Crop?.SpeedPercent ?? 0,
                } );
            }
            return snapshot;
        }

        // A null snapshot (old save) gives an empty farm. A crop the catalog no longer knows is dropped.
        public static FarmModel FromSnapshot( ICropCatalog catalog, FarmSnapshot snapshot ) {
            var model = new FarmModel( catalog );
            if( snapshot?.Plots == null ) {
                return model;
            }
            foreach( var saved in snapshot.Plots ) {
                var plot = new Plot { Type = saved.Type };
                if( !string.IsNullOrEmpty( saved.CropId ) && catalog.TryGet( new ItemId( saved.CropId ), out _ ) ) {
                    plot.Crop = new CropState( saved.CropId, saved.PlantedAtMinute, saved.FruitClockStartMinute, saved.SpeedPercent );
                }
                model.plots[ new PlotKey( saved.X, saved.Y, saved.Z ) ] = plot;
            }
            return model;
        }

        private bool TryGetCrop( PlotKey key, out Plot plot, out CropDefinition def ) {
            def = null;
            return plots.TryGetValue( key, out plot )
                && plot.Crop != null
                && catalog.TryGet( new ItemId( plot.Crop.CropId ), out def );
        }

        private static void RemoveFruit( Plot plot, CropDefinition def, long now, int count ) {
            if( def.Lifecycle == CropLifecycle.Annual ) {
                plot.Crop = null;
            } else {
                CropGrowth.ConsumeFruit( def, plot.Crop, now, count );
            }
        }
    }
}
