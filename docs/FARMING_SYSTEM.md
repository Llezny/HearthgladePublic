# Farming system — reference

Current-state technical reference for crops, garden plots, fences and animals raiding the garden. The history and the
decisions behind it are in `docs/FARMING_PLAN.en.md` (all phases done, 2026-10-03). Open work items live in `docs/TODO.md`.

## 1. Mental model

The whole farm is **data and logic in Core**; Unity only draws it and feeds it commands.

```
CropSO (asset)  --ToDefinition-->  CropDefinition (Core, immutable)
Map.Farm : FarmModel (Core, one per map, saved in MapModel.farm)
   PlotRecord(PlotKey, PlotType, CropState?)   CropState = (cropId, plantedAt, fruitClockStart)
Plot (MonoBehaviour)  = stateless VIEW of one PlotKey; draws CropVisual from FarmModel.Describe
FarmingService (Scoped, ITickable) = adapter: world clock, inventory, audio, HUD notice, view refresh
```

* **Crops always grow**, loaded chunk or not, because growth is a pure function of time:
  `CropGrowth.StageAt / ReadyYield / NextChangeAt (def, state, now)`. Nothing ticks per plant. `now` is
  `IWorldClock.NowMinutes` (game minutes), implemented by `ClockManager` (rebuilt from day counter + day/night phase +
  time of day; saved with the game; keeps counting on other maps and loading screens).
* **One game minute is about 3.3 real seconds** (`DEFAULT_CLOCK_SPEED 3 × DEFAULT_TIMESCALE 0.1` = 0.3 game minutes per
  second); a game day (1440) is 80 real minutes. Times in `CropSO` are game minutes. `FarmingService.FormatRealTime`
  converts for the tooltip.
* A `Plot` can be despawned with its chunk at any moment and loses nothing. Key = anchor cell `(x, 0, z)` of the object.

## 2. Core (`Assets/Scripts/Gameplay/Core/Farming`, tests in `Core/Tests/FarmModelTests.cs`, `ForageTests.cs`)

| Type | Role |
|------|------|
| `CropDefinition` | stage durations (last stage is open-ended), `MaxYield`, `FruitRegrowMinutes`, produce/seed `ItemId`, `SeedDropChance`, `ForageKind` |
| `CropLifecycle` | `Annual` (one harvest, the plot empties) / `Perennial` (stays, fruit regrows, one per `FruitRegrowMinutes` up to `MaxYield`) |
| `PlotType` | `Bed` / `Orchard` / `Trellis`: a crop only goes on its own type |
| `FarmModel` | commands `AddPlot/EnsurePlot/RemovePlot/TryPlant/TryHarvest/Eat`; queries `Describe`, `NextChangeAt`, `TryFindForageTarget`; events `PlotChanged`, `CropEaten`; `ToSnapshot/FromSnapshot` |
| `PlotView` | what the view needs: stage, progress, `ReadyYield`, `MaxYield`, `MinutesUntilRipe`, `SpeedPercent` |
| `ClimateRange`, `Climate`, `CropClimate` | climate rule: a crop outside its good temperature / humidity range grows slower (see "Climate" below) |
| `FarmSnapshot` | versioned plain-field save (`Version` 2, plots, each with `SpeedPercent`; a version 1 save loads at 100 %); a crop the catalog no longer knows is dropped on load |

Perennial rules worth knowing: harvesting keeps the progress of the next fruit; from a full plant the fruit clock restarts
(surplus time is not banked); an annual that is eaten while ripe empties its bed, a perennial only loses the fruit.
Randomness (seed drop) is a `DeterministicRandom` parameter, never `UnityEngine.Random`.

## 3. Unity side (`Environment/Farming`, `UI/Menu/Farming`)

* `CropSO` (`Assets/ScriptableObjects/Farming/*`): lifecycle, `plotType`, seed/produce `ItemSO`, `seedDropChance`,
  `stageMinutes[]`, `maxYield`, `fruitRegrowMinutes`, `visualPrefab`, `harvest` (a `ResourceSO` for animation/sound/skill),
  `forageKind`, the good climate (`temperatureFrom/To`, `humidityFrom/To`). Registered in `DatabaseSO.Crops` by *Refresh items database*; `CropCatalog` (DI) bakes definitions.
