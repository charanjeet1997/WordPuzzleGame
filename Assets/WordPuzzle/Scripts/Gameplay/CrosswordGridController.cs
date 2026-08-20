using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Factory;
using WordPuzzle.Services;

namespace WordPuzzle.Gameplay
{
    public class CrosswordGridController : MonoBehaviour
    {
        [Header("Grid Layout Parameters")]
        [Tooltip("Preferred tile size. Levels with a large grid shrink below this to fit the screen.")]
        public float tileSize = 0.4615f;
        public float tileSpacing = 0.046f;

        [Header("Screen Fit")]
        [Tooltip("Camera used to measure the visible area. Falls back to Camera.main.")]
        public Camera viewCamera;

        [Tooltip("Viewport rect the grid may occupy in portrait: the band between the HUD bar " +
                 "and the letter wheel. x/y are the bottom-left corner, in 0-1 viewport coords.")]
        public Rect portraitArea = new Rect(0.04f, 0.52f, 0.92f, 0.42f);

        [Tooltip("Viewport rect in landscape. The wheel takes the left, so the grid sits on " +
                 "the right and gains most of the height back.")]
        public Rect landscapeArea = new Rect(0.45f, 0.08f, 0.52f, 0.84f);

        [Tooltip("Never shrink tiles below this, even if the grid then overflows.")]
        public float minTileSize = 0.16f;

        [Tooltip("Largest a tile may grow to. The authored size is a starting point, not a cap: " +
                 "on a wide screen a three-word grid was leaving most of its area empty.")]
        public float maxTileSize = 0.95f;

        [Tooltip("Letter size in tile-local units. Lower values leave more margin inside the tile.")]
        public float letterFontSize = 2.4f;

        [Tooltip("Optional. Leave empty to use the TextMeshPro default font.")]
        public TMP_FontAsset letterFont;

        private readonly Dictionary<Vector2Int, GridTile> _gridTiles = new Dictionary<Vector2Int, GridTile>();
        private readonly List<TargetWordEntry> _targetWords = new List<TargetWordEntry>();
        private readonly List<GridTile> _scratchTiles = new List<GridTile>();

        private LevelData _lastLevel;

        private void OnEnable() => LayoutService.LayoutChanged += OnLayoutChanged;
        private void OnDisable() => LayoutService.LayoutChanged -= OnLayoutChanged;

        /// <summary>
        /// Rebuilds at the new size. Cheap enough to do wholesale - a grid is at most a few
        /// dozen tiles - and rebuilding preserves reveal state because it is replayed from the
        /// level data and the model rather than held in the tiles.
        /// </summary>
        private void OnLayoutChanged(ScreenLayout layout)
        {
            if (_lastLevel != null) BuildGrid(_lastLevel);
        }

