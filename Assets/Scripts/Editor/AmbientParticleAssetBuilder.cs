using System;
using System.IO;
using Hearthglade.Core.Entities;
using Hearthglade.Gameplay.Entities;
using UnityEditor;
using UnityEngine;
using static Hearthglade.EditorTools.AnimalAssetKit;

namespace Hearthglade.EditorTools
{
    /// <summary>
    /// Builds a handful of ambient particle swarms (reusing the FireflySwarm component and its glow
    /// material - no new art) for biomes that don't have any ambient life yet: sunbeam pollen dust for
    /// the wooded biomes, low mist wisps for the swamp, and gentle snow flurries for the tundra.
    /// </summary>
    public static class AmbientParticleAssetBuilder {

        private const string MaterialPath = "Assets/Arts/Materials/VFX/Firefly.mat";

        private struct Spec {
            public string Name;
            public string PrefabPath;
            public Color ColorMin, ColorMax;
            public float SizeMin, SizeMax;
            public int MaxParticles;
            public float RateOverTime;
            public Vector3 ShapePosition, ShapeScale;
            public float LifetimeMin, LifetimeMax;
            public float VelocityY;
            public float NoiseStrength, NoiseFrequency, NoiseScrollSpeed;
            public float FadeInFraction, FadeOutFraction;
            public float FadeSeconds;
            public (string biome, int maxAlive, float cooldown, SpawnTime time)[] Biomes;
        }

        // Sunbeam dust under trees: Forest/DenseForest/Taiga have no ambient life yet (butterflies are
        // Meadow-only, fireflies are night-only) - a subtle daytime effect fills that gap.
        private static readonly Spec Pollen = new Spec {
            Name = "PollenMotes",
            PrefabPath = "Assets/_Prefabs/Entities/PollenMotes.prefab",
            ColorMin = new Color( 1f, 0.95f, 0.75f, 0.5f ), ColorMax = new Color( 1f, 1f, 0.85f, 0.8f ),
            SizeMin = 0.03f, SizeMax = 0.05f,
            MaxParticles = 10, RateOverTime = 1.2f,
            ShapePosition = new Vector3( 0f, 0.3f, 0f ), ShapeScale = new Vector3( 1.2f, 0.6f, 1.2f ),
            LifetimeMin = 4f, LifetimeMax = 7f,
            VelocityY = 0.05f,
            NoiseStrength = 0.15f, NoiseFrequency = 0.5f, NoiseScrollSpeed = 0.2f,
            FadeInFraction = 0.2f, FadeOutFraction = 0.3f,
            FadeSeconds = 2f,
            Biomes = new (string, int, float, SpawnTime)[] {
                ( "Forest", 3, 6f, SpawnTime.Day ), ( "DenseForest", 3, 6f, SpawnTime.Day ), ( "Taiga", 2, 8f, SpawnTime.Day )
            }
        };

        // Low wisps hovering over the swamp, always on (a swamp reads as damp/misty day or night).
        private static readonly Spec Mist = new Spec {
            Name = "MistWisps",
            PrefabPath = "Assets/_Prefabs/Entities/MistWisps.prefab",
            ColorMin = new Color( 0.72f, 0.8f, 0.76f, 0.14f ), ColorMax = new Color( 0.65f, 0.75f, 0.72f, 0.24f ),
            SizeMin = 0.6f, SizeMax = 1f,
            MaxParticles = 4, RateOverTime = 0.35f,
            ShapePosition = new Vector3( 0f, 0.15f, 0f ), ShapeScale = new Vector3( 2.5f, 0.3f, 2.5f ),
            LifetimeMin = 6f, LifetimeMax = 10f,
            VelocityY = 0f,
            NoiseStrength = 0.3f, NoiseFrequency = 0.25f, NoiseScrollSpeed = 0.08f,
            FadeInFraction = 0.35f, FadeOutFraction = 0.35f,
            FadeSeconds = 3f,
            Biomes = new (string, int, float, SpawnTime)[] { ( "Swamp", 2, 12f, SpawnTime.Always ) }
        };

