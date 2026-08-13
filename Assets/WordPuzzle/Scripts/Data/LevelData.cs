using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordPuzzle.Data
{
    [Serializable]
    public class TargetWordEntry
    {
        public string word;
        public int startRow;
        public int startCol;
        public WordOrientation orientation;
    }

    /// <summary>
    /// A single authored level. Plain serializable data, not a ScriptableObject: levels are
    /// stored inline in the <see cref="LevelDatabase"/> list, so a 1000-level campaign is one
    /// asset instead of 1000 files to create, track and load.
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public string levelName = "Level_0001";
        public int levelNumber = 1;
        public string chapterTitle = "Chapter 1 - Green Valley";
        public string wheelLetters = "CATS";
        public List<TargetWordEntry> targetWords = new List<TargetWordEntry>();
        public List<string> bonusWords = new List<string>();

        public int GetMaxGridRows()
        {
            int maxRow = 0;
            foreach (var entry in targetWords)
            {
                int endRow = entry.startRow + (entry.orientation == WordOrientation.Vertical ? entry.word.Length - 1 : 0);
                if (endRow > maxRow) maxRow = endRow;
            }
            return maxRow + 1;
        }

        public int GetMaxGridCols()
        {
            int maxCol = 0;
            foreach (var entry in targetWords)
            {
                int endCol = entry.startCol + (entry.orientation == WordOrientation.Horizontal ? entry.word.Length - 1 : 0);
                if (endCol > maxCol) maxCol = endCol;
            }
            return maxCol + 1;
        }
    }
}
