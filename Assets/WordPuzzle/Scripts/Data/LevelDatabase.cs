using System.Collections.Generic;
using UnityEngine;
using WordPuzzle.Services;

namespace WordPuzzle.Data
{
    /// <summary>
    /// Holds every authored level. Levels are plain serializable data stored inline, so a
    /// large campaign is one file on disk and one reference in the scene rather than N
    /// ScriptableObject assets.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "WordPuzzle/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField] private List<LevelData> levels = new List<LevelData>();

        [Header("Chapter Ordering")]
        [Tooltip("Levels grouped into one chapter. Order is shuffled within a chapter, never across.")]
        [SerializeField] private int levelsPerChapter = 20;

        [Tooltip("Off plays levels in authored order.")]
        [SerializeField] private bool shuffleWithinChapter = true;

        private const string PrefKeyShuffleSeed = "LevelShuffleSeed";

        public int Count => levels.Count;

        public List<LevelData> Levels => levels;

        public int LevelsPerChapter => Mathf.Max(1, levelsPerChapter);

        /// <summary>
        /// Levels are 1-based and wrap, so a level number beyond the authored set
        /// still resolves instead of returning null.
        /// <para>
        /// With shuffling on, progression walks chapters in order but visits the levels inside
        /// a chapter in a shuffled order. The shuffle is derived from a per-player seed and the
        /// chapter index rather than drawn at random each time, which is what makes it
        /// repeat-free within a chapter and identical after a reload - picking randomly on
        /// entry would hand out the same level twice and break resume.
        /// </para>
        /// </summary>
        public LevelData GetLevel(int levelNumber)
        {
            if (levels == null || levels.Count == 0) return null;

            int index = (levelNumber - 1) % levels.Count;
            if (index < 0) index += levels.Count;

            if (shuffleWithinChapter) index = ShuffledIndex(index);

            return levels[index];
        }

        /// <summary>Maps a sequential position onto its shuffled slot inside the same chapter.</summary>
        private int ShuffledIndex(int sequentialIndex)
        {
            int per = LevelsPerChapter;
            int chapter = sequentialIndex / per;
            int posInChapter = sequentialIndex % per;

            int chapterStart = chapter * per;
            // The final chapter can be short; never shuffle past the end of the list.
            int chapterSize = Mathf.Min(per, levels.Count - chapterStart);
            if (chapterSize <= 1) return sequentialIndex;

            var order = new int[chapterSize];
            for (int i = 0; i < chapterSize; i++) order[i] = i;

            // Seeded per chapter so each chapter gets its own order, and stable across sessions.
            var rng = new System.Random(GetSeed() ^ (chapter * 73856093));
            for (int i = chapterSize - 1; i > 0; i--)
            {
                int k = rng.Next(i + 1);
                (order[i], order[k]) = (order[k], order[i]);
            }

            return chapterStart + order[posInChapter];
        }

        /// <summary>
        /// One seed per player, created on first use, so two installs see different orders
        /// while a single install always sees the same one.
        /// </summary>
        private static int GetSeed()
        {
            if (!GameStorage.HasKey(PrefKeyShuffleSeed))
            {
                GameStorage.SetInt(PrefKeyShuffleSeed, Random.Range(1, int.MaxValue));
                GameStorage.Save();
            }
            return GameStorage.GetInt(PrefKeyShuffleSeed, 1);
        }

#if UNITY_EDITOR
        public void EditorSetLevels(List<LevelData> newLevels)
        {
            levels = newLevels;
        }
#endif
    }
}
