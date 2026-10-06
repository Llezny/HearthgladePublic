using Hearthglade.Gameplay.Map.Generator.PerlinNoise;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    class EditorGUITextures : EditorWindow
    {
        private Texture2D texture;
        private int size = 0;
        private int octaves = 0;
        private float persistance = 0;
        private float lacunarity = 0;
        int seed = 0;
        

        [MenuItem("LuakszTools/NoiseDisplayer")]
        static void Init() {
            var window = GetWindow<EditorGUITextures>("NoiseDrawer");
            window.position = new Rect(0, 0, 800, 600);
            window.Show();
        }

        void OnGUI() {
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("Size");
            size = EditorGUILayout.IntSlider(size, 50, 500);

            GUILayout.Label("Octaves");
            octaves = EditorGUILayout.IntSlider(octaves, 1, 10);
            
            GUILayout.Label("Persistance");
            persistance = EditorGUILayout.Slider(persistance, 0, 5);

            GUILayout.Label("Lacunarity");
            lacunarity = EditorGUILayout.Slider(lacunarity, 0, 5);

            GUILayout.Label("Seed");
            seed = EditorGUILayout.IntSlider(seed, int.MinValue, int.MaxValue);

            if (EditorGUI.EndChangeCheck())
            {
                texture = PerlinNoise.GetPerlinTexture( size, size, octaves, lacunarity, persistance, seed); //PerlinNoise.Get2DPerlinNoiseTexture( size, size, seed, scale, octaves, persistance, lacunarity, Vector2.zero, PerlinNoise.NormalizeMode.Global );
            }
            if (texture)
            {
               EditorGUI.DrawPreviewTexture(new Rect(position.width * 0.05f, position.height * 0.4f, position.height * 0.5f, position.height * 0.5f), texture);
            }
        }
    }
}