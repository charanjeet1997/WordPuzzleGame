#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WordPuzzle.Editor
{
    /// <summary>
    /// Forces streaming Vorbis on background music. The tracks ship as WAV, and at Unity's
    /// default "Decompress On Load" a handful of multi-megabyte loops sits in memory
    /// uncompressed for the whole session - real cost on a mobile build for audio that is
    /// played start to finish exactly once at a time.
    /// </summary>
    public class MusicImportSettings : AssetPostprocessor
    {
        private const string MusicFolder = "Assets/WordPuzzle/Audio/Music";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicFolder)) return;

            var importer = (AudioImporter)assetImporter;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            // preloadAudioData moved onto the per-platform sample settings; the importer-level
            // property is obsolete. Streaming clips must not preload anyway.
            settings.preloadAudioData = false;

            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
        }
    }
}
#endif
