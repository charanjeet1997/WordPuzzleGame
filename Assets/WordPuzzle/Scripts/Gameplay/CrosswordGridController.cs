using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Factory;

namespace WordPuzzle.Gameplay
{
    public class CrosswordGridController : MonoBehaviour
    {
        [Header("Grid Layout Parameters")]
        public float tileSize = 0.4615f;
        public float tileSpacing = 0.046f;

        [Tooltip("Letter size in tile-local units. Lower values leave more margin inside the tile.")]
        public float letterFontSize = 2.4f;

        [Tooltip("Optional. Leave empty to use the TextMeshPro default font.")]
        public TMP_FontAsset letterFont;

        private readonly Dictionary<Vector2Int, GridTile> _gridTiles = new Dictionary<Vector2Int, GridTile>();
        private readonly List<TargetWordEntry> _targetWords = new List<TargetWordEntry>();

        public void BuildGrid(LevelData levelData)
        {
            ClearGrid();
            if (levelData == null || levelData.targetWords == null) return;

            _targetWords.AddRange(levelData.targetWords);

            int maxRows = levelData.GetMaxGridRows();
            int maxCols = levelData.GetMaxGridCols();

            float totalWidth = maxCols * tileSize + (maxCols - 1) * tileSpacing;
            float totalHeight = maxRows * tileSize + (maxRows - 1) * tileSpacing;

            Vector3 startPos = new Vector3(-totalWidth * 0.5f + tileSize * 0.5f, totalHeight * 0.5f - tileSize * 0.5f, 0f);

            foreach (var entry in levelData.targetWords)
            {
                string word = entry.word.ToUpperInvariant();
                int row = entry.startRow;
                int col = entry.startCol;

                for (int i = 0; i < word.Length; i++)
                {
                    int r = row + (entry.orientation == WordOrientation.Vertical ? i : 0);
                    int c = col + (entry.orientation == WordOrientation.Horizontal ? i : 0);

                    Vector2Int posKey = new Vector2Int(r, c);
                    if (!_gridTiles.ContainsKey(posKey))
                    {
                        Vector3 worldPos = transform.position + startPos + new Vector3(c * (tileSize + tileSpacing), -r * (tileSize + tileSpacing), 0f);

                        GridTile tile = FactoryFuncMapping.CreateGridTile();
                        if (tile == null) continue;

                        tile.transform.SetParent(transform, false);
                        tile.transform.position = worldPos;
                        tile.Initialize(word[i], r, c);
                        tile.SetSize(tileSize);
                        tile.SetFontSize(letterFontSize);
                        tile.SetFont(letterFont);
                        _gridTiles.Add(posKey, tile);
                    }
                }
            }
        }

        public bool TryRevealWord(string submittedWord)
        {
            if (string.IsNullOrEmpty(submittedWord)) return false;
            string word = submittedWord.ToUpperInvariant();

            bool wordMatched = false;
            foreach (var entry in _targetWords)
            {
                if (string.Equals(entry.word, word, System.StringComparison.OrdinalIgnoreCase))
                {
                    RevealWordEntry(entry);
                    wordMatched = true;
                }
            }
            return wordMatched;
        }

        public bool RevealRandomHiddenTile()
        {
            List<GridTile> unrevealedTiles = new List<GridTile>();
            foreach (var tile in _gridTiles.Values)
            {
                if (!tile.IsRevealed) unrevealedTiles.Add(tile);
            }

            if (unrevealedTiles.Count > 0)
            {
                int idx = Random.Range(0, unrevealedTiles.Count);
                unrevealedTiles[idx].Reveal(true);
                return true;
            }
            return false;
        }

        private void RevealWordEntry(TargetWordEntry entry)
        {
            int row = entry.startRow;
            int col = entry.startCol;
            for (int i = 0; i < entry.word.Length; i++)
            {
                int r = row + (entry.orientation == WordOrientation.Vertical ? i : 0);
                int c = col + (entry.orientation == WordOrientation.Horizontal ? i : 0);

                Vector2Int posKey = new Vector2Int(r, c);
                if (_gridTiles.TryGetValue(posKey, out GridTile tile))
                {
                    tile.Reveal(true);
                }
            }
        }

        private void ClearGrid()
        {
            // Tiles are pooled - return them to the factory instead of destroying them.
            foreach (var tile in _gridTiles.Values)
            {
                if (tile != null) FactoryFuncMapping.RecycleGridTile(tile);
            }
            _gridTiles.Clear();
            _targetWords.Clear();
        }
    }
}
