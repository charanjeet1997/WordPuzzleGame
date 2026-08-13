#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using WordPuzzle.Data;

namespace WordPuzzle.Editor
{
    public class PlacedWord
    {
        public string Word;
        public int Row;
        public int Col;
        public WordOrientation Orientation;
    }

    /// <summary>
    /// Builds and validates crossword layouts.
    /// <para>
    /// Rules enforced:
    /// 1. Intersecting cells must agree on their letter.
    /// 2. Every maximal run of 2+ letters, in both axes, must be one of the target words.
    ///    This is what stops a word starting directly under another and merging into a
    ///    nonsense run such as "CCAT".
    /// 3. No target may be a substring of another target (no CAT alongside CATS).
    /// 4. Every target must be spellable from the wheel letters.
    /// </para>
    /// </summary>
    public static class LevelLayoutBuilder
    {
        public static bool IsSpellable(string word, string wheelLetters)
        {
            var pool = new Dictionary<char, int>();
            foreach (char c in wheelLetters)
            {
                pool.TryGetValue(c, out int n);
                pool[c] = n + 1;
            }

            foreach (char c in word)
            {
                if (!pool.TryGetValue(c, out int n) || n == 0) return false;
                pool[c] = n - 1;
            }
            return true;
        }

        /// <summary>
        /// Removes any word that appears inside another kept word, so no level ever
        /// contains both a word and its extension.
        /// </summary>
        public static List<string> ApplySubstringRule(List<string> words)
        {
            return words.Where(w => !words.Any(other => other != w && other.Contains(w))).ToList();
        }

        public static List<PlacedWord> Build(string wheelLetters, List<string> candidates, int maxWords)
        {
            var ordered = candidates.OrderByDescending(w => w.Length).ThenBy(w => w).ToList();
            if (ordered.Count == 0) return null;

            var placed = new List<PlacedWord>
            {
                new PlacedWord { Word = ordered[0], Row = 0, Col = 0, Orientation = WordOrientation.Horizontal }
            };

            foreach (string word in ordered.Skip(1))
            {
                if (placed.Count >= maxWords) break;
                TryPlace(word, placed);
            }

            if (placed.Count < 2) return null;

            Normalize(placed);
            return Validate(placed, wheelLetters, out _) ? placed : null;
        }

        private static void TryPlace(string word, List<PlacedWord> placed)
        {
            Dictionary<(int, int), char> grid = BuildGrid(placed);

            foreach (var cell in grid)
            {
                for (int i = 0; i < word.Length; i++)
                {
                    if (word[i] != cell.Value) continue;

                    // Cross the existing letter perpendicular to whatever owns that cell.
                    foreach (WordOrientation orientation in new[] { WordOrientation.Vertical, WordOrientation.Horizontal })
                    {
                        int row = orientation == WordOrientation.Vertical ? cell.Key.Item1 - i : cell.Key.Item1;
                        int col = orientation == WordOrientation.Horizontal ? cell.Key.Item2 - i : cell.Key.Item2;

                        var candidate = new PlacedWord { Word = word, Row = row, Col = col, Orientation = orientation };
                        placed.Add(candidate);

                        // Accept only if the whole board still reads legally.
                        if (Validate(placed, null, out _)) return;

                        placed.Remove(candidate);
                    }
                }
            }
        }

        private static Dictionary<(int, int), char> BuildGrid(List<PlacedWord> placed)
        {
            var grid = new Dictionary<(int, int), char>();
            foreach (var p in placed)
            {
                for (int i = 0; i < p.Word.Length; i++)
                {
                    int r = p.Row + (p.Orientation == WordOrientation.Vertical ? i : 0);
                    int c = p.Col + (p.Orientation == WordOrientation.Horizontal ? i : 0);
                    grid[(r, c)] = p.Word[i];
                }
            }
            return grid;
        }

        /// <summary>Shifts the layout so the top-left filled cell sits at (0,0).</summary>
        private static void Normalize(List<PlacedWord> placed)
        {
            int minRow = placed.Min(p => p.Row);
            int minCol = placed.Min(p => p.Col);

            foreach (var p in placed)
            {
                p.Row -= minRow;
                p.Col -= minCol;
            }
        }

        public static bool Validate(List<PlacedWord> placed, string wheelLetters, out string error)
        {
            error = null;
            var grid = new Dictionary<(int, int), char>();

            foreach (var p in placed)
            {
                for (int i = 0; i < p.Word.Length; i++)
                {
                    int r = p.Row + (p.Orientation == WordOrientation.Vertical ? i : 0);
                    int c = p.Col + (p.Orientation == WordOrientation.Horizontal ? i : 0);

                    if (grid.TryGetValue((r, c), out char existing) && existing != p.Word[i])
                    {
                        error = $"Letter conflict at ({r},{c}): '{existing}' vs '{p.Word[i]}'";
                        return false;
                    }
                    grid[(r, c)] = p.Word[i];
                }
            }

            var words = new HashSet<string>(placed.Select(p => p.Word));

            int maxRow = grid.Keys.Max(k => k.Item1);
            int maxCol = grid.Keys.Max(k => k.Item2);
            int minRow = grid.Keys.Min(k => k.Item1);
            int minCol = grid.Keys.Min(k => k.Item2);

            if (!RunsAreWords(grid, words, minRow, maxRow, minCol, maxCol, true, out error)) return false;
            if (!RunsAreWords(grid, words, minRow, maxRow, minCol, maxCol, false, out error)) return false;

            foreach (string w in words)
            {
                if (words.Any(o => o != w && o.Contains(w)))
                {
                    error = $"'{w}' is a substring of another target word";
                    return false;
                }

                if (wheelLetters != null && !IsSpellable(w, wheelLetters))
                {
                    error = $"'{w}' is not spellable from wheel '{wheelLetters}'";
                    return false;
                }
            }

            return true;
        }

        private static bool RunsAreWords(Dictionary<(int, int), char> grid, HashSet<string> words,
            int minRow, int maxRow, int minCol, int maxCol, bool horizontal, out string error)
        {
            error = null;
            int outerFrom = horizontal ? minRow : minCol;
            int outerTo = horizontal ? maxRow : maxCol;
            int innerFrom = horizontal ? minCol : minRow;
            int innerTo = horizontal ? maxCol : maxRow;

            for (int a = outerFrom; a <= outerTo; a++)
            {
                string run = "";
                for (int b = innerFrom; b <= innerTo + 1; b++)
                {
                    var key = horizontal ? (a, b) : (b, a);
                    if (b <= innerTo && grid.TryGetValue(key, out char ch))
                    {
                        run += ch;
                        continue;
                    }

                    if (run.Length >= 2 && !words.Contains(run))
                    {
                        error = $"{(horizontal ? "Row" : "Column")} {a} reads \"{run}\", which is not a target word";
                        return false;
                    }
                    run = "";
                }
            }
            return true;
        }
    }
}
#endif
