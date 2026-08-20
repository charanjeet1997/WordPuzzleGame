using System;
using System.Collections.Generic;
using UnityEngine;
using ServiceLocatorFramework;
using WordPuzzle.Models;

namespace WordPuzzle.Services
{
    public interface IProgressionService
    {
        int CurrentLevelIndex { get; set; }
        int HighestUnlockedLevel { get; set; }
        int Coins { get; set; }
        int TotalScore { get; set; }
        int TotalWordsFound { get; set; }
        int TotalBonusWordsFound { get; set; }

        int GetStarsForLevel(int levelIndex);
        void SetStarsForLevel(int levelIndex, int stars);

        bool HasSavedLevelState(int levelIndex);
        bool TryGetSavedLevelState(int levelIndex, string fingerprint, out List<string> solvedWords, out List<string> bonusWords, out int hintsUsed);
        void SaveLevelState(int levelIndex, string fingerprint, IEnumerable<string> solvedWords, IEnumerable<string> bonusWords, int hintsUsed);
        void ClearLevelState(int levelIndex);

        void SaveAll();
        void ResetAllProgress();

        /// <summary>Best clear time in seconds for a level in the current mode, or 0 when unset.</summary>
        float GetBestTime(int levelIndex);

        /// <summary>Records a clear time, keeping it only when it beats the stored one.</summary>
        void SubmitTime(int levelIndex, float seconds);
    }

    public class ProgressionService : MonoBehaviour, IProgressionService
    {
        // Campaign position is per mode: clearing level 40 in Classic must not move Time
        // Trial. Keys that describe where you are get the mode suffix; the wallet and the
        // lifetime totals stay global, because coins are one shared economy.
        private static string PrefKeyCurrentLevel => GameModeContext.Key("WordPuzzle_CurrentLevel");
        private static string PrefKeyHighestLevel => GameModeContext.Key("WordPuzzle_HighestLevel");
        private static string PrefKeyStarsPrefix => GameModeContext.Key("WordPuzzle_Stars") + "_Lvl_";
        private static string PrefKeyLevelStatePrefix => GameModeContext.Key("WordPuzzle_State") + "_Lvl_";
        private static string PrefKeyBestTimePrefix => GameModeContext.Key("WordPuzzle_BestTime") + "_Lvl_";

        private const string PrefKeyCoins = "WordPuzzle_Coins";
        private const string PrefKeyTotalScore = "WordPuzzle_TotalScore";
        private const string PrefKeyTotalWords = "WordPuzzle_TotalWords";
        private const string PrefKeyTotalBonusWords = "WordPuzzle_TotalBonusWords";

        public const int DefaultStartingCoins = 100;

        [Serializable]
        private class LevelResumeData
        {
            public int levelIndex;

            /// <summary>The level's wheel letters. Regenerating the campaign replaces the
            /// puzzle at a given index, and resuming into a different puzzle would restore
            /// words the new grid never asked for.</summary>
            public string fingerprint;
            public List<string> solvedWords = new List<string>();
            public List<string> bonusWords = new List<string>();
            public int hintsUsed;
        }

        private int _currentLevelIndex = 1;
        private int _highestUnlockedLevel = 1;
        private int _coins = DefaultStartingCoins;
        private int _totalScore = 0;
        private int _totalWordsFound = 0;
        private int _totalBonusWordsFound = 0;

        public int CurrentLevelIndex
        {
            get => _currentLevelIndex;
            set
            {
                _currentLevelIndex = Mathf.Max(1, value);
                if (_currentLevelIndex > _highestUnlockedLevel)
                {
                    _highestUnlockedLevel = _currentLevelIndex;
                    GameStorage.SetInt(PrefKeyHighestLevel, _highestUnlockedLevel);
                }
                GameStorage.SetInt(PrefKeyCurrentLevel, _currentLevelIndex);
            }
        }

        public int HighestUnlockedLevel
        {
            get => _highestUnlockedLevel;
            set
            {
                _highestUnlockedLevel = Mathf.Max(1, value);
                GameStorage.SetInt(PrefKeyHighestLevel, _highestUnlockedLevel);
            }
        }

        public int Coins
        {
            get => _coins;
            set
            {
                _coins = Mathf.Max(0, value);
                GameStorage.SetInt(PrefKeyCoins, _coins);
            }
        }

        public int TotalScore
        {
            get => _totalScore;
            set
            {
                _totalScore = Mathf.Max(0, value);
                GameStorage.SetInt(PrefKeyTotalScore, _totalScore);
            }
        }

        public int TotalWordsFound
        {
            get => _totalWordsFound;
            set
            {
                _totalWordsFound = Mathf.Max(0, value);
                GameStorage.SetInt(PrefKeyTotalWords, _totalWordsFound);
            }
        }

        public int TotalBonusWordsFound
        {
            get => _totalBonusWordsFound;
            set
            {
                _totalBonusWordsFound = Mathf.Max(0, value);
                GameStorage.SetInt(PrefKeyTotalBonusWords, _totalBonusWordsFound);
            }
        }

        private void Awake()
        {
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<IProgressionService>())
            {
                ServiceLocator.Current.Register<IProgressionService>(this);
            }
            LoadAll();
        }

        private void OnDestroy()
        {
            SaveAll();
            if (ServiceLocator.Current != null && ServiceLocator.Current.Has<IProgressionService>())
            {
                ServiceLocator.Current.Unregister<IProgressionService>();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveAll();
            }
        }

        private void OnApplicationQuit()
        {
            SaveAll();
        }