* `Map.Farm` is built in `Map.InitializeMap` and restored in `RestoreDataFromSavedState` **before** objects spawn; an old
  save has no `farm` field and gets an empty farm. `Plot.OnSceneObjectModelAssigned` (a hook on `SceneObject`, called after
  spawn + injection, also for a just-placed building) binds the key and `EnsurePlot`s it, which migrates old beds.
* `Plot` interaction: empty → `SeedPickerMenu` (the scene component that used to be `PlantingServiceView`; same script guid,
  tiles pooled, only owned seeds that fit the plot type); growing → tooltip "Growing 40% - ripe in 7 min"; ripe → loading bar
  (`GatheringTime` shared with `Resource`), then `FarmingService.Harvest` (produce + chance of a seed, sound, dust puff).
  A ripe plot shows a floating `ActivityBadge` (shared frame + glyph sprite, here `activity_glyph_harvest`).
* `CropVisual` (child of the crop prefab): one object per stage, optional fruit objects hidden one by one as they are picked.
  `facesEast` + `headYaw` turn a plant with a head (the sunflower; a mature one really faces east) so its head looks along world +X
  (`Plot.SpawnVisual` calls `Orient`, so a rotated bed does not matter). The wild sunflower has a fixed rotation in `Meadow.asset`
  (`minRotation = maxRotation = 90 - headYaw = 214.4`). `headYaw` -124.4 is derived from the Blender head azimuth 0.6 rad
  (`crops_batch1.py`, Blender (x, y) becomes Unity (-x, -y) with the export settings of `crop_lib.py`), not measured: if the heads
  do not look along +X in game, change both numbers.
* Planting consumes **one seed item** (`WheatSeed`, `CarrotSeed`, `StrawberrySeed`, `AppleSapling`, `GrapeCutting`);
  harvest gives produce back and sometimes the seed.

### Crops and plots

| Crop | Plot | Cycle | Stages (game min) | Yield | Regrow | Forage |
|------|------|-------|-------------------|-------|--------|--------|
| Wheat | `GardenPatch` (bed, 1×1) | annual | 60, 60 | 2 | – | grain |
| Carrot | bed | annual | 60, 90 | 2 | – | vegetable |
| Strawberry | bed | perennial | 90 | 2 | 120 | berry |
| AppleTree | `OrchardPlot` (2×2) | perennial | 360, 720 | 6 | 360 | fruit |
| GrapeVine | `Trellis` (1×1×2, rotatable) | perennial | 120, 240 | 4 | 180 | fruit |
| Rye | bed | annual | 70, 90 | 2 | – | grain |
| Potato | bed | annual | 60, 80 | 3 | – | vegetable |
| Cabbage | bed | annual | 70, 100 | 2 | – | vegetable |
| Lingonberry | bed | perennial | 100 | 4 | 100 | berry |
| Raspberry | bed | perennial | 110 | 3 | 120 | berry |
| Pear | `OrchardPlot` | perennial | 400, 800 | 6 | 400 | fruit |
| OysterMushroom | bed (a log) | perennial | 50 | 3 | 90 | – |
| Peas | `Trellis` | annual | 60, 80 | 3 | – | vegetable |
| Sunflower | bed | annual | 80, 100 | 2 | – | grain |
| Corn | bed | annual | 90, 120 | 3 | – | grain |
| Tomato | `Trellis` | annual | 70, 90 | 3 | – | vegetable |
| Rice | bed (a paddy: the stage meshes carry a sheet of water) | annual | 80, 110 | 3 | – | grain |
| Cranberry | bed | perennial | 90 | 4 | 100 | berry |
| WatermelonPlant | bed | annual | 100, 140 | 2 | – | fruit |
| DatePalm | `OrchardPlot` | perennial | 500, 900 | 4 | 450 | fruit |
| Oats | bed | annual | 70, 90 | 2 | – | grain |
| Turnip | bed | annual | 60, 80 | 2 | – | vegetable |
| Blueberry | bed | perennial | 100 | 4 | 110 | berry |
| Cherry | `OrchardPlot` | perennial | 380, 760 | 6 | 380 | fruit |
| Beans | `Trellis` | annual | 70, 90 | 3 | – | vegetable |
| Flax | bed | annual | 70, 90 | 3 | – | – |
| Cotton | bed | annual | 90, 120 | 3 | – | – |
| Chili | bed | annual | 80, 100 | 3 | – | – |
| Cattail | bed | annual | 50, 70 | 2 | – | – |
| Citrus | `OrchardPlot` | perennial | 420, 800 | 5 | 420 | – |