        public void BuildGrid(LevelData levelData)
        {
            _lastLevel = levelData;
            ClearGrid();
            if (levelData == null || levelData.targetWords == null) return;

            _targetWords.AddRange(levelData.targetWords);

            int maxRows = levelData.GetMaxGridRows();
            int maxCols = levelData.GetMaxGridCols();

            // Tile size is per level, not fixed: a 7x9 grid at the authored size runs off a
            // 19.5:9 screen and collides with the HUD, so large levels scale down to fit.
            float fittedTile = FitTileSize(maxRows, maxCols, out Vector3 areaCenter);
            float fittedSpacing = tileSpacing * (fittedTile / tileSize);

            float totalWidth = maxCols * fittedTile + (maxCols - 1) * fittedSpacing;
            float totalHeight = maxRows * fittedTile + (maxRows - 1) * fittedSpacing;

            // Centred on the measured band rather than on this transform, so the grid sits
            // between the HUD and the wheel whatever the level's dimensions are.
            Vector3 gridOrigin = areaCenter + new Vector3(
                -totalWidth * 0.5f + fittedTile * 0.5f,
                totalHeight * 0.5f - fittedTile * 0.5f,
                0f);

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
                        Vector3 worldPos = gridOrigin + new Vector3(
                            c * (fittedTile + fittedSpacing),
                            -r * (fittedTile + fittedSpacing),
                            0f);

                        GridTile tile = FactoryFuncMapping.CreateGridTile();
                        if (tile == null) continue;

                        tile.transform.SetParent(transform, false);
                        tile.transform.position = worldPos;
                        tile.Initialize(word[i], r, c);
                        tile.SetSize(fittedTile);
                        tile.SetFontSize(letterFontSize);
                        tile.SetFont(letterFont);
                        _gridTiles.Add(posKey, tile);
                    }
                }
            }
        }

        /// <summary>
        /// Largest tile size at which a rows x cols grid fits the allotted band of the screen,
        /// never larger than the authored <see cref="tileSize"/>. Also reports the world-space
        /// centre of that band so the caller can centre the grid in it.
        /// </summary>
        private float FitTileSize(int rows, int cols, out Vector3 areaCenter)
        {
            areaCenter = transform.position;
            if (rows <= 0 || cols <= 0) return tileSize;

            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return tileSize;

            // The band differs by orientation: stacked in portrait, side by side in landscape.
            Rect area = LayoutService.IsLandscape ? landscapeArea : portraitArea;

            float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(area.xMin, area.yMin, depth));
            Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(area.xMax, area.yMax, depth));

            float availableWidth = Mathf.Abs(topRight.x - bottomLeft.x);
            float availableHeight = Mathf.Abs(topRight.y - bottomLeft.y);

            areaCenter = new Vector3(
                (bottomLeft.x + topRight.x) * 0.5f,
                (bottomLeft.y + topRight.y) * 0.5f,
                transform.position.z);

            // Spacing scales with the tile, so solve for tile size directly:
            //   width = cols * t + (cols - 1) * t * spacingRatio
            float spacingRatio = tileSize > 0.0001f ? tileSpacing / tileSize : 0f;
            float widthFit = availableWidth / (cols + (cols - 1) * spacingRatio);
            float heightFit = availableHeight / (rows + (rows - 1) * spacingRatio);

            // Grows as well as shrinks: capped by maxTileSize rather than by the authored
            // size, so a small grid fills the space it is given instead of floating in it.
            float fitted = Mathf.Min(widthFit, heightFit, maxTileSize);
            return Mathf.Max(fitted, minTileSize);
        }

        public bool TryRevealWord(string submittedWord)
        {
            return TryRevealWord(submittedWord, out _);
        }

        /// <summary>
        /// Reveals the word and reports the world-space center of the tiles it occupies, so
        /// feedback (particles, shake) can play on the word instead of at the manager's origin.
        /// </summary>
        public bool TryRevealWord(string submittedWord, out Vector3 wordCenter)
        {
            wordCenter = transform.position;
            if (string.IsNullOrEmpty(submittedWord)) return false;
            string word = submittedWord.ToUpperInvariant();

            bool wordMatched = false;
            Vector3 sum = Vector3.zero;
            int counted = 0;

            foreach (var entry in _targetWords)
            {
                if (string.Equals(entry.word, word, System.StringComparison.OrdinalIgnoreCase))
                {
                    RevealWordEntry(entry, ref sum, ref counted);
                    wordMatched = true;
                }
            }

            if (counted > 0) wordCenter = sum / counted;
            return wordMatched;
        }

        /// <summary>Whether a hint has anything left to reveal - checked before charging for one.</summary>
        public bool HasHiddenTiles()
        {
            foreach (var tile in _gridTiles.Values)
            {
                if (!tile.IsRevealed) return true;
            }
            return false;
        }

        public bool RevealRandomHiddenTile()
        {
            return RevealRandomHiddenTile(out _);
        }

        /// <summary>Reveals a random hidden tile and reports its world position for feedback.</summary>
        public bool RevealRandomHiddenTile(out Vector3 tilePosition)
        {
            tilePosition = transform.position;

            // Reused across calls: a hint can be spammed, and this allocated a List every press.
            _scratchTiles.Clear();
            foreach (var tile in _gridTiles.Values)
            {
                if (!tile.IsRevealed) _scratchTiles.Add(tile);
            }

            if (_scratchTiles.Count > 0)
            {
                int idx = Random.Range(0, _scratchTiles.Count);
                GridTile picked = _scratchTiles[idx];
                picked.Reveal(true);
                tilePosition = picked.transform.position;
                _scratchTiles.Clear();
                return true;
            }
            return false;
        }

        private void RevealWordEntry(TargetWordEntry entry, ref Vector3 positionSum, ref int counted)
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
                    positionSum += tile.transform.position;
                    counted++;
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