        public void LoadAll()
        {
            _currentLevelIndex = Mathf.Max(1, GameStorage.GetInt(PrefKeyCurrentLevel, 1));
            _highestUnlockedLevel = Mathf.Max(_currentLevelIndex, GameStorage.GetInt(PrefKeyHighestLevel, 1));
            _coins = GameStorage.GetInt(PrefKeyCoins, DefaultStartingCoins);
            _totalScore = GameStorage.GetInt(PrefKeyTotalScore, 0);
            _totalWordsFound = GameStorage.GetInt(PrefKeyTotalWords, 0);
            _totalBonusWordsFound = GameStorage.GetInt(PrefKeyTotalBonusWords, 0);
        }

        public void SaveAll()
        {
            GameStorage.SetInt(PrefKeyCurrentLevel, _currentLevelIndex);
            GameStorage.SetInt(PrefKeyHighestLevel, _highestUnlockedLevel);
            GameStorage.SetInt(PrefKeyCoins, _coins);
            GameStorage.SetInt(PrefKeyTotalScore, _totalScore);
            GameStorage.SetInt(PrefKeyTotalWords, _totalWordsFound);
            GameStorage.SetInt(PrefKeyTotalBonusWords, _totalBonusWordsFound);
            GameStorage.Save();
        }

        public int GetStarsForLevel(int levelIndex)
        {
            return Mathf.Clamp(GameStorage.GetInt(PrefKeyStarsPrefix + levelIndex, 0), 0, 3);
        }

        public void SetStarsForLevel(int levelIndex, int stars)
        {
            int currentStars = GetStarsForLevel(levelIndex);
            if (stars > currentStars)
            {
                GameStorage.SetInt(PrefKeyStarsPrefix + levelIndex, Mathf.Clamp(stars, 1, 3));
                GameStorage.Save();
            }
        }

        public bool HasSavedLevelState(int levelIndex)
        {
            return GameStorage.HasKey(PrefKeyLevelStatePrefix + levelIndex);
        }

        public bool TryGetSavedLevelState(int levelIndex, string fingerprint, out List<string> solvedWords, out List<string> bonusWords, out int hintsUsed)
        {
            solvedWords = new List<string>();
            bonusWords = new List<string>();
            hintsUsed = 0;

            string key = PrefKeyLevelStatePrefix + levelIndex;
            if (!GameStorage.HasKey(key)) return false;

            string json = GameStorage.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                LevelResumeData data = JsonUtility.FromJson<LevelResumeData>(json);
                bool sameLevel = data != null && data.levelIndex == levelIndex;

                // Old saves carry no fingerprint; treating those as a mismatch discards them
                // once, which is the safe direction - a wrongly resumed level looks broken.
                bool samePuzzle = sameLevel && !string.IsNullOrEmpty(data.fingerprint)
                                  && string.Equals(data.fingerprint, fingerprint,
                                      StringComparison.OrdinalIgnoreCase);

                if (sameLevel && !samePuzzle)
                {
                    ClearLevelState(levelIndex);
                    return false;
                }

                if (samePuzzle)
                {
                    if (data.solvedWords != null) solvedWords.AddRange(data.solvedWords);
                    if (data.bonusWords != null) bonusWords.AddRange(data.bonusWords);
                    hintsUsed = data.hintsUsed;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProgressionService] Failed to parse saved level {levelIndex} state: {ex.Message}");
            }

            return false;
        }

        public void SaveLevelState(int levelIndex, string fingerprint, IEnumerable<string> solvedWords, IEnumerable<string> bonusWords, int hintsUsed)
        {
            LevelResumeData data = new LevelResumeData
            {
                levelIndex = levelIndex,
                fingerprint = fingerprint ?? string.Empty,
                solvedWords = solvedWords != null ? new List<string>(solvedWords) : new List<string>(),
                bonusWords = bonusWords != null ? new List<string>(bonusWords) : new List<string>(),
                hintsUsed = hintsUsed
            };

            string json = JsonUtility.ToJson(data);
            GameStorage.SetString(PrefKeyLevelStatePrefix + levelIndex, json);
            GameStorage.Save();
        }

        public void ClearLevelState(int levelIndex)
        {
            string key = PrefKeyLevelStatePrefix + levelIndex;
            if (GameStorage.HasKey(key))
            {
                GameStorage.DeleteKey(key);
                GameStorage.Save();
            }
        }

        public float GetBestTime(int levelIndex)
        {
            return GameStorage.GetFloat(PrefKeyBestTimePrefix + levelIndex, 0f);
        }

        public void SubmitTime(int levelIndex, float seconds)
        {
            if (seconds <= 0f) return;

            float best = GetBestTime(levelIndex);
            if (best > 0f && seconds >= best) return;   // slower than the record, so not a record

            GameStorage.SetFloat(PrefKeyBestTimePrefix + levelIndex, seconds);
            GameStorage.Save();
        }

        /// <summary>
        /// Re-reads everything for the mode that is now active. Called on a mode switch: the
        /// in-memory fields hold the previous mode's numbers and would otherwise be written
        /// back under the new mode's keys, merging the two campaigns.
        /// </summary>
        public void ReloadForCurrentMode()
        {
            LoadAll();
        }

        public void ResetAllProgress()
        {
            _currentLevelIndex = 1;
            _highestUnlockedLevel = 1;
            _coins = DefaultStartingCoins;
            _totalScore = 0;
            _totalWordsFound = 0;
            _totalBonusWordsFound = 0;

            // Clear all stored keys and level states completely
            GameStorage.DeleteAll();
            GameStorage.Save();

            // Re-save fresh clean defaults
            SaveAll();
        }
    }
}
