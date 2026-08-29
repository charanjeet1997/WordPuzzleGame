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

        /// <summary>
        /// Marks a bitmask payload, as "b1:signature:base64". Anything without this prefix is
        /// the older comma-joined word list and is read once, then rewritten in the new form.
        /// </summary>
        private const string BitmaskPrefix = "b1:";

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
        // GameStorage.Save() flushes to disk every time it is called.
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
            string stored = GameStorage.GetString(PrefKeyDiscovered, string.Empty);
            if (string.IsNullOrEmpty(stored)) return;

            if (stored.StartsWith(BitmaskPrefix, StringComparison.Ordinal))
            {
                LoadBitmask(stored);
                return;
            }

            // Legacy comma-joined list. Read as-is; the next Save writes a bitmask.
            foreach (string word in stored.Split(','))
            {
                if (word.Length > 0) _discovered.Add(word);
            }

            _dirty = true;
        }

        /// <summary>
        /// A bit per catalogue word, packed and base64'd. The comma-joined list reached about
        /// 50 KB at full completion, which is a heavy write against the 1 MB the portal allows
        /// for the whole save; the bitmask is roughly 1 KB regardless of progress.
        ///
        /// Bit positions are indices into the alphabetical catalogue, so they are only
        /// meaningful for the exact word list that produced them. The signature guards that.
        /// </summary>
        private void LoadBitmask(string stored)
        {
            string[] parts = stored.Split(':');
            if (parts.Length != 3)
            {
                Debug.LogWarning("[WordCollectionService] Malformed collection payload - ignoring.");
                return;
            }

            if (parts[1] != CatalogueSignature())
            {
                // The word list changed since this was written, so every bit now points at a
                // different word. Discarding is the only honest option: keeping them would
                // show words the player never found.
                Debug.LogWarning("[WordCollectionService] Saved collection was built against a " +
                                 "different word list and has been discarded.");
                _dirty = true;
                return;
            }

            byte[] bits;
            try
            {
                bits = Convert.FromBase64String(parts[2]);
            }
            catch (FormatException)
            {
                Debug.LogWarning("[WordCollectionService] Collection payload was not valid base64.");
                return;
            }

            for (int i = 0; i < _allWords.Count; i++)
            {
                int index = i >> 3;
                if (index >= bits.Length) break;

                if ((bits[index] & (1 << (i & 7))) != 0) _discovered.Add(_allWords[i]);
            }
        }

        private string EncodeBitmask()
        {
            var bits = new byte[(_allWords.Count + 7) / 8];

            for (int i = 0; i < _allWords.Count; i++)
            {
                if (!_discovered.Contains(_allWords[i])) continue;
                bits[i >> 3] |= (byte)(1 << (i & 7));
            }

            return BitmaskPrefix + CatalogueSignature() + ":" + Convert.ToBase64String(bits);
        }

        /// <summary>
        /// Identifies the catalogue a bitmask was built against - word count plus an FNV-1a
        /// hash of the words themselves. Cheap, and changes if a single word is added,
        /// removed or reordered.
        /// </summary>
        private string CatalogueSignature()
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;

                uint hash = offset;
                foreach (string word in _allWords)
                {
                    foreach (char c in word) hash = (hash ^ c) * prime;
                    hash = (hash ^ (byte)'|') * prime;
                }

                return _allWords.Count.ToString() + "-" + hash.ToString("x8");
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

            GameStorage.SetString(PrefKeyDiscovered, EncodeBitmask());
            GameStorage.Save();
            _dirty = false;
        }

        /// <summary>Clears the collection. Wired to Reset Progress alongside the other saves.</summary>
        public void ResetCollection()
        {
            _discovered.Clear();
            GameStorage.DeleteKey(PrefKeyDiscovered);
            GameStorage.Save();
            _dirty = false;
        }
    }
}
