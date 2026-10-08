using System.Collections.Generic;
using System.Linq;
using Hearthglade.Gameplay.Audio;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Map;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer.Unity;

namespace Hearthglade.EditorTools
{
    // Sets up the music: the songs as Addressables (a bundle each), a playlist per place, the audio config, the
    // AppScope prefab as the root scope of dependency injection, and the playlists on the maps. Everything that
    // already exists is kept as it is (so playlists tuned by hand are not overwritten); only what is missing is made.
    public static class AudioAssetBuilder
    {
        private const string MusicFolder = "Assets/Arts/Sounds/Music";
        private const string MenuClip = "Assets/3rd-Party/Music/FantasyAmbience.mp3";
        private const string PlaylistFolder = "Assets/ScriptableObjects/Audio/Music";
        private const string ConfigPath = "Assets/ScriptableObjects/Audio/AudioConfig.asset";
        private const string ScopePrefabPath = "Assets/_Prefabs/Managers/AppScope.prefab";
        private const string VContainerSettingsPath = "Assets/Settings/VContainerSettings.asset";
        private const string MapFolder = "Assets/Resources/ScriptableObjects/Maps";
        private const string MusicGroupName = "Music";

        private static readonly (string name, string[] clips)[] Playlists =
        {
            ( "MenuMusic", new[] { MenuClip } ),
            ( "MainIslandMusic", new[] { $"{MusicFolder}/main_island_1.wav", $"{MusicFolder}/main_island_2.wav", $"{MusicFolder}/main_island_3.wav" } ),
            ( "MosshollowMusic", new[] { $"{MusicFolder}/mosshollow_harbor.wav" } ),
            ( "SnowIslandMusic", new[] { $"{MusicFolder}/snow_island.wav" } ),
        };

        // Maps not listed (caves, expeditions, Dunegate) have no music of their own: what played before keeps playing.
        private static readonly (string map, string playlist)[] MapMusic =
        {
            ( "Home", "MainIslandMusic" ),
            ( "Forest", "MainIslandMusic" ),
            ( "Winter", "SnowIslandMusic" ),
            ( "Rimehaven", "SnowIslandMusic" ),
            ( "Mosshollow", "MosshollowMusic" ),
        };

        [ MenuItem( "Tools/Agent Tools/Audio/Set up music" ) ]
        public static void SetUpMusic()
        {
            var playlists = new Dictionary<string, MusicPlaylistSO>();
            foreach( var ( name, clips ) in Playlists )
            {
                playlists[ name ] = EnsurePlaylist( name, clips );
            }
            var config = EnsureConfig( playlists[ "MenuMusic" ] );
            var scope = EnsureScopePrefab( config );
            EnsureVContainerRootScope( scope );
            AssignMapMusic( playlists );
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log( "[AudioAssetBuilder] Music is set up." );
        }

        private static MusicPlaylistSO EnsurePlaylist( string name, string[] clipPaths )
        {
            string path = $"{PlaylistFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<MusicPlaylistSO>( path );
            if( existing != null )
            {
                return existing;
            }
            var playlist = ScriptableObject.CreateInstance<MusicPlaylistSO>();
            foreach( var clipPath in clipPaths )
            {
                MakeAddressable( clipPath );
                playlist.Tracks.Add( new MusicTrack { Clip = new AssetReferenceT<AudioClip>( AssetDatabase.AssetPathToGUID( clipPath ) ) } );
            }
            EnsureFolder( PlaylistFolder );
            AssetDatabase.CreateAsset( playlist, path );
            return playlist;
        }

        // One bundle per song: loading a song then loads only that song, not all of them.
        private static void MakeAddressable( string clipPath )
        {
            var importer = AssetImporter.GetAtPath( clipPath ) as AudioImporter;
            if( importer == null )
            {
                UnityEngine.Debug.LogError( $"[AudioAssetBuilder] {clipPath} is not an audio clip." );
                return;
            }
            // Clips in the music folder get this when imported; the ones outside it (the menu song) are set here.
            if( !clipPath.StartsWith( MusicFolder ) )
            {
                MusicImportSettings.Apply( importer );
                importer.SaveAndReimport();
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup( MusicGroupName ) ?? CreateMusicGroup( settings );
            var entry = settings.CreateOrMoveEntry( AssetDatabase.AssetPathToGUID( clipPath ), group );
            entry.address = $"Music/{System.IO.Path.GetFileNameWithoutExtension( clipPath )}";
        }

        private static AddressableAssetGroup CreateMusicGroup( AddressableAssetSettings settings )
        {
            var template = settings.FindGroup( "Prefabs" ) ?? settings.DefaultGroup;
            var group = settings.CreateGroup( MusicGroupName, false, false, true, template.Schemas.ToList() );
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            return group;
        }

        private static AudioConfigSO EnsureConfig( MusicPlaylistSO menuMusic )
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioConfigSO>( ConfigPath );
            if( config != null )
            {
                return config;
            }
            config = ScriptableObject.CreateInstance<AudioConfigSO>();
            config.MenuMusic = menuMusic;
            AssetDatabase.CreateAsset( config, ConfigPath );
            return config;
        }

        private static AppScope EnsureScopePrefab( AudioConfigSO config )
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>( ScopePrefabPath );
            if( existing != null )
            {
                return existing.GetComponent<AppScope>();
            }
            var root = new GameObject( "AppScope" );
            var scope = root.AddComponent<AppScope>();
            var serialized = new SerializedObject( scope );
            serialized.FindProperty( "audioConfig" ).objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset( root, ScopePrefabPath );
            Object.DestroyImmediate( root );
            return prefab.GetComponent<AppScope>();
        }

        // VContainerSettings has to be one of the preloaded assets: that is how the root scope is found at runtime.
        private static void EnsureVContainerRootScope( AppScope scope )
        {
            var settings = AssetDatabase.LoadAssetAtPath<VContainerSettings>( VContainerSettingsPath );
            if( settings == null )
            {
                settings = ScriptableObject.CreateInstance<VContainerSettings>();
                EnsureFolder( "Assets/Settings" );
                AssetDatabase.CreateAsset( settings, VContainerSettingsPath );
            }
            settings.RootLifetimeScope = scope;
            EditorUtility.SetDirty( settings );

            var preloaded = PlayerSettings.GetPreloadedAssets().Where( a => a != null && !( a is VContainerSettings ) ).ToList();
            preloaded.Add( settings );
            PlayerSettings.SetPreloadedAssets( preloaded.ToArray() );
        }

        private static void AssignMapMusic( Dictionary<string, MusicPlaylistSO> playlists )
        {
            foreach( var ( map, playlist ) in MapMusic )
            {
                var mapType = AssetDatabase.LoadAssetAtPath<MapSO>( $"{MapFolder}/{map}.asset" );
                if( mapType == null || mapType.Music != null )
                {
                    continue;
                }
                mapType.Music = playlists[ playlist ];
                EditorUtility.SetDirty( mapType );
            }
        }

        private static void EnsureFolder( string folder )
        {
            if( AssetDatabase.IsValidFolder( folder ) )
            {
                return;
            }
            string parent = System.IO.Path.GetDirectoryName( folder ).Replace( '\\', '/' );
            EnsureFolder( parent );
            AssetDatabase.CreateFolder( parent, System.IO.Path.GetFileName( folder ) );
        }
    }
}
