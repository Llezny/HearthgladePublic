# World generation system — reference

Current-state technical reference for how maps are generated and rendered. This is **not** a
history of how it got built — see `docs/WORLD_GEN_PLAN.md` for that (closed 2026-09-23, kept as an
archive of decisions and session-by-session notes). Open work items live in `docs/TODO.md`.

## 1. Mental model

A "world" (Home, Forest, Winter, ...) is just a **data combo**, not a code path:

```
MapSO (size, lighting, entry prefab)
  └─ PerlinNoiseMapConfig (noise presets, island shape, resource/POI budgets)
        └─ List<BiomeSO> (climate range, ground block, resources, entities, cosmetic patches)
              └─ List<PoiSO> (points of interest and their loot)
```

Adding a new world type (a new island, a new biome mix, a harder ship destination) means creating
new assets in this combo — no new C# classes needed. Generation itself is one deterministic,
Unity-free pipeline (`Assets/Scripts/Gameplay/Core/World/`, tested with `dotnet test`, no
`UnityEngine` references); Unity-side code (`Assets/Scripts/Gameplay/Map/`) only reads the config,
calls into Core on a thread-pool worker, and turns the *result* into GameObjects.

## 2. Generation pipeline

Entry point: `MapGenerator.GenerateBlocksUsingNoiseAsync` (`Assets/Scripts/Gameplay/Map/MapGenerator.cs`).
Reads ScriptableObjects on the main thread, then runs the pure part via `UniTask.RunOnThreadPool`:

1. **`TerrainGenerator.Generate`** (`Core/World/TerrainGenerator.cs`) — for every cell:
   - samples 3 independent `GradientNoise` layers (height, temperature, humidity), each seeded via
     `SeedMixer.Derive(seed, channel)` so they never correlate;
   - **domain warps** the sample position (2 more noise layers offset X/Z) so coasts and biome
     borders wind instead of following the noise lattice;
   - **equalizes** each layer (`LayerEqualizer`) so a biome's climate range is a literal *share* of
     the map, not an arbitrary noise-value cutoff;
   - **island mask**: `ApplyIsland` pulls height down to sea level as `WorldShapeSettings.IslandCoverage`
     (distance-from-center falloff) grows; `IslandRadius = 0` skips this entirely (plain noise map,
     no island — used by nothing today but still supported);
   - **climate jitter**: fine noise nudges temperature/humidity right before biome lookup, so biome
     borders are ragged instead of smooth arcs;
   - **`AssignBiomes`**: first `BiomeRule` (height/temp/humidity box) that contains the cell wins —
     rule order in `PerlinNoiseMapConfig.Biomes` is priority order;
   - **`IslandCleanup`**: keeps only the largest landmass (`KeepMainIslandOnly`) and fills lakes
     smaller than `MinLakeSize`;
   - **`ApplyPatches`**: per-`BiomePatchData` noise field; where it peaks *and* the cell already
     matches that patch's owning biome, the cell's *look* changes (e.g. a Dirt patch in Meadow) but
     block/resources/walkability stay the owning biome's — purely cosmetic, see `BiomeSO.Patches`.
2. **`ResourceField.Build`** — wraps the generated terrain with lookups resources/POI need: biome at
   a cell, distance from the player start, "near water", reservation state.
3. **`PoiPlacer.Place`** — picks sites for points of interest **before** resources, and reserves
   their footprint (`ResourceField.Reserve`) so nothing else grows there. Must run first for that
   reason.