Rye to Corn are crop batch 1 (2026-10-04). Models: `Tools/Blender/crops_batch1.py` (helpers in `crop_lib.py`) writes one FBX per
plant to `Assets/Arts/Models/Environment/Farming/Crops/` with objects `<Plant>_Stage0..N`, `<Plant>_Fruit0..N` (perennials,
already placed in plant space) and `<Plant>_Wild`, plus the produce icons and the pear sapling icon; seed pouches come from
`seed_pouch.py`. The Unity side (items, `CropSO`, crop and wild prefabs, `ResourceSO`, biome entries, database refresh) was
generated by `Tools/Unity/CropBatch1Builder.cs.txt` (copy it into `Assets/Scripts/Editor/`, run with `-executeMethod`, delete
it again). Bed plants stand at local y 0.052, the pear at the apple tree's offset and stage scales (0.257 / 0.57 / 1), the pea
vines on the trellis plane. Item ids: `Rye RyeSeed`, `Potato PotatoSeed`, `Cabbage CabbageSeed`, `Lingonberry LingonberrySeed`,
`Raspberry RaspberrySeed`, `Pear PearSapling`, `OysterMushroom OysterSpawn`, `Peas PeaSeed`, `Sunflower SunflowerSeed`,
`Corn CornSeed`.

Tomato to Citrus are crop batch 2 (2026-10-04): `Tools/Blender/crops_batch2.py` (the cherry and citrus trees come from
`crop_trees.py`, the same Pine-spray technique as the pear; the date palm is hand-built) and `Tools/Unity/CropBatch2Builder.cs.txt`.
Differences from batch 1: the watermelon crop reuses the existing `Watermelon` item and wild prefab (the wild one now drops
`WatermelonSeed`); Flax and Cotton are plain fibre items (no food data), Date / Lemon / CattailRoot are the produce of
DatePalm / Citrus / Cattail; saplings `DatePalmSapling`, `CherrySapling`, `LemonSapling`; seeds `TomatoSeed`, `RiceSeed`,
`CranberrySeed`, `WatermelonSeed`, `OatsSeed`, `TurnipSeed`, `BlueberrySeed`, `BeanSeed`, `FlaxSeed`, `CottonSeed`, `ChiliSeed`,
`CattailSeed`. Flax and Cotton are not eaten by animals (forage none); neither are chili, cattail and lemons.

### Climate (2026-10-04)

A crop in the wrong climate **grows slower; it never dies and never yields less**. Every `CropSO` has a good range
(`temperatureFrom/To`, `humidityFrom/To`, world scale -1..1; default = anywhere). Per axis the fit is 1 inside the range and
falls linearly to 0 at `CropClimate.Margin` (0.3) outside it; `fit = fitT * fitH`; `speed = 50 + 50 * fit` percent
(`CropClimate.MinSpeedPercent` 50). Wheat, carrot, strawberry, apple tree and grapevine got ranges as well (first guesses);
the 25 others use the table below.

* **Where the climate comes from.** `MapGenerator` writes the cell's temperature and humidity into `BlockModel.climateT/climateH`
  (one byte each, `Climate.Encode`: 0 = unknown, 1..255 = -1..1; only ground blocks, and only written to the save when set).
  `Map.ClimateAt(cell)` decodes it; a block without it (a save from before 2026-10-04) falls back to the middle of its biome's
  range (`BiomeSO.ClimateCentre`). Blocks swapped by the player keep the climate of the old block (`MapGenerator.ReplaceBlock`).
  Names are `climateT/H` on purpose: old saves can still carry a float `temperature` / `humidity` per block.
* **Growth stays a pure function of time.** The speed is read once when planting (`FarmModel.TryPlant(..., Climate?)`) and stored
  in `CropState.SpeedPercent` (saved in `PlotSnapshot`). `CropGrowth.Scaled` stretches every stage duration and the fruit regrow
  time by `100 / speed`; `StageAt`, `IsMature`, `ReadyYield`, `MinutesUntilRipe`, `ConsumeFruit` and `NextChangeAt` all go
  through it, so the view and the tooltip agree. A multi-cell plot uses the climate of its anchor (lowest corner) cell.
