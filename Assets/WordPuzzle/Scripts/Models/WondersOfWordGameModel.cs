using System.Collections.Generic;
using UnityEngine;
using ServiceLocatorFramework;
using DataBindingFramework;
using WordPuzzle.Data;

namespace WordPuzzle.Models
{
    public class WordSubmittedEventData
    {
        public string SubmittedWord;
        public bool IsTargetMatch;
        public bool IsBonusWord;
        public bool IsAlreadySolved;
        public int MatchScore;
    }

    public class WondersOfWordGameModel
    {
        // Property Key Constants
        public const string KEY_CURRENT_LEVEL_INDEX = "CurrentLevelIndex";
        public const string KEY_COINS = "PlayerCoins";
        public const string KEY_SCORE = "PlayerScore";
        public const string KEY_GAME_STATE = "GameState";
        public const string KEY_CURRENT_WORD_PREVIEW = "CurrentWordPreview";
        public const string KEY_SOLVED_WORDS_COUNT = "SolvedWordsCount";
        public const string KEY_TARGET_WORDS_TOTAL = "TargetWordsTotal";
        public const string KEY_BONUS_WORDS_COUNT = "BonusWordsCount";
        public const string KEY_HINTS_USED = "HintsUsed";

        // Observer Key Constants
        public const string OBS_WORD_SUBMITTED = "WordSubmittedObserver";
        public const string OBS_WORD_MATCHED = "WordMatchedObserver";
        public const string OBS_WRONG_WORD = "WrongWordObserver";
        public const string OBS_BONUS_WORD_FOUND = "BonusWordFoundObserver";
        public const string OBS_WORD_ALREADY_FOUND = "WordAlreadyFoundObserver";
        public const string OBS_HINT_USED = "HintUsedObserver";
        public const string OBS_LEVEL_COMPLETED = "LevelCompletedObserver";
        public const string OBS_SWIPE_CHAR_ADDED = "SwipeCharAddedObserver";

        private readonly IPropertyManager _propertyManager;
        private readonly IObserverManager _observerManager;

        public Property<int> CurrentLevelIndex { get; private set; }
        public Property<int> Coins { get; private set; }
        public Property<int> Score { get; private set; }
        public Property<GameState> State { get; private set; }
        public Property<string> CurrentWordPreview { get; private set; }
        public Property<int> SolvedWordsCount { get; private set; }
        public Property<int> TargetWordsTotal { get; private set; }
        public Property<int> BonusWordsCount { get; private set; }
        public Property<int> HintsUsed { get; private set; }

