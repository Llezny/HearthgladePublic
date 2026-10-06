using System;
using System.IO;
using Hearthglade.Gameplay.Environment;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    /// <summary>
    /// Builds the dust puff shown when a building piece is placed: a generated cloud texture, an alpha-blended
    /// material and a one-shot particle burst prefab under Resources, so PlaceDust can load it without scene wiring.
    /// </summary>
    public static class PlaceDustAssetBuilder {

        private const string TexturePath = "Assets/Arts/Textures/VFX/DustPuff.png";
        private const string MaterialPath = "Assets/Arts/Materials/VFX/PlaceDust.mat";
        private const string PrefabPath = "Assets/Resources/VFX/PlaceDust.prefab";

        [ MenuItem( "LuakszTools/Building/Build place dust" ) ]
        public static void Build() {
            BuildTexture();
            var material = BuildMaterial();
            BuildPrefab( material );
            AssetDatabase.SaveAssets();
        }

        // A lumpy cloud: a mostly opaque core with a soft edge, wobbled by a few sine lobes so it doesn't read
        // as a perfect circle (SoftGlow is too faint and round for dust).
        private static void BuildTexture() {
            const int size = 128;
            var pixels = new Color32[ size * size ];
            for( int y = 0; y < size; y++ ) {
                for( int x = 0; x < size; x++ ) {
                    float dx = ( x + 0.5f ) / size * 2f - 1f;
                    float dy = ( y + 0.5f ) / size * 2f - 1f;
                    float angle = Mathf.Atan2( dy, dx );
                    float radius = Mathf.Sqrt( dx * dx + dy * dy );
                    float lobes = 1f + 0.12f * Mathf.Sin( angle * 3f + 1f ) + 0.08f * Mathf.Sin( angle * 5f + 2f );
                    float t = radius / ( 0.9f * lobes );
                    float alpha = 1f - Mathf.SmoothStep( 0.35f, 1f, t );
                    pixels[ y * size + x ] = new Color32( 255, 255, 255, ( byte ) Mathf.RoundToInt( alpha * 255f ) );
                }
            }
            var texture = new Texture2D( size, size, TextureFormat.RGBA32, false );
            texture.SetPixels32( pixels );
            Directory.CreateDirectory( Path.GetDirectoryName( TexturePath ) );
            File.WriteAllBytes( TexturePath, texture.EncodeToPNG() );
            UnityEngine.Object.DestroyImmediate( texture );

            AssetDatabase.ImportAsset( TexturePath, ImportAssetOptions.ForceSynchronousImport );
            var importer = ( TextureImporter ) AssetImporter.GetAtPath( TexturePath );
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // URP's particle shader set up the way its inspector does for a transparent, alpha-blended material.
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
            material.SetFloat( "_Blend", 0f );
            material.SetFloat( "_SrcBlend", ( float ) UnityEngine.Rendering.BlendMode.SrcAlpha );
            material.SetFloat( "_DstBlend", ( float ) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
            material.SetFloat( "_SrcBlendAlpha", ( float ) UnityEngine.Rendering.BlendMode.One );
            material.SetFloat( "_DstBlendAlpha", ( float ) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
            material.SetFloat( "_ZWrite", 0f );
            material.EnableKeyword( "_SURFACE_TYPE_TRANSPARENT" );
            material.SetOverrideTag( "RenderType", "Transparent" );
            material.renderQueue = ( int ) UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty( material );
            return material;
        }

        private static void BuildPrefab( Material material ) {
            var root = new GameObject( "PlaceDust" );
            var dust = root.AddComponent<PlaceDust>();

            var child = new GameObject( "Particles" );
            child.transform.SetParent( root.transform, false );
            var particles = child.AddComponent<ParticleSystem>();
            ConfigureParticles( particles, material );

            var serialized = new SerializedObject( dust );
            serialized.FindProperty( "particles" ).objectReferenceValue = particles;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory( Path.GetDirectoryName( PrefabPath ) );
            PrefabUtility.SaveAsPrefabAsset( root, PrefabPath );
            UnityEngine.Object.DestroyImmediate( root );
            UnityEngine.Debug.Log( "[PlaceDustAssetBuilder] Built " + PrefabPath );
        }

        private static void ConfigureParticles( ParticleSystem particles, Material material ) {
            var main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve( 0.6f, 1f );
            main.startSpeed = new ParticleSystem.MinMaxCurve( 0.2f, 0.4f );
            main.startSize = new ParticleSystem.MinMaxCurve( 0.22f, 0.36f );
            main.startRotation = new ParticleSystem.MinMaxCurve( 0f, Mathf.PI * 2f );
            main.startColor = new ParticleSystem.MinMaxGradient( new Color( 0.8f, 0.74f, 0.62f, 0.8f ), new Color( 0.9f, 0.85f, 0.75f, 0.95f ) );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts( new[] { new ParticleSystem.Burst( 0f, 12, 16 ) } );

            // A circle lies in the shape's XY plane; turned flat it hugs the ground and pushes puffs outward.
            // PlaceDust scales it to the piece's footprint.
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 0.35f;
            shape.rotation = new Vector3( 90f, 0f, 0f );
            shape.position = new Vector3( 0f, 0.1f, 0f );

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve( 0f, 0f );
            velocity.z = new ParticleSystem.MinMaxCurve( 0f, 0f );
            velocity.y = new ParticleSystem.MinMaxCurve( 0.08f, 0.16f );

            // Slows down as it spreads: ease the puffs to a stop instead of drifting at constant speed.
            var limit = particles.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.12f;
            limit.limit = new ParticleSystem.MinMaxCurve( 0.2f );

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve( 1f, AnimationCurve.EaseInOut( 0f, 0.6f, 1f, 1.4f ) );

            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve( -0.6f, 0.6f );

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey( Color.white, 0f ), new GradientColorKey( Color.white, 1f ) },
                new[] { new GradientAlphaKey( 0f, 0f ), new GradientAlphaKey( 1f, 0.15f ), new GradientAlphaKey( 0f, 1f ) } );
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient( fade );

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