4. **`ResourcePlanner.PlanMap`** — for every ground, unreserved cell: rolls candidates
   (`ResourceRoller`, from each biome's `Resources` list, blended at biome borders via a 5×5 mix),
   resolves spacing conflicts deterministically (`ResourceRule.MinSpacing`, priority = hash of
   position so ordering never matters), then caps each chunk at `ResourceChunkBudget` (drops plain
   spawns before deposit/ore-vein objects).
5. Back on the main thread, `MapGenerator` walks every cell once, turns it into a `BlockModel`
   (**data only** — no GameObject yet) and calls `Content.AddSceneObjects` for whatever the planner
   decided grows there. `PoiBuilder.Apply` then writes the POI pieces (tent, chest with loot, ...)
   as `SceneObjectModel`s.
6. Nothing is instantiated until a chunk becomes active — see §4.

### Determinism & seeding

`MapGenerator.GetMapSeed(gameSeed, mapName, mapOrdinal) = SeedMixer.Derive(gameSeed, HashString(mapName), mapOrdinal)`.
`gameSeed` comes from `SaveManager.GameSeed` (the save's `SaveHeader.Seed`), so the same save seed
always yields the same Home map, but a second trip to "Forest" (`mapOrdinal` incremented) yields a
*different* Forest. Every noise layer, patch, resource roll and POI placement derives its own
sub-seed via `SeedMixer.Derive`/`SeedMixer.Mix` from that one map seed — nothing uses `UnityEngine.Random`.

`WorldGenVersion.Current` (`Core/World/WorldGenVersion.cs`) is saved per map and bumped whenever a
change would move things for an existing save re-rolling content (e.g. scene objects a save
predates). Purely visual changes (a new biome texture, a mesh-jaggedness tweak) do **not** need a
bump — only changes to *what* generates where.

## 3. Data model

- **`TerrainGrid`** (`Core/World/TerrainGrid.cs`): one byte of packed flags (Present/Ground/Walkable/
  Wall/Elevated) + one byte of biome id, per cell, for the whole map. Cheap to snapshot
  (`CopyWindow` → `TerrainWindow`) for a worker thread to read without racing the main thread.
- **`BlockModel`** (`Assets/Scripts/Gameplay/Map/BlockModel.cs`): the saved/authoritative per-cell
  data (position, prefab name, patch id, scene objects). `Map.Models` is the dictionary from cell to
  `BlockModel`; there is no `Map.GetBlock`/`BlocksDictionary` — blocks are not GameObjects.
- **`BlockSO`**: per-prefab data — `IsGround`, `IsElevated` (a *small* step, `TerrainVisualParams.ElevatedRise`,
  used for the grass-mound-over-sand/dirt lip look — unrelated to the larger, withdrawn "hill levels"
  feature), `Biome` (visual id), resources, walkability.

## 4. Chunk streaming & runtime content

- `ChunkManager.ChunkSize = 13` cells. `ChunkStreamingPlanner` (Core, pure, unit-tested) decides
  which chunks should be active from the player's chunk coordinate: **active radius 1** (3×3),
  **prebuild radius 2** (visuals computed one ring further out so an about-to-be-active chunk
  already has ground), **visual-keep radius 3** (visuals beyond that are torn down again — cheap to
  recompute later). `ChunkManager` (Unity) only owns GameObjects/async and reads the planner's
  `ToShow`/`ToHide` diffs.
- `BlockContent.SpawnBlockVisual` instantiates a prefab per non-ground cell in a newly active chunk
  (water/buyable blocks) and builds one `BoxCollider` per **ground** cell (`Block` layer — read by
  `GroundChecker`/`BuildingPlacer`). Ground blocks render nothing themselves; the terrain *looks*
  like blocks purely via the overlay mesh in §5.
- Scene objects (resources, POI pieces) become `SceneObjectModel`s parented under the chunk on
  activation; a saved game keeps them as-is (a gathered resource stays gone) via `SceneObjectKey`.

## 5. Terrain visuals (the "diorama" layer)

Per active chunk, `ChunkVisualComputer.Compute` (Core, pure, runs on the thread pool) builds:

- **`SplatBuilder`**: two RGBA textures (8 biome-weight channels total: Grass/Dirt/Sand/Stone in
  splat A, Taiga/Forest/Swamp/Tundra in splat B) sampled by `TerrainSplat.shader`
  (`Assets/Shaders/Terrain/TerrainSplat.shader`) — one draw call for the whole chunk top.
- **`TerrainMeshBuilder.BuildTop`**: one flat quad per ground cell, plus a "grass lip" overhang
  wherever a neighbour is lower or missing (`EmitLip`) — a short flat strip pushed outward plus a
  vertical drop face, so the cliff seam beneath is hidden. Convex island corners get a **rounded
  fan** (`EmitCorner`, 3 segments along a quarter-circle arc, added 2026-09-23 — previously a single
  flat triangle that looked like a sharp spike).
- **`TerrainMeshBuilder.BuildCliffs`**: a vertical wall per edge with a real height drop.
  - A **true deep cliff** (neighbour cell doesn't exist — coastline/map edge, drops to
    `BaseTopY - CliffDepth`) is **jagged**: split into 3 columns, the 2 interior ones nudged in/out
    along the wall's normal by a small deterministic hash offset (`GrassBuilder.Hash`, reused), the
    2 end columns pinned to the flat edge so neighbouring geometry still seams cleanly.
  - A **shallow internal step** (the `IsElevated` grass-mound lip between two existing ground cells)
    stays a **plain flat quad** — jaggedness there reads as a broken seam, not eroded rock (fixed
    2026-09-23 after exactly that regression).
  - `CliffRock.shader` renders this mesh, one more draw call per chunk.
- **`GrassBuilder`**: a GPU-instanced tuft mesh scattered on interior (non-edge) cells,
  `GrassTufts.shader`.

`ChunkVisualPipeline` (Unity) turns the returned `MeshBuffers`/`SplatResult` into real `Mesh`/
`Material` objects and assigns them to the chunk's renderers; `TerrainVisualSO` holds the tunables
(`overhang`, `overhangDrop`, `cliffDepth`, `enableCliffs`, blend sharpness, ...).

**Water** is *not* part of this per-chunk system: it's one oversized flat quad
(`Assets/_Prefabs/Environment/MapBackground/SimpleWater.prefab`, one draw call per map) seen through
the gaps where a cell is non-ground. `mat_water.mat` is currently a plain opaque
`Universal Render Pipeline/Lit` blue — no shore foam or waves (a styled attempt was built and
reverted 2026-09-23, see `docs/TODO.md`).

**Camera note relevant to any future depth-texture visual work**: the gameplay camera is an
**orthographic** Cinemachine vcam, but the Unity Editor's Scene View camera is always perspective.
Any shader trick using `clipPos.w` as eye depth, or `_ZBufferParams`-based `LinearEyeDepth`, only
works under perspective and will look fine in Scene View while being silently wrong in the actual
Game View. Use `LinearEyeDepth(positionWS, viewMatrix)` (projection-agnostic) instead.

## 6. Resources & vegetation

`BiomeSO.Resources` (`List<SpawnableResourceData>`) per biome. Each rule can require: a density
range read from a per-map noise field (`ResourceField`, groves/clearings), proximity to water,
minimum distance from the player start (with a rarity ramp), minimum spacing between same-rule
objects, and deposit clustering (`ClusterSpacing` — ore veins/mushroom rings spawn in tight
clumps instead of one at a time). `ResourcePlanner` resolves all of this deterministically per chunk
(§2 step 4); results are cached so reserving cells must happen *before* the first `PlanChunk` call.
`Assets/Scripts/Tests/ItemSourceTests.cs` scans every database/biome/recipe and fails if an item has
no discoverable source (an intentional safety net against orphaned items).

**Climate niches and own patches (2026-10-04).** Two rule features make vegetation look grown instead of sprinkled:

* *Climate niche* (`useClimate`, `temperatureFrom/To`, `humidityFrom/To` on `SpawnableResourceData`): the chance is multiplied by a fit
  that is 1 inside the ranges and falls linearly to 0 over `ResourceRoller.ClimateMargin` (0.3) outside, same scale as the biome ranges
  and the crop climate. `ResourceContext` carries the cell's `Temperature` / `Humidity` (from `GeneratedTerrain`). So pines sit in the cool half
  of a forest and oaks in the warm half, and a wild crop grows where the same crop would grow at full speed.
* *Own patches* (`patchScale`, `patchCoverage`): every entry gets its own noise field (`ResourceField.PatchAt`, equalised like the climate layers,
  one byte per cell, built on first use per table and rule), and grows only in the part of the map where that field is high, `patchCoverage` of
  the map in total, with a soft edge (`ResourceRoller.PatchEdge`). The chance inside a patch is the entry's `spawnProbability`, so a patchy entry
  needs a probability of about `old / coverage` to keep its total. Species form their own thickets and groves instead of sharing one
  density field (the old `densityFrom/To` still exists and multiplies in).
* Applied to the trees (patch 11 cells, coverage 0.55), berries (6, 0.35), wild crops and flowers (6-7, 0.4-0.45), wild fruit trees (12, 0.5),
  brown mushrooms (7, 0.35) and bushes (9, 0.5). Probabilities were raised by `1 / coverage / mean climate fit` (the mean fit over the cells of the
  biome on all finite maps); an entry whose mean fit would be under 0.3 (the biome is far from the plant's best climate) kept the patches and lost
  the niche; the wild wheat and the swamp watermelon have no niche on purpose. Numbers per map: `Tools > Farming > Count wild plants on the home map`;
  totals stayed within roughly +-30 % of before. Ore deposits (`clusterSpacing`) were not touched.
* Tests: `ResourceNicheTests` (fit, patch coverage, clumping, determinism), written, not run.

## 7. Points of interest

`PoiSO` (type, loot table, `PoiRule`: count range, clear radius, allowed biomes, distance-from-start
range, spacing from other POIs, water proximity) → `PoiPlacer.Place` picks sites (reachability
checked via `GridFlood`) → `PoiBuilder.Apply` instantiates the site's pieces as `SceneObjectModel`s,
with `LootRoller` picking loot tiered by distance from the player start. Today there is exactly one
type, `AbandonedCamp`; the mechanism supports more without new code (`docs/TODO.md`).

## 8. Fauna & ambient life

`BiomeSO.Entities` (`List<EntitySpawnData>`: prefab, max alive, cooldown, `SpawnTime` Always/Day/
Night) replaces the older `MapSO.entitiesOnMap`. `EntityManager.Tick(isDay)` (fed by
`ClockManager.IsDay`) spawns/retires entities as time-of-day rules change. Two entity shapes exist:

- **`PassiveEntityController`**-driven animals (deer, hen): a real model, `Animancer` clips,
  `WanderBrain` movement — no NavMesh in this project, movement is gated by `GroundChecker` +
  `Map.IsWorldPositionWalkable`.
- **Ambient VFX entities** (`FireflySwarm` — despite the name, a generic "one `ParticleSystem`,
  plays on enable, fades out via `Retire()`" component, reused beyond fireflies): butterflies
  (`AmbientFlyer`, a flown mesh) and, since 2026-09-23, pollen motes / mist wisps / snow flurries
  (`Assets/Scripts/Editor/AmbientParticleAssetBuilder.cs`, `LuakszTools/Animals/Build ambient
  particles`) — no collider, no interaction, purely cosmetic (`CanInteract() => false`).

New animal/ambient assets are built by one-off Editor tools (`*AssetBuilder.cs` under
`Assets/Scripts/Editor/`, mostly `[MenuItem("LuakszTools/Animals/...")]`) that construct the
prefab/material through Unity's C# API rather than hand-authored YAML — safer than writing a
`ParticleSystem`'s ~2000-line serialized form by hand.

## 9. Multi-world support in practice

A new world is a new `MapSO` + its own `PerlinNoiseMapConfig` (+ maybe a new `BiomeSO` if the
climate coverage needs a new catch-all). Home/Forest/Winter all reuse the same noise presets and
most biome art; a new biome asset only needs a texture/tint if it wants a distinct look (the
`TerrainSplat` shader has 8 biome slots, currently all used: Grass/Dirt/Sand/Stone/Taiga/Forest/
Swamp/Tundra — a 9th biome needs either a new splat channel or reusing an existing slot's texture
with a different tint, as Forest/Swamp/Tundra all do).

Travel between worlds: `Ship.destinations` (`List<ShipDestination>`), picked in `ShipMenu`'s
destination list, each with its own cost/description/target `MapSO`. `Ship.NewMapOnEachEntry = true`
and `MapManager.GoToNewMapAsync` means a ship sailing from Home always generates a **fresh** map on
each trip (by design) — `Ship` itself isn't `ISaveable`, so the selected destination doesn't persist
across a save/reload (a known, out-of-scope gap). Per-map-type difficulty scaling by repeat-visit
count (`mapOrdinal`) doesn't read into resource/enemy density yet — see `docs/TODO.md` and
`docs/TRAVEL_PROGRESSION_PLAN.md` (a designed-but-unbuilt directional travel + depth system that
would likely replace it).

## 10. Testing

- **Core (Unity-free)**: `cd Tests.Standalone && dotnet test` — covers noise, biome rules, terrain
  mesh geometry, resource planning, POI placement, chunk streaming math. Fast, no Editor needed.
- **Unity EditMode/PlayMode**: `Unity.exe -batchmode -nographics -projectPath <repo> -runTests
  -testPlatform PlayMode|EditMode` (Editor must be closed first). Covers prefab/scene wiring,
  integration (`Assets/Scripts/Tests/PlayMode/MapIntegrationTests.cs`).
- Per current working agreement, **tests are only run when explicitly requested** — see the
  project's session notes if picking this back up with an AI assistant.

## 11. Where things are (quick index)

| Concern | Path |
|---|---|
| Config assets | `Assets/Resources/ScriptableObjects/Maps/*.asset` (MapSO), `.../PerlinNoise/Config/*.asset`, `Assets/ScriptableObjects/Map/Biomes/*.asset` |
| Pure generation logic | `Assets/Scripts/Gameplay/Core/World/` |
| Unity orchestration | `Assets/Scripts/Gameplay/Map/` (`MapGenerator`, `ChunkManager`, `Map`, `BlockContent`) |
| Terrain visuals (Unity side) | `Assets/Scripts/Gameplay/Map/Visual/` (`ChunkVisualPipeline`, `TerrainVisualSO`) |
| Shaders | `Assets/Shaders/Terrain/` (`TerrainSplat`, `CliffRock`, `GrassTufts`) |
| Entities | `Assets/Scripts/Gameplay/Entities/`, `Assets/Scripts/Gameplay/Core/Entities/` |
| Editor asset builders | `Assets/Scripts/Editor/*AssetBuilder.cs` |
| Core tests | `Assets/Scripts/Gameplay/Core/Tests/` |
| Historical plan (closed) | `docs/WORLD_GEN_PLAN.md` |
| Open work | `docs/TODO.md` |
