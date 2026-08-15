using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ServiceLocatorFramework;

namespace WordPuzzle.Services
{
    /// <summary>
    /// Looks up dictionary meanings for words the player finds. Definitions come from a
    /// trimmed WordNet extract in Resources/word_definitions.json - bundled rather than
    /// fetched, so meanings work offline and never stall the level-complete screen.
    /// </summary>
    public class WordDefinitionService : MonoBehaviour
    {
        private const string ResourcePath = "word_definitions";

        // Serialized shapes matching the JSON. JsonUtility needs concrete classes with public
        // fields; it cannot deserialize a dictionary, which is why the file is an array.
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
            public string @base;    // set on inflected forms: CATS -> CAT
            public SenseDto[] senses;
        }

        [Serializable]
        private class FileDto
        {
            public EntryDto[] entries;
        }

        private readonly Dictionary<string, EntryDto> _entries =
            new Dictionary<string, EntryDto>(StringComparer.OrdinalIgnoreCase);

        /// <summary>False until the JSON has finished parsing; lookups return null before that.</summary>
        public bool IsReady { get; private set; }

        private void Awake()
        {
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<WordDefinitionService>())
            {
                ServiceLocator.Current.Register<WordDefinitionService>(this);
            }

            StartCoroutine(LoadAsync());
        }

        private void OnDestroy()
        {
            if (ServiceLocator.Current != null
                && ServiceLocator.Current.Has<WordDefinitionService>()
                && ServiceLocator.Current.Get<WordDefinitionService>() == this)
            {
                ServiceLocator.Current.Unregister<WordDefinitionService>();
            }
        }

        /// <summary>
        /// Loaded off the first frame rather than in Awake: the file is ~3 MB and parsing it
        /// synchronously at boot costs a visible hitch on a low-end phone. Nothing needs a
        /// definition until a word is solved, so a frame or two of delay is free.
        /// </summary>
        private IEnumerator LoadAsync()
        {
            ResourceRequest request = Resources.LoadAsync<TextAsset>(ResourcePath);
            yield return request;

            var asset = request.asset as TextAsset;
            if (asset == null)
            {
                Debug.LogWarning($"[WordDefinitionService] Resources/{ResourcePath}.json not found - " +
                                 "word meanings will be unavailable.");
                yield break;
            }

            FileDto file = JsonUtility.FromJson<FileDto>(asset.text);
            if (file?.entries == null)
            {
                Debug.LogWarning("[WordDefinitionService] Definitions file could not be parsed.");
                yield break;
            }

            foreach (EntryDto entry in file.entries)
            {
                if (!string.IsNullOrEmpty(entry.word)) _entries[entry.word] = entry;
            }

            Resources.UnloadAsset(asset);
            IsReady = true;
        }

        public bool HasDefinition(string word) => TryResolve(word, out _);

        /// <summary>
        /// The primary meaning, or null when the word has none. WordNet orders senses by how
        /// common they are, so the first one is the reading a player most likely means.
        /// </summary>
        public string GetPrimaryMeaning(string word)
        {
            if (!TryResolve(word, out EntryDto entry)) return null;
            return entry.senses[0].meaning;
        }

        /// <summary>Part of speech of the primary meaning ("noun", "verb", ...), or null.</summary>
        public string GetPrimaryPartOfSpeech(string word)
        {
            if (!TryResolve(word, out EntryDto entry)) return null;
            return entry.senses[0].pos;
        }

        /// <summary>
        /// The lemma a word inflects from (CATS -> CAT), or null when the word is its own
        /// base form. Lets the UI caption a plural without repeating the parent's text.
        /// </summary>
        public string GetBaseForm(string word)
        {
            if (!_entries.TryGetValue(word ?? string.Empty, out EntryDto entry)) return null;
            return string.IsNullOrEmpty(entry.@base) ? null : entry.@base;
        }

        public int GetSenseCount(string word) => TryResolve(word, out EntryDto entry) ? entry.senses.Length : 0;

        /// <summary>All meanings, most common first. Empty when the word has none.</summary>
        public IReadOnlyList<string> GetAllMeanings(string word)
        {
            if (!TryResolve(word, out EntryDto entry)) return Array.Empty<string>();

            var list = new List<string>(entry.senses.Length);
            foreach (SenseDto sense in entry.senses) list.Add(sense.meaning);
            return list;
        }

        /// <summary>
        /// Finds the entry that actually carries senses, following the one base-form hop that
        /// inflected words use. The generator guarantees every pointer resolves, but the hop
        /// is depth-limited anyway so a bad file cannot spin here.
        /// </summary>
        private bool TryResolve(string word, out EntryDto entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(word) || !IsReady) return false;

            string key = word.Trim();
            for (int hop = 0; hop < 2; hop++)
            {
                if (!_entries.TryGetValue(key, out EntryDto found)) return false;

                if (found.senses != null && found.senses.Length > 0)
                {
                    entry = found;
                    return true;
                }

                if (string.IsNullOrEmpty(found.@base)) return false;
                key = found.@base;
            }
            return false;
        }
    }
}
