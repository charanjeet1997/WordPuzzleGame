#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WordPuzzle.Data;

namespace WordPuzzle.Editor
{
    /// <summary>Order the campaign walks through wheel sizes.</summary>
    public enum WheelOrder
    {
        ShortToLong,   // 4-letter wheels open the campaign, 7-letter ones close it
        LongToShort,   // more sub-words available early, but a denser opening screen
        Random         // shuffled, so difficulty does not climb monotonically
    }

    /// <summary>
    /// Authoring tool for the level campaign. Generates validated crossword levels from a
    /// word list and stores them inline in the LevelDatabase asset.
    /// </summary>
    public class LevelGeneratorWindow : EditorWindow
    {
        private const string DatabasePath = "Assets/WordPuzzle/Data/SO/LevelDatabase.asset";

        private TextAsset _wordListAsset;
        private LevelDatabase _database;

        private int _levelCount = 1000;
        private int _minWheelLetters = 4;
        private int _maxWheelLetters = 7;
        private int _minWordsPerLevel = 3;
        private int _maxWordsPerLevel = 8;
        private bool _appendInsteadOfReplace;
        private WheelOrder _wheelOrder = WheelOrder.ShortToLong;
        private int _mixedSeed = 12345;
        private int _levelsPerChapter = 20;
        private bool _useThemedChapterNames = true;

        private string _manualWheel = "";
        private string _status = "";
        private Vector2 _scroll;

        [MenuItem("Aurora Words/Level Generator")]
        public static void Open()
        {
            GetWindow<LevelGeneratorWindow>("Level Generator").minSize = new Vector2(420f, 460f);
        }

        private void OnEnable()
        {
            if (_database == null) _database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use word_list_targets.txt here, not word_list.txt. " +
                "word_list.txt is the wider ACCEPTS list used at runtime for bonus words - it " +
                "contains brands and acronyms (RCA, ACER, DVD). Those are fine to type for coins " +
                "but must never become a required grid answer.", MessageType.Warning);
            _wordListAsset = (TextAsset)EditorGUILayout.ObjectField("Word List", _wordListAsset, typeof(TextAsset), false);
            _database = (LevelDatabase)EditorGUILayout.ObjectField("Level Database", _database, typeof(LevelDatabase), false);