* **UI.** Seed picker: each seed says "good here" or "slow here (65%): too dry" (`FarmingService.PlantingNote`, appended to the
  tile's name text). Plot tooltip: "Growing 40% - ripe in 7 min (slow: too cold)".
* Tests: `CropClimateTests` (fit, speed, issue, encode/decode, stages / fruit clock scaling, snapshot v1 and v2). Written 2026-10-04, not run yet.

### Where the new crops grow wild, and the climate they are meant for

The ranges below are the `CropSO` climate fields of the crops (see "Climate"); the wild plants also carry the climate through the
biome they spawn in. Temperature is the world's -1..1 scale (about -10 / 10 / 30 °C), humidity -1 (dry) .. 1 (wet).

| Plant | Good temperature | Good humidity | Wild in |
|-------|------------------|---------------|---------|
| Rye | 2 °C ± 6 (-0.7..-0.1) | -0.6..0.2 | Taiga, Tundra |
| Potato | 8 °C ± 6 (-0.4..0.2) | -0.2..0.6 | Taiga, Forest |
| Cabbage | 10 °C ± 6 (-0.3..0.3) | 0..0.6 | Forest |
| Lingonberry | 0 °C ± 6 (-0.8..-0.2) | 0..0.7 | Taiga, Tundra |
| Raspberry | 12 °C ± 6 (-0.2..0.4) | 0.1..0.6 | Forest, DenseForest |
| Pear | 14 °C ± 5 (-0.05..0.45) | -0.2..0.4 | Forest, DenseForest |
| OysterMushroom | 12 °C ± 6 (-0.2..0.4) | 0.3..1 | DenseForest |
| Peas | 13 °C ± 5 (-0.1..0.4) | -0.1..0.5 | Meadow, Forest |
| Sunflower | 22 °C ± 6 (0.3..0.9) | -1..-0.2 | Meadow |
| Corn | 24 °C ± 5 (0.45..0.95) | -0.6..0.1 | Meadow |
| Tomato | 24 °C ± 5 (0.45..0.95) | -0.4..0.2 | Meadow |
| Rice | 26 °C ± 5 (0.55..1.0) | 0.6..1 | Swamp |
| Cranberry | 8 °C ± 5 (-0.35..0.15) | 0.5..1 | Swamp |
| WatermelonPlant | 28 °C ± 5 (0.65..1.0) | -0.8..-0.1 | Meadow, Swamp (the already existing wild watermelon) |
| DatePalm | 30 °C ± 5 (0.75..1.0) | -1..-0.3 | Beach |
| Oats | 6 °C ± 6 (-0.5..0.1) | 0..0.6 | Taiga, Tundra |
| Turnip | 3 °C ± 6 (-0.65..-0.05) | -0.3..0.5 | Taiga, Tundra |
| Blueberry | 6 °C ± 6 (-0.5..0.1) | 0.2..0.8 | Taiga, Tundra, DenseForest, Forest |
| Cherry | 13 °C ± 5 (-0.1..0.4) | -0.2..0.4 | Forest, DenseForest |
| Beans | 20 °C ± 5 (0.25..0.75) | -0.2..0.4 | Forest, Meadow |
| Flax | 10 °C ± 6 (-0.3..0.3) | 0..0.6 | Forest |
| Cotton | 27 °C ± 5 (0.6..1.0) | -0.6..-0.1 | Meadow |
| Chili | 28 °C ± 5 (0.65..1.0) | -0.7..-0.2 | Meadow |
| Cattail | 15 °C ± 10 (-0.25..0.75) | 0.6..1 | Swamp |
| Citrus | 25 °C ± 5 (0.5..1.0) | -0.3..0.4 | Beach |

Spawn probabilities per cell are in the biome assets (0.015-0.035 for plants, 0.003-0.004 for the pear; spacing 1.1-3 cells).
Wild plants are gatherable `Resource` prefabs `Wild<Name>` (box collider fitted to the mesh, the pear a capsule), each with a
`ResourceSO` that drops the produce and sometimes the seed (35-50 %, the pear sapling 50 %).

Multi-cell plots are anchored at their lowest corner cell and do not rotate (`BuildingPlacer.CanRotate`). The apple tree is
`AppleTree.fbx` (`Tools/Blender/apple_tree.py`: a forked trunk with a broad dome crown made of the spruce's own foliage sprays hooked onto a branch network, 2252 tris,
0.61 x 0.70 x 0.62 m) at uniform scale 0.257 / 0.57 / 1 for the three stages, with 6 red fruit objects placed on the crown shell;
`WildAppleTree` uses the same mesh at full size. The vine is its own low-poly model.
Build category for all garden plots: `BuildCategory.Plot`.

## 4. Seeds and exploration

* Wild plants of the biomes carry a **bonus drop** (`ResourceSO.BonusItem/BonusChance`): wild wheat → `WheatSeed`, wild
  carrot → `CarrotSeed`, wild strawberry bush → `StrawberrySeed`, `WildGrapes` → `GrapeCutting`, `WildAppleTree` → `AppleSapling`
  (Meadow, Forest, DenseForest). Batch 1: each `Wild<Name>` drops its own seed (`RyeSeed`, `PotatoSeed`, ... `PearSapling`,
  `OysterSpawn`).
* Abandoned camp chests (`PoiSO.loot`) can hold seeds, saplings and cuttings (the last two from tier 1).
* `ItemSourceTests` counts bonus drops and crop produce/seed drops as sources.

## 5. Fences, gates, paths

* `BuildEdgeBox.Kind` (`EdgeKind.Wall` / `Fence`). A fence or gate **blocks movement** like a wall but **never closes a
  room**: `BuildEdgeGrid.IsWallOccupied` is what room detection (`PlayerController` probe) uses, `RoomWallCutaway` skips
  fences. `BuildCategory.Fence` / `Gate` are edge categories (`BuildCategoryExtensions.IsEdgePiece / ToEdgeKind`).
* `Fence` (prefab name unchanged, it is the save key), `FenceGate` (a `Door` + `DoorAnimation`; an open gate lets animals in),
  `CobblePath` (a `Floor`, height 0.047 = `Map.FloorTileHeight`). Recipes in `BuildingsRecipies.JSON`.
* `Map.CanMoveTo` (player) and `Map.CanStep` (animals, ground layer) share `IsStepBlockedByEdges`, so animals stop at fences
  and house walls too.

## 6. Animals raid the garden

* Core `WanderBrain` gained `Seeking` and `Eating`; `ForageBrain` decides: when a rest runs out, with `ForageChance` it asks
  `FarmModel.TryFindForageTarget` for the nearest **ripe** plot within the radius that matches its diet (a plot on cooldown is
  skipped), walks straight at it (no pathfinding), eats one piece after `EatSeconds`, then leaves that plot alone for
  `CooldownSeconds`. A blocked step (fence, closed gate) → `GiveUp`, the plot is ignored for `BlockedCooldownSeconds`.
  The player coming within `alertDistance` makes it run (flee interrupts everything).
* `AnimalForaging` (serialized on the prefab, diet empty = never): Hen = grain + vegetable, radius 1.5; Deer = any, radius 2.5.
  `AnimalClips.eat` is optional (`Hen_Eat`, `Deer_Eat` built by the animal builders).
* Raids happen only while the animal exists, i.e. near the player (animals are spawned near the player). Crops are not
  eaten while the player is away. `FarmingService` shows "A deer ate your Apple!" when `CropEaten` fires with an eater.

## 7. Tests

* Core, no Unity (`cd Tests.Standalone && dotnet test`): `FarmModelTests`, `CropClimateTests`, `ForageTests`, `BuildEdgeGridTests`.
* PlayMode: `FarmingIntegrationTests` (plant → despawn view → clock → harvest), `ForagingIntegrationTests` (deer eats an
  unfenced crop, leaves a fenced one). `ItemSourceTests` (EditMode) covers the seed/produce sources.

## 8. Known limits / ideas

* No watering, fertiliser, seasons, scarecrow, chicken coop; gates must be closed by hand.
* Wild plant counts per biome: `Tools > Farming > Count wild plants on the home map` (`Scripts/Editor/WildPlantCounter.cs`, prints the planned objects for 3 seeds). DenseForest is not in the home map (only `ForestMapConfig`), so the wild oyster mushroom only exists there.
* Background raids while the player is away (plan question 3, variant "3b") are not implemented.
* No plant-discovery catalogue; no planting sound; harvest effect reuses the building dust puff.
* Winter berries for the tundra and per-direction seeds wait for `docs/TRAVEL_PROGRESSION_PLAN.md`.
