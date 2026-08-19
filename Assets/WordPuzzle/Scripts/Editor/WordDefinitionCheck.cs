#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace WordPuzzle.Editor
{
    /// <summary>
    /// Diagnostic for the word-meaning feature. Answers the only question that matters when
    /// no meaning appears in game: is the data broken, or is the service missing from the
    /// scene? This checks the data half without entering play mode.
    /// </summary>
    public static class WordDefinitionCheck
    {
        [Serializable]
        private class SenseDto
        {
            public string pos;
            public string meaning;
        }

        [Serializable]
        private class EntryDto
        {
            public string word;
            public string @base;
            public SenseDto[] senses;
        }

        [Serializable]
        private class FileDto
        {
            public EntryDto[] entries;
        }

        [MenuItem("Aurora Words/Check Word Definitions")]
        public static void Check()
        {
            var asset = Resources.Load<TextAsset>("word_definitions");
            if (asset == null)
            {
                Debug.LogError("[Definitions] Resources/word_definitions.json NOT FOUND. " +
                               "The file must sit in a folder named Resources and be imported as a TextAsset.");
                return;
            }

            Debug.Log($"[Definitions] Loaded asset, {asset.text.Length / 1024} KB of text.");

            FileDto file;
            try
            {
                file = JsonUtility.FromJson<FileDto>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Definitions] JsonUtility failed: {e.Message}");
                return;
            }

            if (file?.entries == null || file.entries.Length == 0)
            {
                Debug.LogError("[Definitions] Parsed, but entries came back empty. " +
                               "JsonUtility returns empty rather than throwing when the shape does not match.");
                return;
            }

            Debug.Log($"[Definitions] Parsed {file.entries.Length} entries.");

            int withSenses = 0, withBase = 0;
            foreach (EntryDto e in file.entries)
            {
                if (e.senses != null && e.senses.Length > 0) withSenses++;
                else if (!string.IsNullOrEmpty(e.@base)) withBase++;
            }
            Debug.Log($"[Definitions] {withSenses} carry senses, {withBase} point at a base form.");

            foreach (string probe in new[] { "STAR", "CATS", "ABASHED", "TRAIL", "BASE" })
            {
                EntryDto hit = null;
                foreach (EntryDto e in file.entries)
                {
                    if (string.Equals(e.word, probe, StringComparison.OrdinalIgnoreCase)) { hit = e; break; }
                }

                if (hit == null) Debug.LogWarning($"[Definitions] {probe}: no entry");
                else if (hit.senses != null && hit.senses.Length > 0)
                    Debug.Log($"[Definitions] {probe}: {hit.senses[0].pos} - {hit.senses[0].meaning}");
                else Debug.Log($"[Definitions] {probe}: base form -> {hit.@base}");
            }
        }
    }
}
#endif
