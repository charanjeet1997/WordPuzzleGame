using System;
using System.Collections.Generic;
using UnityEngine;
using ServiceLocatorFramework;

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
        bool TryGetSavedLevelState(int levelIndex, out List<string> solvedWords, out List<string> bonusWords, out int hintsUsed);
        void SaveLevelState(int levelIndex, IEnumerable<string> solvedWords, IEnumerable<string> bonusWords, int hintsUsed);
        void ClearLevelState(int levelIndex);

        void SaveAll();
        void ResetAllProgress();
    }

    public class ProgressionService : MonoBehaviour, IProgressionService
    {
        private const string PrefKeyCurrentLevel = "WordPuzzle_CurrentLevel";
        private const string PrefKeyHighestLevel = "WordPuzzle_HighestLevel";
        private const string PrefKeyCoins = "WordPuzzle_Coins";
        private const string PrefKeyTotalScore = "WordPuzzle_TotalScore";
        private const string PrefKeyTotalWords = "WordPuzzle_TotalWords";
        private const string PrefKeyTotalBonusWords = "WordPuzzle_TotalBonusWords";
        private const string PrefKeyStarsPrefix = "WordPuzzle_Stars_Lvl_";
        private const string PrefKeyLevelStatePrefix = "WordPuzzle_State_Lvl_";

        public const int DefaultStartingCoins = 100;

        [Serializable]
        private class LevelResumeData
        {
            public int levelIndex;
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
                    PlayerPrefs.SetInt(PrefKeyHighestLevel, _highestUnlockedLevel);
                }
                PlayerPrefs.SetInt(PrefKeyCurrentLevel, _currentLevelIndex);
            }
        }

        public int HighestUnlockedLevel
        {
            get => _highestUnlockedLevel;
            set
            {
                _highestUnlockedLevel = Mathf.Max(1, value);
                PlayerPrefs.SetInt(PrefKeyHighestLevel, _highestUnlockedLevel);
            }
        }

        public int Coins
        {
            get => _coins;
            set
            {
                _coins = Mathf.Max(0, value);
                PlayerPrefs.SetInt(PrefKeyCoins, _coins);
            }
        }

        public int TotalScore
        {
            get => _totalScore;
            set
            {
                _totalScore = Mathf.Max(0, value);
                PlayerPrefs.SetInt(PrefKeyTotalScore, _totalScore);
            }
        }

        public int TotalWordsFound
        {
            get => _totalWordsFound;
            set
            {
                _totalWordsFound = Mathf.Max(0, value);
                PlayerPrefs.SetInt(PrefKeyTotalWords, _totalWordsFound);
            }
        }

        public int TotalBonusWordsFound
        {
            get => _totalBonusWordsFound;
            set
            {
                _totalBonusWordsFound = Mathf.Max(0, value);
                PlayerPrefs.SetInt(PrefKeyTotalBonusWords, _totalBonusWordsFound);
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
            _currentLevelIndex = Mathf.Max(1, PlayerPrefs.GetInt(PrefKeyCurrentLevel, 1));
            _highestUnlockedLevel = Mathf.Max(_currentLevelIndex, PlayerPrefs.GetInt(PrefKeyHighestLevel, 1));
            _coins = PlayerPrefs.GetInt(PrefKeyCoins, DefaultStartingCoins);
            _totalScore = PlayerPrefs.GetInt(PrefKeyTotalScore, 0);
            _totalWordsFound = PlayerPrefs.GetInt(PrefKeyTotalWords, 0);
            _totalBonusWordsFound = PlayerPrefs.GetInt(PrefKeyTotalBonusWords, 0);
        }

        public void SaveAll()
        {
            PlayerPrefs.SetInt(PrefKeyCurrentLevel, _currentLevelIndex);
            PlayerPrefs.SetInt(PrefKeyHighestLevel, _highestUnlockedLevel);
            PlayerPrefs.SetInt(PrefKeyCoins, _coins);
            PlayerPrefs.SetInt(PrefKeyTotalScore, _totalScore);
            PlayerPrefs.SetInt(PrefKeyTotalWords, _totalWordsFound);
            PlayerPrefs.SetInt(PrefKeyTotalBonusWords, _totalBonusWordsFound);
            PlayerPrefs.Save();
        }

        public int GetStarsForLevel(int levelIndex)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyStarsPrefix + levelIndex, 0), 0, 3);
        }

        public void SetStarsForLevel(int levelIndex, int stars)
        {
            int currentStars = GetStarsForLevel(levelIndex);
            if (stars > currentStars)
            {
                PlayerPrefs.SetInt(PrefKeyStarsPrefix + levelIndex, Mathf.Clamp(stars, 1, 3));
                PlayerPrefs.Save();
            }
        }

        public bool HasSavedLevelState(int levelIndex)
        {
            return PlayerPrefs.HasKey(PrefKeyLevelStatePrefix + levelIndex);
        }

        public bool TryGetSavedLevelState(int levelIndex, out List<string> solvedWords, out List<string> bonusWords, out int hintsUsed)
        {
            solvedWords = new List<string>();
            bonusWords = new List<string>();
            hintsUsed = 0;

            string key = PrefKeyLevelStatePrefix + levelIndex;
            if (!PlayerPrefs.HasKey(key)) return false;

            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                LevelResumeData data = JsonUtility.FromJson<LevelResumeData>(json);
                if (data != null && data.levelIndex == levelIndex)
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

        public void SaveLevelState(int levelIndex, IEnumerable<string> solvedWords, IEnumerable<string> bonusWords, int hintsUsed)
        {
            LevelResumeData data = new LevelResumeData
            {
                levelIndex = levelIndex,
                solvedWords = solvedWords != null ? new List<string>(solvedWords) : new List<string>(),
                bonusWords = bonusWords != null ? new List<string>(bonusWords) : new List<string>(),
                hintsUsed = hintsUsed
            };

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PrefKeyLevelStatePrefix + levelIndex, json);
            PlayerPrefs.Save();
        }

        public void ClearLevelState(int levelIndex)
        {
            string key = PrefKeyLevelStatePrefix + levelIndex;
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        public void ResetAllProgress()
        {
            _currentLevelIndex = 1;
            _highestUnlockedLevel = 1;
            _coins = DefaultStartingCoins;
            _totalScore = 0;
            _totalWordsFound = 0;
            _totalBonusWordsFound = 0;

            // Clear stored keys
            PlayerPrefs.DeleteKey(PrefKeyCurrentLevel);
            PlayerPrefs.DeleteKey(PrefKeyHighestLevel);
            PlayerPrefs.DeleteKey(PrefKeyCoins);
            PlayerPrefs.DeleteKey(PrefKeyTotalScore);
            PlayerPrefs.DeleteKey(PrefKeyTotalWords);
            PlayerPrefs.DeleteKey(PrefKeyTotalBonusWords);

            // Clear any level state keys
            for (int i = 1; i <= 200; i++)
            {
                if (PlayerPrefs.HasKey(PrefKeyStarsPrefix + i)) PlayerPrefs.DeleteKey(PrefKeyStarsPrefix + i);
                if (PlayerPrefs.HasKey(PrefKeyLevelStatePrefix + i)) PlayerPrefs.DeleteKey(PrefKeyLevelStatePrefix + i);
            }

            SaveAll();
        }
    }
}
