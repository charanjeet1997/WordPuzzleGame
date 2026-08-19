#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace WordPuzzle.Editor
{
    /// <summary>
    /// Turns any .ttf/.otf in the project into a TextMeshPro font asset without going through
    /// the Font Asset Creator window, and wires it into the grid and wheel in one step.
    /// </summary>
    public static class FontAssetBuilder
    {
        private const string FontsFolder = "Assets/WordPuzzle/Fonts";
        private const string OutputFolder = "Assets/WordPuzzle/Fonts/Generated";

        /// <summary>
        /// Only the weights the design actually calls for. The downloaded families ship every
        /// weight and width variant, and building an SDF atlas for all of them is slow and
        /// leaves dozens of unused assets in the project.
        /// </summary>
        private static readonly string[] RequiredFonts =
        {
            "Fredoka-SemiBold",   // design: Fredoka 600 - wordmark, chapter name
            "Fredoka-Bold",       // design: Fredoka 700 - titles, PLAY
            "Nunito-Bold",        // design: Nunito 700 - body, labels
            "Nunito-ExtraBold"    // design: Nunito 800 - pills, counters, tile + wheel letters
        };

        [MenuItem("Aurora Words/Build TMP Font Assets From Fonts Folder")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(FontsFolder))
            {
                Directory.CreateDirectory(FontsFolder);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("No fonts found",
                    $"Created {FontsFolder}.\n\nDrop a .ttf or .otf in there, then run this again.", "OK");
                return;
            }

            string[] allPaths = AssetDatabase.FindAssets("t:Font", new[] { FontsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".ttf") || p.EndsWith(".otf"))
                .ToArray();

            if (allPaths.Length == 0)
            {
                EditorUtility.DisplayDialog("No fonts found",
                    $"Put a .ttf or .otf inside {FontsFolder} first.", "OK");
                return;
            }

            // Match on exact filename so "Fredoka-Bold" never picks up "Fredoka_Condensed-Bold".
            string[] fontPaths = allPaths
                .Where(p => RequiredFonts.Contains(Path.GetFileNameWithoutExtension(p)))
                .ToArray();

            if (fontPaths.Length == 0)
            {
                EditorUtility.DisplayDialog("No matching weights",
                    $"Found {allPaths.Length} font file(s) but none named:\n\n" +
                    string.Join("\n", RequiredFonts) +
                    $"\n\nCheck the filenames under {FontsFolder}.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder(FontsFolder, "Generated");
            }

            int built = 0;
            foreach (string path in fontPaths)
            {
                Font source = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (source == null) continue;

                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source);
                if (asset == null)
                {
                    Debug.LogWarning($"[FontAssetBuilder] Could not create a TMP asset from {path}.");
                    continue;
                }

                string outPath = $"{OutputFolder}/{Path.GetFileNameWithoutExtension(path)} SDF.asset";
                AssetDatabase.DeleteAsset(outPath);
                AssetDatabase.CreateAsset(asset, outPath);

                // The atlas and material are sub-assets; without this they are lost on reload.
                if (asset.atlasTextures != null)
                {
                    foreach (Texture2D tex in asset.atlasTextures)
                    {
                        if (tex != null) AssetDatabase.AddObjectToAsset(tex, asset);
                    }
                }
                if (asset.material != null) AssetDatabase.AddObjectToAsset(asset.material, asset);

                EditorUtility.SetDirty(asset);
                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("TMP font assets built",
                $"Created {built} of {RequiredFonts.Length} font asset(s) in {OutputFolder}.\n\n" +
                "Next: assign 'Nunito-ExtraBold SDF' to Letter Font on both CrosswordGrid and " +
                "LetterWheel in WordPuzzleWorld.prefab.",
                "OK");
        }
    }
}
#endif
