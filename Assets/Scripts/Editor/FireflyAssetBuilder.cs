using System;
using System.IO;
using Hearthglade.Core.Entities;
using Hearthglade.Gameplay.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Hearthglade.EditorTools.AnimalAssetKit;

namespace Hearthglade.EditorTools
{
    /// <summary>
    /// Builds the firefly swarm: an additive glow material, one particle system that drifts and blinks a few fireflies over a spot,
    /// the prefab and its place in the biome tables (night only). There is no model: a firefly is a soft glowing dot.
    /// </summary>
    public static class FireflyAssetBuilder {

        private const string TexturePath = "Assets/Arts/Textures/VFX/SoftGlow.png";
        private const string MaterialPath = "Assets/Arts/Materials/VFX/Firefly.mat";
        private const string PrefabPath = "Assets/_Prefabs/Entities/FireflySwarm.prefab";

        // Fireflies like damp, quiet places. (biome asset, most alive at once, seconds between spawns)
        private static readonly (string biome, int maxAlive, float cooldown)[] BiomeTables = {
            ( "Meadow", 2, 10f ), ( "Forest", 2, 10f ), ( "Swamp", 3, 8f )
        };

        [ MenuItem( "Tools/Agent Tools/Animals/Build fireflies" ) ]
        public static void Build() {
            ImportTexture();
            var material = BuildMaterial();
            var prefab = BuildPrefab( material );
            foreach( var ( biome, maxAlive, cooldown ) in BiomeTables ) {
                EnsureInBiome( biome, prefab, maxAlive, cooldown, SpawnTime.Night );
            }
            AssetDatabase.SaveAssets();
        }

        private static void ImportTexture() {
            AssetDatabase.ImportAsset( TexturePath, ImportAssetOptions.ForceSynchronousImport );
            var importer = ( TextureImporter ) AssetImporter.GetAtPath( TexturePath );
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // URP's particle shader, set up by hand the way its material inspector does for a transparent, additive material.
        private static Material BuildMaterial() {
            var shader = Shader.Find( "Universal Render Pipeline/Particles/Unlit" );
            if( shader == null ) {
                throw new InvalidOperationException( "The URP particle shader was not found" );
            }
            Directory.CreateDirectory( Path.GetDirectoryName( MaterialPath ) );
            var material = AssetDatabase.LoadAssetAtPath<Material>( MaterialPath );
            if( material == null ) {
                material = new Material( shader );
                AssetDatabase.CreateAsset( material, MaterialPath );
            }
            material.shader = shader;
            material.SetTexture( "_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>( TexturePath ) );
            material.SetColor( "_BaseColor", Color.white );
            material.SetFloat( "_Surface", 1f );
            material.SetFloat( "_Blend", 2f );
            material.SetFloat( "_SrcBlend", ( float ) UnityEngine.Rendering.BlendMode.SrcAlpha );
            material.SetFloat( "_DstBlend", ( float ) UnityEngine.Rendering.BlendMode.One );
            material.SetFloat( "_SrcBlendAlpha", ( float ) UnityEngine.Rendering.BlendMode.One );
            material.SetFloat( "_DstBlendAlpha", ( float ) UnityEngine.Rendering.BlendMode.One );
            material.SetFloat( "_ZWrite", 0f );
            material.EnableKeyword( "_SURFACE_TYPE_TRANSPARENT" );
            material.SetOverrideTag( "RenderType", "Transparent" );
            material.renderQueue = ( int ) UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty( material );
            return material;
        }

        private static FireflySwarm BuildPrefab( Material material ) {
            var root = new GameObject( "FireflySwarm" );
            var swarm = root.AddComponent<FireflySwarm>();

            var child = new GameObject( "Particles" );
            child.transform.SetParent( root.transform, false );
            var particles = child.AddComponent<ParticleSystem>();
            ConfigureParticles( particles, material );

            var serialized = new SerializedObject( swarm );
            serialized.FindProperty( "particles" ).objectReferenceValue = particles;
            serialized.FindProperty( "fadeSeconds" ).floatValue = 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory( Path.GetDirectoryName( PrefabPath ) );
            var prefab = PrefabUtility.SaveAsPrefabAsset( root, PrefabPath );
            UnityEngine.Object.DestroyImmediate( root );
            UnityEngine.Debug.Log( "[FireflyAssetBuilder] Built " + PrefabPath );
            return prefab.GetComponent<FireflySwarm>();
        }

        // About six fireflies over a patch of a metre and a half, each fading in and out (blinking) over a few seconds while it drifts.
        private static void ConfigureParticles( ParticleSystem particles, Material material ) {
            var main = particles.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve( 3f, 5f );
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve( 0.09f, 0.13f );
            main.startColor = new ParticleSystem.MinMaxGradient( new Color( 0.85f, 1f, 0.35f, 1f ), new Color( 1f, 0.85f, 0.3f, 1f ) );
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 8;

            var emission = particles.emission;
            emission.rateOverTime = 1.6f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3( 0f, 0.25f, 0f );
            shape.scale = new Vector3( 1.4f, 0.35f, 1.4f );

            var blink = new Gradient();
            blink.SetKeys(
                new[] { new GradientColorKey( Color.white, 0f ), new GradientColorKey( Color.white, 1f ) },
                new[] {
                    new GradientAlphaKey( 0f, 0f ), new GradientAlphaKey( 1f, 0.15f ), new GradientAlphaKey( 0.2f, 0.35f ),
                    new GradientAlphaKey( 1f, 0.55f ), new GradientAlphaKey( 0.25f, 0.75f ), new GradientAlphaKey( 0f, 1f )
                } );
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient( blink );

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Renders the swarm against a night sky into one PNG (three moments); needs a graphics device, so no -nographics.</summary>
        [ MenuItem( "Tools/Agent Tools/Animals/Render firefly preview" ) ]
        public static void RenderPreview() {
            string output = Environment.GetEnvironmentVariable( "ANIMAL_PREVIEW" ) ?? "Temp/firefly_preview.png";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>( PrefabPath );
            var scene = EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Single );
            var swarm = ( GameObject ) PrefabUtility.InstantiatePrefab( prefab, scene );
            var particles = swarm.GetComponentInChildren<ParticleSystem>();

            var cameraObject = new GameObject( "Camera" );
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 0.9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color( 0.05f, 0.09f, 0.13f );
            cameraObject.transform.position = new Vector3( 0f, 2f, -2f );
            cameraObject.transform.LookAt( new Vector3( 0f, 0.2f, 0f ) );

            const int size = 512;
            var sheet = new Texture2D( size * 3, size, TextureFormat.RGB24, false );
            var target = new RenderTexture( size, size, 24 );
            camera.targetTexture = target;
            for( int i = 0; i < 3; i++ ) {
                particles.Simulate( 6f + i * 1.3f, true, true );
                camera.Render();
                RenderTexture.active = target;
                var cell = new Texture2D( size, size, TextureFormat.RGB24, false );
                cell.ReadPixels( new Rect( 0, 0, size, size ), 0, 0 );
                cell.Apply();
                sheet.SetPixels( i * size, 0, size, size, cell.GetPixels() );
                UnityEngine.Object.DestroyImmediate( cell );
            }
            RenderTexture.active = null;
            Directory.CreateDirectory( Path.GetDirectoryName( Path.GetFullPath( output ) ) );
            File.WriteAllBytes( output, sheet.EncodeToPNG() );
            UnityEngine.Debug.Log( "[FireflyAssetBuilder] Preview written to " + Path.GetFullPath( output ) );
        }
    }
}
