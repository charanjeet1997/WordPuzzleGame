using System.Collections.Generic;
using UnityEngine;

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

        public int Count => levels.Count;

        public List<LevelData> Levels => levels;

        /// <summary>
        /// Levels are 1-based and wrap, so a level number beyond the authored set
        /// still resolves instead of returning null.
        /// </summary>
        public LevelData GetLevel(int levelNumber)
        {
            if (levels == null || levels.Count == 0) return null;

            int index = (levelNumber - 1) % levels.Count;
            if (index < 0) index += levels.Count;
            return levels[index];
        }

#if UNITY_EDITOR
        public void EditorSetLevels(List<LevelData> newLevels)
        {
            levels = newLevels;
        }
#endif
    }
}
