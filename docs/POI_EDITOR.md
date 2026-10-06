# In-scene POI editor

A tool for laying out points of interest (`PoiSO`: camps, harbours) as a scene, instead of typing
`offset`/`rotation` into the asset's lists by hand. Code: `Gameplay/Map/Poi/PoiAuthoring.cs`
(drawing components, editor only) and `Editor/PoiEditorTool.cs` (the tool, inspectors).

## How to use

1. Select a `PoiSO` asset in the Project window and click **Edit layout in scene** in the inspector, or use
   `Tools > POI > Edit selected POI in scene`. This opens the scene
   `Assets/Scenes/Authoring/PoiEditor.unity` (created on first use, in `.gitignore`:
   it is a working copy, the asset is the source of truth).
2. The pieces are **prefab instances** under the `POI layout` object. Move, rotate,
   duplicate (Ctrl+D) and delete them with the regular Unity tools. While you drag, they snap
   to map cells (`snapStep`, half a cell by default; rotation in `rotationStep` = 15° steps).
3. Adding: the **Piece (prefab)** field in the `POI layout` inspector (the piece lands in the middle of the view),
   or drag a prefab into the scene and click **Adopt prefab instances dragged into the scene**
   (or drag it onto `POI layout` in the Hierarchy).
4. **Paths** (painted into the terrain, no objects): the **Path** and **Round plaza** buttons. A path is an
   object with two ends `From`/`To` (the same spot = a round plaza), a `halfWidth` and a look
   (`look`). When a path is selected, the Scene view shows handles for its ends and its width.
5. **Save to POI asset** writes the layout into the `PoiSO` (`pieces` and `paths`; the other asset fields are
   left unchanged). Saving the scene with Ctrl+S saves too (`saveToAssetWhenSceneIsSaved`). Switching to another POI
   with unsaved changes asks what to do.

## What you see in the Scene view

- The map cell grid (0.3675 units; a stronger line every 5), the site axes (E = +x, N = +z), the
  `clearRadius` circle.
- The **map island** the POI stands on (the map whose `perlinNoiseConfig.Pois` contains it, seed 12345):
  water in blue, land in green, plus a marker for the player start. For randomly placed POIs
  (the camp) the layout is the POI's own, and the island is only indicative (without the site's random rotation).
- Paths in their look's colour; the **Refresh island** button recomputes the island.

## Rules and limitations

- What gets saved: the prefab, the x/z position in cells (rounded to 0.01), and the Y rotation relative to
  the prefab. Instance height and scale are ignored (the builder takes them from the prefab).
- Objects that are not prefab instances are skipped on save (with a console warning).
- Fields outside the layout (placement, spacing, loot, anchor) are still edited in the asset inspector.
- `HarborAssetBuilder` fills in a harbour layout only when the POI has no pieces yet;
  `Tools > Ports > Reset harbour layouts to the defaults` restores the built-in layouts.
