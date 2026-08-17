using System;
using System.Collections.Generic;
using UnityEngine;
using ServiceLocatorFramework;

namespace WordPuzzle.Services
{
    /// <summary>
    /// The player's word collection: every target word in the game, and which of them they
    /// have discovered. This is the "how many words have I learned" number, and it is
    /// deliberately shared across all game modes - a collection that resets per mode would
    /// punish players for trying Time Trial, and the point is a single total that only grows.
    /// </summary>
    public class WordCollectionService : MonoBehaviour
    {
        private const string PrefKeyDiscovered = "WordCollection_Discovered";
        private const string TargetListResource = "word_list_targets";

        /// <summary>Every collectable word, alphabetical. Bonus-only words are not collectable.</summary>
        private readonly List<string> _allWords = new List<string>();

        private readonly HashSet<string> _discovered =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool _dirty;

        /// <summary>Raised when a word is discovered for the first time, with that word.</summary>
        public event Action<string> WordDiscovered;

        public IReadOnlyList<string> AllWords => _allWords;
        public int TotalCount => _allWords.Count;
        public int DiscoveredCount => _discovered.Count;

        public float CompletionFraction =>
            _allWords.Count == 0 ? 0f : _discovered.Count / (float)_allWords.Count;

        private void Awake()
        {
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<WordCollectionService>())
            {
                ServiceLocator.Current.Register<WordCollectionService>(this);
            }

            LoadCatalogue();
            LoadDiscovered();
        }

        private void OnDestroy()
        {
            Save();

            if (ServiceLocator.Current != null
                && ServiceLocator.Current.Has<WordCollectionService>()
                && ServiceLocator.Current.Get<WordCollectionService>() == this)
            {
                ServiceLocator.Current.Unregister<WordCollectionService>();
            }
        }

        // Writes are batched rather than per word: a level can add eight entries, and
        // PlayerPrefs.Save() flushes to disk every time it is called.
        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationQuit() => Save();

        private void LoadCatalogue()
        {
            var asset = Resources.Load<TextAsset>(TargetListResource);
            if (asset == null)
            {
                Debug.LogWarning($"[WordCollectionService] Resources/{TargetListResource}.txt not found - " +
                                 "the collection screen will be empty.");
                return;
            }

            foreach (string line in asset.text.Split('\n'))
            {
                string word = line.Trim().ToUpperInvariant();
                if (word.Length > 0) _allWords.Add(word);
            }

            _allWords.Sort(StringComparer.Ordinal);
            Resources.UnloadAsset(asset);
        }

        private void LoadDiscovered()
        {
            string stored = PlayerPrefs.GetString(PrefKeyDiscovered, string.Empty);
            if (string.IsNullOrEmpty(stored)) return;

            foreach (string word in stored.Split(','))
            {
                if (word.Length > 0) _discovered.Add(word);
            }
        }

        /// <summary>
        /// Records a find. Returns true only the first time a word is seen, so callers can
        /// celebrate a genuinely new discovery without tracking that themselves.
        /// </summary>
        public bool Discover(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;

            string key = word.Trim().ToUpperInvariant();
            if (!_discovered.Add(key)) return false;

            _dirty = true;
            WordDiscovered?.Invoke(key);
            return true;
        }

        public bool IsDiscovered(string word) =>
            !string.IsNullOrEmpty(word) && _discovered.Contains(word.Trim());

        /// <summary>How many collectable words start with the given letter, and how many are found.</summary>
        public void GetLetterProgress(char letter, out int found, out int total)
        {
            found = 0;
            total = 0;
            char upper = char.ToUpperInvariant(letter);

            foreach (string word in _allWords)
            {
                if (word.Length == 0 || word[0] != upper) continue;
                total++;
                if (_discovered.Contains(word)) found++;
            }
        }

        public void Save()
        {
            if (!_dirty) return;

            PlayerPrefs.SetString(PrefKeyDiscovered, string.Join(",", _discovered));
            PlayerPrefs.Save();
            _dirty = false;
        }

        /// <summary>Clears the collection. Wired to Reset Progress alongside the other saves.</summary>
        public void ResetCollection()
        {
            _discovered.Clear();
            PlayerPrefs.DeleteKey(PrefKeyDiscovered);
            PlayerPrefs.Save();
            _dirty = false;
        }
    }
}
