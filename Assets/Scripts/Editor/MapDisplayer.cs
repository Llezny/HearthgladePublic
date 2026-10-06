using System.Collections.Generic;
using System.IO;
using Hearthglade;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Map.Generator.PerlinNoise;
using UnityEditor;
using MessageType = UnityEditor.MessageType;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// Shows the world a map type generates for a game seed: biome map, the three climate layers, how much of the
    /// map every biome takes, and problems in the biome config. It runs the same Core code as the game, with the
    /// same size and the same derived seed, so what is shown here is what a new game gets.
    /// </summary>
    class MapDisplayer : EditorWindow {
        private const string HomeMapPath = "Assets/Resources/ScriptableObjects/Maps/Home.asset";
        private const int ExportScale = 4;

        [SerializeField] private MapSO mapType;
        [SerializeField] private PerlinNoiseMapConfig configOverride;
        [SerializeField] private int gameSeed = 12345;
        [SerializeField] private int mapOrdinal;

        private Texture2D biomeTexture;
        private Texture2D heightTexture;
        private Texture2D temperatureTexture;
        private Texture2D humidityTexture;

        private PerlinNoiseMapConfig config;
        private GeneratedTerrain terrain;
        private TerrainStats stats;
        private int overlapCount;
        private string mapInfo;
        private List<string> problems = new();
        private Vector2 scroll;

        [MenuItem("LuakszTools/MapDisplayer")]
        static void Init() {
            var window = GetWindow<MapDisplayer>("MapDisplayer");
            window.position = new Rect(0, 0, 720, 900);
            window.Show();
        }

        private void OnEnable() {
            if (mapType == null) {
                mapType = AssetDatabase.LoadAssetAtPath<MapSO>(HomeMapPath);
            }
            Refresh();
        }

        private void OnDisable() {
            DestroyTextures();
        }

        private void OnGUI() {
            EditorGUI.BeginChangeCheck();
            mapType = EditorGUILayout.ObjectField("Map type", mapType, typeof(MapSO), false) as MapSO;
            configOverride = EditorGUILayout.ObjectField(
                new GUIContent("Config override", "Preview another biome config instead of the one of the map type"),
                configOverride, typeof(PerlinNoiseMapConfig), false) as PerlinNoiseMapConfig;
            gameSeed = EditorGUILayout.IntField(new GUIContent("Game seed", "The seed of a save; the map seed is derived from it like in the game"), gameSeed);
            mapOrdinal = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Map ordinal", "0 = the first map of a game (Home)"), mapOrdinal));
            bool changed = EditorGUI.EndChangeCheck();

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Refresh")) changed = true;
                if (GUILayout.Button("Random seed")) {
                    gameSeed = Random.Range(int.MinValue, int.MaxValue);
                    changed = true;
                }
                using (new EditorGUI.DisabledScope(terrain == null)) {
                    if (GUILayout.Button("Save PNGs...")) SavePngs();
                }
            }
            if (changed) Refresh();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawProblems();
            if (terrain != null) {
                DrawPanels();
                DrawStats();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawProblems() {
            if (!string.IsNullOrEmpty(mapInfo)) {
                EditorGUILayout.LabelField(mapInfo, EditorStyles.miniLabel);
            }
            foreach (var problem in problems) {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            }
            if (overlapCount > 0) {
                EditorGUILayout.HelpBox($"{overlapCount} pair(s) of biome ranges overlap; the earlier biome in the list wins there.", MessageType.Info);
            }
        }

        private void DrawPanels() {
            float panel = Mathf.Min((position.width - 30) / 2f, 340f);
            var area = GUILayoutUtility.GetRect(panel * 2 + 8, (panel + 20) * 2 + 8);
            DrawPanel(new Rect(area.x, area.y, panel, panel + 20), "Biomes", biomeTexture);
            DrawPanel(new Rect(area.x + panel + 8, area.y, panel, panel + 20), "Height (equalised)", heightTexture);
            DrawPanel(new Rect(area.x, area.y + panel + 28, panel, panel + 20), "Temperature (blue = cold)", temperatureTexture);
            DrawPanel(new Rect(area.x + panel + 8, area.y + panel + 28, panel, panel + 20), "Humidity (blue = wet)", humidityTexture);
        }

        private static void DrawPanel(Rect rect, string title, Texture2D texture) {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 18), title, EditorStyles.boldLabel);
            if (texture != null) {
                GUI.DrawTexture(new Rect(rect.x, rect.y + 20, rect.width, rect.width), texture, ScaleMode.ScaleToFit, false);
            }
        }

        private void DrawStats() {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Share of the map", EditorStyles.boldLabel);
            for (int i = 0; i < config.Biomes.Count; i++) {
                var biome = config.Biomes[i];
                var row = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(new Rect(row.x, row.y + 2, 14, 14), Opaque(biome.Color));
                GUI.Label(new Rect(row.x + 20, row.y, 110, 18), biome.BiomeName);
                float share = (float)stats.BiomeShare[i];
                var bar = new Rect(row.x + 130, row.y + 4, Mathf.Max(0, row.width - 190), 10);
                EditorGUI.DrawRect(bar, new Color(0, 0, 0, 0.25f));
                EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * share, bar.height), Opaque(biome.Color));
                GUI.Label(new Rect(bar.xMax + 4, row.y, 56, 18), $"{share:P1}");
            }
            if (stats.UnmatchedShare > 0) {
                EditorGUILayout.HelpBox($"{stats.UnmatchedShare:P1} of the cells match no biome and stay empty.", MessageType.Error);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layers (min / max / mean)", EditorStyles.boldLabel);
            DrawLayer("Height", stats.Height);
            DrawLayer("Temperature", stats.Temperature);
            DrawLayer("Humidity", stats.Humidity);
        }

        private static void DrawLayer(string label, LayerStats layer) {
            EditorGUILayout.LabelField(label, $"{layer.Min:F3} / {layer.Max:F3} / {layer.Mean:F3}");
        }

        private bool hasStart;
        private int startX, startY;

        private void Refresh() {
            DestroyTextures();
            terrain = null;
            stats = null;
            problems = new List<string>();
            overlapCount = 0;
            mapInfo = null;

            config = configOverride != null ? configOverride : mapType != null ? mapType.perlinNoiseConfig : null;
            if (config == null) {
                problems.Add(mapType == null ? "Pick a map type." : $"{mapType.mapName} has no perlin noise config: it cannot be generated.");
                return;
            }
            problems.AddRange(config.GetProblems());
            if (config.HeightNoisePreset == null || config.TemperatureMapPreset == null || config.HumidityMapPreset == null || config.Biomes == null) {
                return;
            }

            int size = mapType != null && mapType.SizeInChunks > 0 ? ChunkManager.ChunkSize * mapType.SizeInChunks : config.Size;
            int seed = mapType != null ? MapGenerator.GetMapSeed(gameSeed, mapType.mapName, mapOrdinal) : gameSeed;
            if (size <= 0) {
                problems.Add("The map has no size (SizeInChunks / config Size is not positive).");
                return;
            }

            var rules = PerlinNoiseGenerator.ToBiomeRules(config);
            terrain = PerlinNoiseGenerator.Generate(config, size, seed);
            stats = TerrainStats.Compute(terrain, rules.Length);
            overlapCount = BiomeRuleValidator.Validate(rules).Overlaps.Count;
            mapInfo = $"{size} x {size} cells, map seed {seed}, generator version {WorldGenVersion.Current}";
            hasStart = false;
            int patched = 0;
            foreach (byte patch in terrain.Patch) patched += patch != 0 ? 1 : 0;
            mapInfo += $", patches on {(float)patched / terrain.Patch.Length:P1} of the cells (drawn darker)";
            if (config.HasIslandShape) {
                var liquid = PerlinNoiseGenerator.ToLiquidFlags(config);
                bool Flat(int x, int y) => x >= 0 && y >= 0 && x < size && y < size
                    && terrain.Biome[terrain.Index(x, y)] >= 0 && !liquid[terrain.Biome[terrain.Index(x, y)]];
                hasStart = PlayerStartFinder.TryFind(size, Flat, 5, out startX, out startY);
                mapInfo += hasStart ? $"\nPlayer start ({startX}, {startY}) drawn as a red square" : "\nNO player start found";
            }
            BuildTextures(1);
        }

        private void BuildTextures(int scale) {
            var (biome, height, temperature, humidity) = CreateTextures(scale);
            biomeTexture = biome;
            heightTexture = height;
            temperatureTexture = temperature;
            humidityTexture = humidity;
        }

        private (Texture2D, Texture2D, Texture2D, Texture2D) CreateTextures(int scale) {
            int size = terrain.Size * scale;
            int Cell(int x, int y) => terrain.Index(x / scale, y / scale);
            return (
                PerlinNoise.ToTexture(size, size, (x, y) => {
                    int cell = Cell(x, y);
                    if (hasStart && Mathf.Abs(x / scale - startX) <= 1 && Mathf.Abs(y / scale - startY) <= 1) {
                        return Color.red;
                    }
                    int biome = terrain.Biome[cell];
                    if (biome == GeneratedTerrain.NoBiome) {
                        return Color.red;
                    }
                    var color = Opaque(config.Biomes[biome].Color);
                    // Cosmetic patches are drawn a little darker than their biome.
                    return terrain.Patch[cell] != 0 ? Color.Lerp(color, Color.black, 0.25f) : color;
                }),
                PerlinNoise.ToTexture(size, size, (x, y) => Color.Lerp(new Color(0.08f, 0.16f, 0.4f), Color.white, Unit(terrain.Height[Cell(x, y)]))),
                PerlinNoise.ToTexture(size, size, (x, y) => Color.Lerp(new Color(0.2f, 0.4f, 0.95f), new Color(0.95f, 0.3f, 0.2f), Unit(terrain.Temperature[Cell(x, y)]))),
                PerlinNoise.ToTexture(size, size, (x, y) => Color.Lerp(new Color(0.85f, 0.7f, 0.4f), new Color(0.1f, 0.45f, 0.9f), Unit(terrain.Humidity[Cell(x, y)])))
            );
        }

        private void SavePngs() {
            string folder = EditorUtility.SaveFolderPanel("Save world preview PNGs", Directory.GetCurrentDirectory(), "");
            if (string.IsNullOrEmpty(folder)) {
                return;
            }
            var (biome, height, temperature, humidity) = CreateTextures(ExportScale);
            string prefix = $"{(mapType != null ? mapType.mapName : "map")}_{gameSeed}_";
            foreach (var (name, texture) in new[] { ("biomes", biome), ("height", height), ("temperature", temperature), ("humidity", humidity) }) {
                File.WriteAllBytes(Path.Combine(folder, prefix + name + ".png"), texture.EncodeToPNG());
                DestroyImmediate(texture);
            }
            EditorUtility.RevealInFinder(folder);
        }

        // Equalised layers are in [-1, 1).
        private static float Unit(double value) {
            return Mathf.Clamp01((float)((value + 1.0) / 2.0));
        }

        // The colours in the biome assets were saved with alpha 0.
        private static Color Opaque(Color color) {
            color.a = 1f;
            return color;
        }

        private void DestroyTextures() {
            foreach (var texture in new[] { biomeTexture, heightTexture, temperatureTexture, humidityTexture }) {
                if (texture != null) DestroyImmediate(texture);
            }
            biomeTexture = heightTexture = temperatureTexture = humidityTexture = null;
        }
    }
}
