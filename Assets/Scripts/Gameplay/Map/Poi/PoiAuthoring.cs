#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hearthglade.Gameplay.Map.Visual;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    /// <summary>
    /// Editor only: the root of a point of interest laid out in a scene (Hearthglade > POI > Edit POI in scene). Its children are the pieces
    /// (prefab instances), and <see cref="PoiPathAuthoring"/> objects for the painted paths. The scene is only a way of drawing: the
    /// source of truth is the <see cref="PoiSO"/>, which is read by Load and written by Save (also when the scene is saved).
    /// One unit of the scene is one world unit; a cell of the map is <see cref="Cell"/> units, so the grid drawn here is the map's.
    /// </summary>
    public class PoiAuthoring : MonoBehaviour
    {
        public const float Cell = MapGenerator.TILE_X_OFFSET;

        public PoiSO poi;
        [Tooltip("Pieces and path ends snap to this many cells while they are moved (0 = free).")]
        [Min(0f)] public float snapStep = 0.5f;
        [Tooltip("Pieces turn in steps of this many degrees (0 = free).")]
        [Min(0f)] public float rotationStep = 15f;
        [Tooltip("Writes the layout to the POI asset whenever this scene is saved.")]
        public bool saveToAssetWhenSceneIsSaved = true;
        public bool showTerrain = true;
        /// <summary>What the layout looked like at the last Load or Save (see PoiEditorTool): the difference is unsaved work.</summary>
        [HideInInspector] public string savedLayout;
        [Min(8)] public int viewRadius = 36;

        /// <summary>Set by the editor tools: land of the map this POI stands on, around the centre of its site (see PoiTerrainPreview).</summary>
        public static Func<PoiAuthoring, PoiTerrain> TerrainProvider;

        [NonSerialized] private PoiTerrain terrain;
        [NonSerialized] private bool terrainRequested;

        public PoiTerrain Terrain => terrain;

        public void SetTerrain(PoiTerrain value)
        {
            terrain = value;
            terrainRequested = true;
        }

        public void ForgetTerrain()
        {
            terrain = null;
            terrainRequested = false;
        }

        public static Vector3 ToWorld(Vector2 cells, float y = 0f)
        {
            return new Vector3(cells.x * Cell, y, cells.y * Cell);
        }

        public static Vector2 ToCells(Vector3 world)
        {
            return new Vector2(world.x / Cell, world.z / Cell);
        }

        private void OnDrawGizmos()
        {
            if (showTerrain && !terrainRequested && TerrainProvider != null)
            {
                terrainRequested = true;
                terrain = TerrainProvider(this);
            }
            float half = viewRadius * Cell;
            float y = -0.002f;

            // Ground: water where the map has none, otherwise land of the island; plain green when the POI has no map to show.
            if (terrain != null && showTerrain)
            {
                Gizmos.color = new Color(0.25f, 0.5f, 0.75f, 0.45f);
                Gizmos.DrawCube(new Vector3(0f, y, 0f), new Vector3(half * 2f, 0.001f, half * 2f));
                Gizmos.color = new Color(0.55f, 0.75f, 0.45f, 0.75f);
                foreach (var run in terrain.LandRuns)
                {
                    float x0 = (run.x - 0.5f) * Cell;
                    float x1 = (run.y + 0.5f) * Cell;
                    Gizmos.DrawCube(new Vector3((x0 + x1) / 2f, y + 0.0005f, run.z * Cell), new Vector3(x1 - x0, 0.001f, Cell));
                }
            }
            else
            {
                Gizmos.color = new Color(0.55f, 0.75f, 0.45f, 0.5f);
                Gizmos.DrawCube(new Vector3(0f, y, 0f), new Vector3(half * 2f, 0.001f, half * 2f));
            }

            // The grid of the map: a line per cell, stronger every five cells and on the axes of the site.
            for (int i = -viewRadius; i <= viewRadius; i++)
            {
                float edge = (i - 0.5f) * Cell;
                bool major = i % 5 == 0;
                Gizmos.color = new Color(0f, 0f, 0f, major ? 0.28f : 0.1f);
                Gizmos.DrawLine(new Vector3(-half, 0f, edge), new Vector3(half, 0f, edge));
                Gizmos.DrawLine(new Vector3(edge, 0f, -half), new Vector3(edge, 0f, half));
            }
            // The centre of the site and its axes (east = +x, north = +z).
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawLine(new Vector3(-half, 0.001f, 0f), new Vector3(half, 0.001f, 0f));
            Gizmos.DrawLine(new Vector3(0f, 0.001f, -half), new Vector3(0f, 0.001f, half));
            Handles.Label(new Vector3(half, 0f, 0f), "E");
            Handles.Label(new Vector3(0f, 0f, half), "N");

            if (poi != null)
            {
                DrawClearRadius();
                DrawStart();
            }
        }

        private void DrawClearRadius()
        {
            Handles.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            Handles.DrawWireDisc(Vector3.zero, Vector3.up, poi.clearRadius * Cell);
        }

        // Where the player starts, as far as it is known: from the map (when the POI is anchored) or from the anchor offset.
        private void DrawStart()
        {
            Vector2? start = terrain != null ? terrain.StartCells : (poi.anchored ? -(Vector2)poi.anchorOffset : null);
            if (start == null)
            {
                return;
            }
            var position = ToWorld(start.Value);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 1f);
            Gizmos.DrawWireSphere(position + Vector3.up * 0.1f, 0.1f);
            Handles.Label(position + Vector3.up * 0.25f, "Player start");
        }
    }

    /// <summary>The land around the centre of a site, in cells from the centre (x east, z north), as runs of land per row.</summary>
    public class PoiTerrain
    {
        /// <summary>x = first cell, y = last cell (both included), z = row.</summary>
        public readonly List<Vector3Int> LandRuns = new List<Vector3Int>();
        public Vector2 StartCells;
    }

    /// <summary>Editor only: one painted path of a POI. Its two child objects From and To are the ends (the same place = a round plaza).</summary>
    public class PoiPathAuthoring : MonoBehaviour
    {
        [Min(0.5f)] public float halfWidth = 1f;
        public BiomeId look = BiomeId.Dirt;

        public Transform From => transform.Find("From");
        public Transform To => transform.Find("To");

        public static Color ColorOf(BiomeId look)
        {
            return look switch
            {
                BiomeId.Dirt => new Color(0.6f, 0.4f, 0.2f, 0.55f),
                BiomeId.Stone => new Color(0.7f, 0.7f, 0.75f, 0.6f),
                BiomeId.Sand => new Color(0.9f, 0.8f, 0.5f, 0.6f),
                BiomeId.Grass => new Color(0.3f, 0.7f, 0.3f, 0.55f),
                _ => new Color(0.5f, 0.6f, 0.3f, 0.55f),
            };
        }

        private void OnDrawGizmos()
        {
            var from = From;
            var to = To;
            if (from == null || to == null)
            {
                return;
            }
            var a = new Vector3(from.position.x, 0.002f, from.position.z);
            var b = new Vector3(to.position.x, 0.002f, to.position.z);
            float radius = halfWidth * PoiAuthoring.Cell;
            Handles.color = ColorOf(look);
            Handles.DrawSolidDisc(a, Vector3.up, radius);
            Handles.DrawSolidDisc(b, Vector3.up, radius);
            var along = b - a;
            if (along.sqrMagnitude > 1e-8f)
            {
                var side = Vector3.Cross(Vector3.up, along.normalized) * radius;
                Handles.DrawAAConvexPolygon(a + side, b + side, b - side, a - side);
            }
            Handles.color = new Color(0f, 0f, 0f, 0.6f);
            Handles.DrawLine(a, b);
            Handles.Label((a + b) / 2f + Vector3.up * 0.02f, $"{name} ({look})");
        }
    }
}
#endif