        public HashSet<string> SolvedTargetWords { get; private set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        public HashSet<string> FoundBonusWords { get; private set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public WondersOfWordGameModel()
        {
            _propertyManager = ServiceLocator.Current.Get<IPropertyManager>();
            _observerManager = ServiceLocator.Current.Get<IObserverManager>();

            InitializeProperties();
            InitializeObservers();
        }

        private void InitializeProperties()
        {
            CurrentLevelIndex = _propertyManager.GetOrCreateProperty<int>(KEY_CURRENT_LEVEL_INDEX);
            Coins = _propertyManager.GetOrCreateProperty<int>(KEY_COINS);
            Score = _propertyManager.GetOrCreateProperty<int>(KEY_SCORE);
            State = _propertyManager.GetOrCreateProperty<GameState>(KEY_GAME_STATE);
            CurrentWordPreview = _propertyManager.GetOrCreateProperty<string>(KEY_CURRENT_WORD_PREVIEW);
            SolvedWordsCount = _propertyManager.GetOrCreateProperty<int>(KEY_SOLVED_WORDS_COUNT);
            TargetWordsTotal = _propertyManager.GetOrCreateProperty<int>(KEY_TARGET_WORDS_TOTAL);
            BonusWordsCount = _propertyManager.GetOrCreateProperty<int>(KEY_BONUS_WORDS_COUNT);
            HintsUsed = _propertyManager.GetOrCreateProperty<int>(KEY_HINTS_USED);

            // Restored values. The level index was previously written to PlayerPrefs but never
            // read back, so every launch restarted at level 1 regardless of progress.
            CurrentLevelIndex.Value = Mathf.Max(1, PlayerPrefs.GetInt(KEY_CURRENT_LEVEL_INDEX, 1));
            Coins.Value = PlayerPrefs.GetInt(KEY_COINS, 100);
            Score.Value = 0;
            State.Value = GameState.MainMenu;
            CurrentWordPreview.Value = "";
            SolvedWordsCount.Value = 0;
            TargetWordsTotal.Value = 0;
            BonusWordsCount.Value = 0;
            HintsUsed.Value = 0;
        }

        private void InitializeObservers()
        {
            _observerManager.GetOrCreateObserver<WordSubmittedEventData>(OBS_WORD_SUBMITTED);
            _observerManager.GetOrCreateObserver<string>(OBS_WORD_MATCHED);
            _observerManager.GetOrCreateObserver<string>(OBS_WRONG_WORD);
            _observerManager.GetOrCreateObserver<string>(OBS_BONUS_WORD_FOUND);
            _observerManager.GetOrCreateObserver<string>(OBS_WORD_ALREADY_FOUND);
            _observerManager.GetOrCreateObserver<HintType>(OBS_HINT_USED);
            _observerManager.GetOrCreateObserver<int>(OBS_LEVEL_COMPLETED);
            _observerManager.GetOrCreateObserver<char>(OBS_SWIPE_CHAR_ADDED);
        }

        /// <summary>
        /// Persists the resume point and wallet. CurrentLevelIndex is the level the player
        /// should land on next launch.
        /// </summary>
        public void SaveProgress()
        {
            PlayerPrefs.SetInt(KEY_CURRENT_LEVEL_INDEX, CurrentLevelIndex.Value);
            PlayerPrefs.SetInt(KEY_COINS, Coins.Value);
            PlayerPrefs.Save();
        }

        public void AddCoins(int amount)
        {
            Coins.Value += amount;
            PlayerPrefs.SetInt(KEY_COINS, Coins.Value);
            PlayerPrefs.Save();
        }

        public bool SpendCoins(int amount)
        {
            if (Coins.Value >= amount)
            {
                Coins.Value -= amount;
                PlayerPrefs.SetInt(KEY_COINS, Coins.Value);
                PlayerPrefs.Save();
                return true;
            }
            return false;
        }

        public void ResetLevelProgress(int totalTargets)
        {
            SolvedTargetWords.Clear();
            FoundBonusWords.Clear();
            SolvedWordsCount.Value = 0;
            TargetWordsTotal.Value = totalTargets;
            BonusWordsCount.Value = 0;
            HintsUsed.Value = 0;
            CurrentWordPreview.Value = "";
        }

        public void NotifySwipeCharAdded(char c)
        {
            var observer = _observerManager.GetOrCreateObserver<char>(OBS_SWIPE_CHAR_ADDED);
            observer.Notify(c);
        }

        public void NotifyWordSubmitted(WordSubmittedEventData eventData)
        {
            var observer = _observerManager.GetOrCreateObserver<WordSubmittedEventData>(OBS_WORD_SUBMITTED);
            observer.Notify(eventData);

            if (eventData.IsTargetMatch)
            {
                SolvedTargetWords.Add(eventData.SubmittedWord);
                SolvedWordsCount.Value = SolvedTargetWords.Count;
                Score.Value += eventData.MatchScore;
                AddCoins(COINS_TARGET_WORD);

                var matchObs = _observerManager.GetOrCreateObserver<string>(OBS_WORD_MATCHED);
                matchObs.Notify(eventData.SubmittedWord);

                if (SolvedWordsCount.Value >= TargetWordsTotal.Value && TargetWordsTotal.Value > 0)
                {
                    NotifyLevelCompleted();
                }
            }
            else if (eventData.IsBonusWord)
            {
                FoundBonusWords.Add(eventData.SubmittedWord);
                BonusWordsCount.Value = FoundBonusWords.Count;
                AddCoins(COINS_BONUS_WORD);

                var bonusObs = _observerManager.GetOrCreateObserver<string>(OBS_BONUS_WORD_FOUND);
                bonusObs.Notify(eventData.SubmittedWord);
            }
            else if (eventData.IsAlreadySolved)
            {
                var repeatObs = _observerManager.GetOrCreateObserver<string>(OBS_WORD_ALREADY_FOUND);
                repeatObs.Notify(eventData.SubmittedWord);
            }
            else
            {
                var wrongObs = _observerManager.GetOrCreateObserver<string>(OBS_WRONG_WORD);
                wrongObs.Notify(eventData.SubmittedWord);
            }
        }

        /// <summary>Score removed for each tile hint taken.</summary>
        public const int HINT_SCORE_PENALTY = 25;

        /// <summary>Coin rewards. Named so the UI can show the same numbers it pays out.</summary>
        public const int COINS_TARGET_WORD = 10;
        public const int COINS_BONUS_WORD = 5;
        public const int COINS_LEVEL_COMPLETE = 50;

        /// <summary>Full marks, minus one star per tile hint used, never below one.</summary>
        public int StarsEarned
        {
            get { return UnityEngine.Mathf.Clamp(3 - HintsUsed.Value, 1, 3); }
        }

        public void NotifyHintUsed(HintType type)
        {
            // Shuffle also routes through here but only rearranges letters - it reveals
            // nothing, so it must not cost a star or any score.
            if (type != HintType.ShuffleWheel)
            {
                HintsUsed.Value += 1;
                Score.Value = UnityEngine.Mathf.Max(0, Score.Value - HINT_SCORE_PENALTY);
            }

            var observer = _observerManager.GetOrCreateObserver<HintType>(OBS_HINT_USED);
            observer.Notify(type);
        }

        public void NotifyLevelCompleted()
        {
            State.Value = GameState.LevelComplete;
            AddCoins(COINS_LEVEL_COMPLETE);

            // Bank the next level immediately. Waiting for the Next button would lose the
            // clear if the player quits from the victory screen.
            PlayerPrefs.SetInt(KEY_CURRENT_LEVEL_INDEX, CurrentLevelIndex.Value + 1);
            PlayerPrefs.Save();

            var observer = _observerManager.GetOrCreateObserver<int>(OBS_LEVEL_COMPLETED);
            observer.Notify(CurrentLevelIndex.Value);
        }
    }
}
