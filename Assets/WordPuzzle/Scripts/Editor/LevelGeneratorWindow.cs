#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WordPuzzle.Data;

namespace WordPuzzle.Editor
{
    /// <summary>
    /// Authoring tool for the level campaign. Generates validated crossword levels from a
    /// word list, writes them as individual assets into Data/SO/Levels, and registers
    /// them in the LevelDatabase.
    /// </summary>
    public class LevelGeneratorWindow : EditorWindow
    {
        private const string DatabasePath = "Assets/WordPuzzle/Data/SO/LevelDatabase.asset";
        private const string LevelsFolder = "Assets/WordPuzzle/Data/SO/Levels";

        private TextAsset _wordListAsset;
        private LevelDatabase _database;

        private int _levelCount = 1000;
        private int _minWheelLetters = 4;
        private int _maxWheelLetters = 7;
        private int _minWordsPerLevel = 3;
        private int _maxWordsPerLevel = 8;
        private bool _appendInsteadOfReplace;

        private string _manualWheel = "";
        private string _status = "";
        private Vector2 _scroll;

        [MenuItem("WordPuzzle/Level Generator")]
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

            using (new EditorGUI.DisabledScope(_wordListAsset == null))
            {
                if (GUILayout.Button($"Generate {_levelCount} Levels Into Data/SO/Levels", GUILayout.Height(32f))) Generate();
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
            EnsureLevelsFolder();

            List<LevelData> levels = _appendInsteadOfReplace ? new List<LevelData>(db.Levels) : new List<LevelData>();
            if (!_appendInsteadOfReplace) DeleteExistingLevelAssets();

            var wheelPool = words
                .Where(w => w.Length >= _minWheelLetters && w.Length <= _maxWheelLetters)
                .OrderBy(w => w.Length)
                .ToList();

            var used = new HashSet<string>(levels.Where(l => l != null).Select(l => l.wheelLetters));
            int made = 0;

            // Batching the writes keeps a 1000-asset run from re-importing after every file.
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string wheel in wheelPool)
                {
                    if (made >= _levelCount) break;
                    if (used.Contains(wheel)) continue;

                    LevelData level = BuildLevel(wheel, words, levels.Count + 1);
                    if (level == null) continue;

                    AssetDatabase.CreateAsset(level, $"{LevelsFolder}/{level.name}.asset");
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
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            db.EditorSetLevels(levels);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _status = made < _levelCount
                ? $"Generated {made} levels from {wheelPool.Count} candidate wheels - the word list is too small for {_levelCount}. " +
                  "A denser list of common words yields far more sub-words per wheel."
                : $"Generated {made} levels into {LevelsFolder} and registered them in the LevelDatabase.";
        }

        private void AddSingle(string wheel)
        {
            LevelDatabase db = GetOrCreateDatabase();
            EnsureLevelsFolder();

            List<string> words = LoadWords();
            var levels = new List<LevelData>(db.Levels);

            LevelData level = BuildLevel(wheel, words, levels.Count + 1);
            if (level == null)
            {
                _status = $"'{wheel}' does not yield at least {_minWordsPerLevel} placeable words under the current rules.";
                return;
            }

            AssetDatabase.CreateAsset(level, $"{LevelsFolder}/{level.name}.asset");
            levels.Add(level);
            db.EditorSetLevels(levels);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            _status = $"Added level {levels.Count} for '{wheel}' with {level.targetWords.Count} words.";
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

            var level = CreateInstance<LevelData>();
            level.name = $"Level_{levelNumber:D4}";
            level.levelNumber = levelNumber;
            level.chapterTitle = $"Chapter {((levelNumber - 1) / 20) + 1}";
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

        private static void EnsureLevelsFolder()
        {
            if (AssetDatabase.IsValidFolder(LevelsFolder)) return;
            AssetDatabase.CreateFolder("Assets/WordPuzzle/Data/SO", "Levels");
        }

        private static void DeleteExistingLevelAssets()
        {
            if (!AssetDatabase.IsValidFolder(LevelsFolder)) return;

            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { LevelsFolder });
            foreach (string guid in guids)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
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
