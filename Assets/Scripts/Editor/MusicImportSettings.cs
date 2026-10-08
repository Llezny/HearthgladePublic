using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Songs of the game are long and are loaded through Addressables one at a time, so they are kept as compressed
    // streams instead of the uncompressed PCM a freshly imported WAV is. Applied once, when a clip in the music
    // folder is first imported (a clip that already has import settings keeps them), so settings changed by hand stick.
    public class MusicImportSettings : AssetPostprocessor
    {
        public const string MusicFolder = "Assets/Arts/Sounds/Music/";

        private void OnPreprocessAudio()
        {
            if( !assetPath.StartsWith( MusicFolder ) || !assetImporter.importSettingsMissing )
            {
                return;
            }
            Apply( ( AudioImporter ) assetImporter );
        }

        public static void Apply( AudioImporter importer )
        {
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = false;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = true;
        }
    }
}
