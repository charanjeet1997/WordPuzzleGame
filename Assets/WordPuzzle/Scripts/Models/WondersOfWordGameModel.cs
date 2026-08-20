using System.Collections.Generic;
using UnityEngine;
using ServiceLocatorFramework;
using DataBindingFramework;
using WordPuzzle.Data;
using WordPuzzle.Services;

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
        public const string KEY_CURRENT_CHAPTER = "CurrentChapterTitle";
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

        /// <summary>Chapter caption of the level being played, e.g. "Chapter 3 - Whispering Woods".</summary>
        public Property<string> CurrentChapterTitle { get; private set; }

        /// <summary>Wheel letters of the level being played, used to fingerprint saved state.</summary>
        public string CurrentWheelLetters { get; set; } = string.Empty;

        /// <summary>Seconds spent on the current level. Only ticks in Time Trial.</summary>
        public Property<float> LevelSeconds { get; private set; }
        public Property<int> SolvedWordsCount { get; private set; }
        public Property<int> TargetWordsTotal { get; private set; }
        public Property<int> BonusWordsCount { get; private set; }
        public Property<int> HintsUsed { get; private set; }

        public HashSet<string> SolvedTargetWords { get; private set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        public HashSet<string> FoundBonusWords { get; private set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        private IProgressionService _progressionService;

        private IProgressionService GetProgressionService()
        {
            if (_progressionService == null && ServiceLocator.Current != null && ServiceLocator.Current.Has<IProgressionService>())
            {
                _progressionService = ServiceLocator.Current.Get<IProgressionService>();
            }
            return _progressionService;
        }

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
            CurrentChapterTitle = _propertyManager.GetOrCreateProperty<string>(KEY_CURRENT_CHAPTER);
            LevelSeconds = _propertyManager.GetOrCreateProperty<float>("LevelSeconds");
            SolvedWordsCount = _propertyManager.GetOrCreateProperty<int>(KEY_SOLVED_WORDS_COUNT);
            TargetWordsTotal = _propertyManager.GetOrCreateProperty<int>(KEY_TARGET_WORDS_TOTAL);
            BonusWordsCount = _propertyManager.GetOrCreateProperty<int>(KEY_BONUS_WORDS_COUNT);
            HintsUsed = _propertyManager.GetOrCreateProperty<int>(KEY_HINTS_USED);

            var prog = GetProgressionService();
            int initLevel = prog != null ? prog.CurrentLevelIndex : Mathf.Max(1, GameStorage.GetInt(GameModeContext.Key(KEY_CURRENT_LEVEL_INDEX), 1));
            int initCoins = prog != null ? prog.Coins : GameStorage.GetInt(KEY_COINS, 100);

            CurrentLevelIndex.Value = initLevel;
            Coins.Value = initCoins;
            Score.Value = 0;
            State.Value = GameState.MainMenu;
            CurrentWordPreview.Value = "";
            SolvedWordsCount.Value = 0;
            TargetWordsTotal.Value = 0;
            BonusWordsCount.Value = 0;
            HintsUsed.Value = 0;
            LevelSeconds.Value = 0f;
        }

        /// <summary>
        /// Pulls campaign position for whichever mode is now active. Without this a mode
        /// switch would keep the previous mode's level number in memory and then save it
        /// under the new mode's key, quietly merging the two campaigns.
        /// </summary>
        public void ReloadForCurrentMode()
        {
            var prog = GetProgressionService();
            if (prog is ProgressionService concrete) concrete.ReloadForCurrentMode();

            CurrentLevelIndex.Value = prog != null
                ? prog.CurrentLevelIndex
                : Mathf.Max(1, GameStorage.GetInt(GameModeContext.Key(KEY_CURRENT_LEVEL_INDEX), 1));

            LevelSeconds.Value = 0f;
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

        public void SaveProgress()
        {
            var prog = GetProgressionService();
            if (prog != null)
            {
                prog.CurrentLevelIndex = CurrentLevelIndex.Value;
                prog.Coins = Coins.Value;
                prog.SaveAll();
            }
            else
            {
                GameStorage.SetInt(GameModeContext.Key(KEY_CURRENT_LEVEL_INDEX), CurrentLevelIndex.Value);
                GameStorage.SetInt(KEY_COINS, Coins.Value);
                GameStorage.Save();
            }
        }

        public void AddCoins(int amount)
        {
            Coins.Value += amount;
            var prog = GetProgressionService();
            if (prog != null)
            {
                prog.Coins = Coins.Value;
                prog.SaveAll();
            }
            else
            {
                GameStorage.SetInt(KEY_COINS, Coins.Value);
                GameStorage.Save();
            }
        }

        public bool SpendCoins(int amount)
        {
            if (Coins.Value >= amount)
            {
                Coins.Value -= amount;
                var prog = GetProgressionService();
                if (prog != null)
                {
                    prog.Coins = Coins.Value;
                    prog.SaveAll();
                }
                else
                {
                    GameStorage.SetInt(KEY_COINS, Coins.Value);
                    GameStorage.Save();
                }
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

        public void SaveMidLevelState()
        {
            var prog = GetProgressionService();
            if (prog != null)
            {
                prog.SaveLevelState(CurrentLevelIndex.Value, CurrentWheelLetters, SolvedTargetWords, FoundBonusWords, HintsUsed.Value);
            }
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

                var prog = GetProgressionService();
                if (prog != null)
                {
                    prog.TotalWordsFound += 1;
                    prog.TotalScore += eventData.MatchScore;
                }

                SaveMidLevelState();

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

                var prog = GetProgressionService();
                if (prog != null)
                {
                    prog.TotalBonusWordsFound += 1;
                }

                SaveMidLevelState();

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

        public const int HINT_SCORE_PENALTY = 25;
        public const int COINS_TARGET_WORD = 10;
        public const int COINS_BONUS_WORD = 5;
        public const int COINS_LEVEL_COMPLETE = 50;

        public int StarsEarned => Mathf.Clamp(3 - HintsUsed.Value, 1, 3);

        public void NotifyHintUsed(HintType type)
        {
            if (type != HintType.ShuffleWheel)
            {
                HintsUsed.Value += 1;
                Score.Value = Mathf.Max(0, Score.Value - HINT_SCORE_PENALTY);
                SaveMidLevelState();
            }

            var observer = _observerManager.GetOrCreateObserver<HintType>(OBS_HINT_USED);
            observer.Notify(type);
        }

        public void NotifyLevelCompleted()
        {
            State.Value = GameState.LevelComplete;
            AddCoins(COINS_LEVEL_COMPLETE);

            int completedLevel = CurrentLevelIndex.Value;
            int stars = StarsEarned;

            var prog = GetProgressionService();
            if (prog != null)
            {
                prog.SetStarsForLevel(completedLevel, stars);
                prog.ClearLevelState(completedLevel);
                prog.CurrentLevelIndex = completedLevel + 1;
                prog.SaveAll();
            }
            else
            {
                GameStorage.SetInt(GameModeContext.Key(KEY_CURRENT_LEVEL_INDEX), completedLevel + 1);
                GameStorage.Save();
            }

            var observer = _observerManager.GetOrCreateObserver<int>(OBS_LEVEL_COMPLETED);
            observer.Notify(completedLevel);
        }
    }
}