            if (_wordListAsset != null)
            {
                EditorGUILayout.HelpBox($"{LoadWords().Count} usable words (3+ letters, A-Z).", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Assign a word list TextAsset (one word per line). " +
                    "Use 'Export WordDictionary' below to bootstrap one from the existing hardcoded list.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            _levelCount = Mathf.Max(1, EditorGUILayout.IntField("Levels To Generate", _levelCount));
            _minWheelLetters = Mathf.Clamp(EditorGUILayout.IntField("Min Wheel Letters", _minWheelLetters), 3, 9);
            _maxWheelLetters = Mathf.Clamp(EditorGUILayout.IntField("Max Wheel Letters", _maxWheelLetters), _minWheelLetters, 9);
            _minWordsPerLevel = Mathf.Max(2, EditorGUILayout.IntField("Min Words Per Level", _minWordsPerLevel));
            _maxWordsPerLevel = Mathf.Max(_minWordsPerLevel, EditorGUILayout.IntField("Max Words Per Level", _maxWordsPerLevel));
            _appendInsteadOfReplace = EditorGUILayout.Toggle("Append To Existing", _appendInsteadOfReplace);

            _wheelOrder = (WheelOrder)EditorGUILayout.EnumPopup("Wheel Order", _wheelOrder);

            // Shown for every order: even the sorted ones shuffle within a length band, so the
            // seed decides which word opens the campaign.
            using (new EditorGUILayout.HorizontalScope())
            {
                _mixedSeed = EditorGUILayout.IntField("Shuffle Seed", _mixedSeed);
                if (GUILayout.Button("New", GUILayout.Width(48f)))
                {
                    _mixedSeed = Random.Range(1, int.MaxValue);
                }
            }

            EditorGUILayout.HelpBox(DescribeOrder(_wheelOrder) +
                "\n\nWheels of the same length are shuffled by the seed, so a chapter mixes different " +
                "letter sets instead of running alphabetically. Change the seed for a different campaign; " +
                "keep it to rebuild the same one.", MessageType.None);

            _levelsPerChapter = Mathf.Max(1, EditorGUILayout.IntField("Levels Per Chapter", _levelsPerChapter));
            _useThemedChapterNames = EditorGUILayout.Toggle("Themed Chapter Names", _useThemedChapterNames);
            EditorGUILayout.LabelField(" ", $"{_levelCount} levels = " +
                $"{Mathf.CeilToInt(_levelCount / (float)_levelsPerChapter)} chapters", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(_wordListAsset == null))
            {
                if (GUILayout.Button($"Generate {_levelCount} Levels Into The Level Database", GUILayout.Height(32f))) Generate();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add A Single Level", EditorStyles.boldLabel);
            _manualWheel = EditorGUILayout.TextField("Wheel Letters", _manualWheel).ToUpperInvariant();

            using (new EditorGUI.DisabledScope(_wordListAsset == null || _manualWheel.Length < 3))
            {
                if (GUILayout.Button("Append Level From These Letters")) AddSingle(_manualWheel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);
            if (GUILayout.Button("Export WordDictionary To TextAsset")) ExportDictionary();

            using (new EditorGUI.DisabledScope(_database == null))
            {
                if (GUILayout.Button("Validate All Levels In Database")) ValidateDatabase();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private List<string> LoadWords()
        {
            if (_wordListAsset == null) return new List<string>();

            return _wordListAsset.text
                .Split(new[] { '\n', '\r', ',', ';', '\t' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim().ToUpperInvariant())
                .Where(w => w.Length >= 3 && w.All(char.IsLetter))
                .Distinct()
                .ToList();
        }

        private void Generate()
        {
            List<string> words = LoadWords();
            if (words.Count == 0)
            {
                _status = "Word list produced no usable words.";
                return;
            }

            LevelDatabase db = GetOrCreateDatabase();

            List<LevelData> levels = _appendInsteadOfReplace ? new List<LevelData>(db.Levels) : new List<LevelData>();

            var candidates = words.Where(w => w.Length >= _minWheelLetters && w.Length <= _maxWheelLetters);
            List<string> wheelPool = OrderWheels(candidates);

            var used = new HashSet<string>(levels.Where(l => l != null).Select(l => l.wheelLetters));
            int made = 0;

            try
            {
                foreach (string wheel in wheelPool)
                {
                    if (made >= _levelCount) break;
                    if (used.Contains(wheel)) continue;

                    LevelData level = BuildLevel(wheel, words, levels.Count + 1);
                    if (level == null) continue;

                    levels.Add(level);
                    used.Add(wheel);
                    made++;

                    if (made % 25 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Level Generator",
                            $"Built {made}/{_levelCount}", made / (float)_levelCount);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            db.EditorSetLevels(levels);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            _status = made < _levelCount
                ? $"Generated {made} levels from {wheelPool.Count} candidate wheels - the word list is too small for {_levelCount}. " +
                  "A denser list of common words yields far more sub-words per wheel."
                : $"Generated {made} levels into the LevelDatabase asset.";
        }

        private void AddSingle(string wheel)
        {
            LevelDatabase db = GetOrCreateDatabase();

            List<string> words = LoadWords();
            var levels = new List<LevelData>(db.Levels);

            LevelData level = BuildLevel(wheel, words, levels.Count + 1);
            if (level == null)
            {
                _status = $"'{wheel}' does not yield at least {_minWordsPerLevel} placeable words under the current rules.";
                return;
            }

            levels.Add(level);
            db.EditorSetLevels(levels);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            _status = $"Added level {levels.Count} for '{wheel}' with {level.targetWords.Count} words.";
        }

        /// <summary>
        /// Orders the wheel pool, which is what sets the campaign's difficulty curve: levels
        /// are handed out in this order, so position in this list is the level number.
        /// </summary>
        private List<string> OrderWheels(IEnumerable<string> candidates)
        {
            // Seeded so a given seed always rebuilds the same campaign - regenerating must not
            // silently reshuffle levels players have already progressed past.
            var rng = new System.Random(_mixedSeed);

            switch (_wheelOrder)
            {
                case WheelOrder.LongToShort:
                    // Shuffled within each length band. Ordering ties alphabetically instead
                    // made a whole chapter run ABBE, ABED, ABET - same length, nearly the same
                    // letters, and level 1 identical on every regeneration.
                    return candidates
                        .OrderByDescending(w => w.Length)
                        .ThenBy(_ => rng.Next())
                        .ToList();

                case WheelOrder.Random:
                    return candidates.OrderBy(_ => rng.Next()).ToList();

                default:
                    return candidates
                        .OrderBy(w => w.Length)
                        .ThenBy(_ => rng.Next())
                        .ToList();
            }
        }

        /// <summary>
        /// Chapter names, cycled across the campaign. Drawn from the same night-forest and
        /// mountain palette the background art uses, so the caption matches what is on screen.
        /// </summary>
        private static readonly string[] ChapterNames =
        {
            "Green Valley", "Starlight Peak", "Whispering Woods", "Amber Hollow", "Frost Ridge",
            "Quiet Meadow", "Ember Trail", "Silver Lake", "Mossy Glade", "Dawn Cliffs",
            "Hidden Spring", "Wildflower Path", "Cedar Pass", "Twilight Fen", "Autumn Reach",
            "Crystal Falls", "Northern Pines", "Sunset Dunes", "Willow Bend", "Storm Hollow",
            "Lantern Grove", "Misty Fjord", "Copper Canyon", "Aurora Fields", "Driftwood Bay"
        };

        /// <summary>
        /// "Chapter 3 - Whispering Woods" for the level, or a bare number when themed names
        /// are off. Names repeat once the list runs out, which a 1000-level campaign will do.
        /// </summary>
        private string ChapterTitleFor(int levelNumber)
        {
            int chapter = ((levelNumber - 1) / _levelsPerChapter) + 1;
            if (!_useThemedChapterNames) return $"Chapter {chapter}";

            string name = ChapterNames[(chapter - 1) % ChapterNames.Length];
            return $"Chapter {chapter} - {name}";
        }

        private static string DescribeOrder(WheelOrder order)
        {
            switch (order)
            {
                case WheelOrder.LongToShort:
                    return "Long to short: starts at 7-letter wheels and works down. More words are " +
                           "findable early, but the opening levels are dense and the grid is large.";
                case WheelOrder.Random:
                    return "Random: wheel sizes are shuffled, so difficulty does not climb steadily.";
                default:
                    return "Short to long: starts at 4-letter wheels and grows to 7. Gentlest opening, " +
                           "and difficulty rises with the level number.";
            }
        }

        private LevelData BuildLevel(string wheel, List<string> words, int levelNumber)
        {
            var candidates = words
                .Where(w => w.Length >= 3 && w.Length <= wheel.Length && LevelLayoutBuilder.IsSpellable(w, wheel))
                .ToList();

            if (!candidates.Contains(wheel)) candidates.Add(wheel);
            candidates = LevelLayoutBuilder.ApplySubstringRule(candidates);

            if (candidates.Count < _minWordsPerLevel) return null;

            List<PlacedWord> placed = LevelLayoutBuilder.Build(wheel, candidates, _maxWordsPerLevel);
            if (placed == null || placed.Count < _minWordsPerLevel) return null;

            var level = new LevelData();
            level.levelName = $"Level_{levelNumber:D4}";
            level.levelNumber = levelNumber;
            level.chapterTitle = ChapterTitleFor(levelNumber);
            level.wheelLetters = wheel;
            level.targetWords = placed.Select(p => new TargetWordEntry
            {
                word = p.Word,
                startRow = p.Row,
                startCol = p.Col,
                orientation = p.Orientation
            }).ToList();

            return level;
        }

        private LevelDatabase GetOrCreateDatabase()
        {
            if (_database != null) return _database;

            _database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (_database != null) return _database;

            _database = CreateInstance<LevelDatabase>();
            AssetDatabase.CreateAsset(_database, DatabasePath);
            AssetDatabase.SaveAssets();
            return _database;
        }

        private void ValidateDatabase()
        {
            int bad = 0;
            var report = new System.Text.StringBuilder();

            foreach (LevelData level in _database.Levels)
            {
                if (level == null) continue;

                var placed = level.targetWords.Select(t => new PlacedWord
                {
                    Word = t.word,
                    Row = t.startRow,
                    Col = t.startCol,
                    Orientation = t.orientation
                }).ToList();

                if (!LevelLayoutBuilder.Validate(placed, level.wheelLetters, out string error))
                {
                    bad++;
                    if (report.Length < 1500) report.AppendLine($"Level {level.levelNumber}: {error}");
                }
            }

            _status = bad == 0
                ? $"All {_database.Count} levels valid."
                : $"{bad} invalid level(s):\n{report}";
        }

        private void ExportDictionary()
        {
            string path = "Assets/WordPuzzle/Resources/word_list.txt";
            var words = new SortedSet<string>();

            foreach (string w in Data.WordDictionary.AllWords) words.Add(w.ToUpperInvariant());

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllLines(path, words);
            AssetDatabase.Refresh();

            _wordListAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            _status = $"Exported {words.Count} words to {path}. Replace this file with a larger list to generate more levels.";
        }
    }
}
#endif