        // Small flecks drifting down out of a ceiling box, matching the Tundra's cold reskin from Phase 6.
        private static readonly Spec Snow = new Spec {
            Name = "SnowFlurries",
            PrefabPath = "Assets/_Prefabs/Entities/SnowFlurries.prefab",
            ColorMin = new Color( 0.9f, 0.95f, 1f, 0.85f ), ColorMax = new Color( 1f, 1f, 1f, 1f ),
            SizeMin = 0.025f, SizeMax = 0.045f,
            MaxParticles = 14, RateOverTime = 3f,
            ShapePosition = new Vector3( 0f, 1f, 0f ), ShapeScale = new Vector3( 2.5f, 0.1f, 2.5f ),
            LifetimeMin = 3f, LifetimeMax = 5f,
            VelocityY = -0.12f,
            NoiseStrength = 0.12f, NoiseFrequency = 0.6f, NoiseScrollSpeed = 0.3f,
            FadeInFraction = 0.15f, FadeOutFraction = 0.15f,
            FadeSeconds = 2f,
            Biomes = new (string, int, float, SpawnTime)[] { ( "Tundra", 3, 10f, SpawnTime.Always ) }
        };

        [ MenuItem( "Tools/Agent Tools/Animals/Build ambient particles" ) ]
        public static void Build() {
            var material = AssetDatabase.LoadAssetAtPath<Material>( MaterialPath );
            if( material == null ) {
                throw new InvalidOperationException( "Firefly.mat not found - run Tools/Agent Tools/Animals/Build fireflies first, its glow material is reused here." );
            }

            foreach( var spec in new[] { Pollen, Mist, Snow } ) {
                var prefab = BuildPrefab( spec, material );
                foreach( var ( biome, maxAlive, cooldown, time ) in spec.Biomes ) {
                    EnsureInBiome( biome, prefab, maxAlive, cooldown, time );
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static FireflySwarm BuildPrefab( Spec spec, Material material ) {
            var root = new GameObject( spec.Name );
            var swarm = root.AddComponent<FireflySwarm>();

            var child = new GameObject( "Particles" );
            child.transform.SetParent( root.transform, false );
            var particles = child.AddComponent<ParticleSystem>();
            ConfigureParticles( particles, material, spec );

            var serialized = new SerializedObject( swarm );
            serialized.FindProperty( "particles" ).objectReferenceValue = particles;
            serialized.FindProperty( "fadeSeconds" ).floatValue = spec.FadeSeconds;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory( Path.GetDirectoryName( spec.PrefabPath ) );
            var prefab = PrefabUtility.SaveAsPrefabAsset( root, spec.PrefabPath );
            UnityEngine.Object.DestroyImmediate( root );
            UnityEngine.Debug.Log( "[AmbientParticleAssetBuilder] Built " + spec.PrefabPath );
            return prefab.GetComponent<FireflySwarm>();
        }

        private static void ConfigureParticles( ParticleSystem particles, Material material, Spec spec ) {
            var main = particles.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve( spec.LifetimeMin, spec.LifetimeMax );
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve( spec.SizeMin, spec.SizeMax );
            main.startColor = new ParticleSystem.MinMaxGradient( spec.ColorMin, spec.ColorMax );
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = spec.MaxParticles;

            var emission = particles.emission;
            emission.rateOverTime = spec.RateOverTime;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = spec.ShapePosition;
            shape.scale = spec.ShapeScale;

            if( Math.Abs( spec.VelocityY ) > 0.0001f ) {
                var velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.y = new ParticleSystem.MinMaxCurve( spec.VelocityY );
            }

            // A single soft fade in/out instead of the firefly's multi-key blink: these effects drift
            // rather than twinkle.
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey( Color.white, 0f ), new GradientColorKey( Color.white, 1f ) },
                new[] {
                    new GradientAlphaKey( 0f, 0f ), new GradientAlphaKey( 1f, spec.FadeInFraction ),
                    new GradientAlphaKey( 1f, 1f - spec.FadeOutFraction ), new GradientAlphaKey( 0f, 1f )
                } );
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient( fade );

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = spec.NoiseStrength;
            noise.frequency = spec.NoiseFrequency;
            noise.scrollSpeed = spec.NoiseScrollSpeed;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
