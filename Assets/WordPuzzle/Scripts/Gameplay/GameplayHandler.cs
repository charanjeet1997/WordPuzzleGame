using System.Collections.Generic;
using UnityEngine;
using Games.WorldSystem;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Models;
using WordPuzzle.Services;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;

namespace WordPuzzle.Gameplay
{
    public class GameplayHandler : MonoBehaviour,
        IWorldEntity,
        IWorldInitState,
        IWorldRunningState,
        IWorldPauseState,
        IGameOverState,
        IWorldDeinitState
    {
        [Header("Gameplay References")]
        public LetterWheelController wheelController;
        public CrosswordGridController gridController;

        private WondersOfWordGameModel _gameModel;
        private AudioManager _audioManager;
        private IParticleService _particleService;
        private LevelData _currentLevelData;
        private bool _isInitialized = false;
        private bool _subscribedToWheel = false;

        private void Awake()
        {
            // Nothing else registers this. Without it HUDView's lookup fails and Hint/Shuffle
            // silently no-op, because both are guarded by a null check on the handler.
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<GameplayHandler>())
            {
                ServiceLocator.Current.Register<GameplayHandler>(this);
            }
        }

        private void Start()
        {
            EnsureServices();
        }

        /// <summary>
        /// Resolves services on demand rather than only in Start(). GameManager spawns the world
        /// prefab and calls LoadLevel in the same frame, so Start() has not run yet on this
        /// component the first time round — resolving only there left _gameModel null and silently
        /// skipped ResetLevelProgress, leaving TargetWordsTotal at 0 so a level could never complete.
        /// </summary>
        private void EnsureServices()
        {
            if (_gameModel == null && ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_particleService == null && ServiceLocator.Current.Has<IParticleService>())
                _particleService = ServiceLocator.Current.Get<IParticleService>();

            if (!_subscribedToWheel && wheelController != null)
            {
                wheelController.OnWordSubmitted += HandleWordSubmitted;
                _subscribedToWheel = true;
            }
        }

        private void OnDestroy()
        {
            if (_subscribedToWheel && wheelController != null)
            {
                wheelController.OnWordSubmitted -= HandleWordSubmitted;
                _subscribedToWheel = false;
            }

            // The world is rebuilt per level, so a stale registration would leave the HUD
            // pointing at a destroyed handler.
            if (ServiceLocator.Current != null
                && ServiceLocator.Current.Has<GameplayHandler>()
                && ServiceLocator.Current.Get<GameplayHandler>() == this)
            {
                ServiceLocator.Current.Unregister<GameplayHandler>();
            }
        }

        #region World System Interfaces Implementation
        public void Initialize()
        {
            EnsureServices();
            _isInitialized = true;
        }

        public void Running()
        {
            if (_gameModel != null && _gameModel.State.Value == GameState.Playing)
            {
                // Active running tick
            }
        }

        public void Pause()
        {
            if (_gameModel != null && _gameModel.State.Value == GameState.Playing)
            {
                _gameModel.State.Value = GameState.Paused;
                Time.timeScale = 0f;
            }
        }

        public void GameOver()
        {
            if (_gameModel != null)
            {
                _gameModel.State.Value = GameState.LevelComplete;
            }
        }

        public void Deinit()
        {
            _isInitialized = false;
        }
        #endregion

        public void LoadLevel(LevelData levelData)
        {
            EnsureServices();

            _currentLevelData = levelData;
            if (levelData == null) return;

            int targetCount = levelData.targetWords != null ? levelData.targetWords.Count : 0;
            if (_gameModel != null)
            {
                // The chapter caption is authored per level, so it travels with the level data
                // rather than being recomputed from the level number in the HUD.
                _gameModel.CurrentChapterTitle.Value = levelData.chapterTitle ?? string.Empty;
                _gameModel.ResetLevelProgress(targetCount);
                _gameModel.State.Value = GameState.Playing;
            }

            if (gridController != null)
            {
                gridController.BuildGrid(levelData);
            }

            // Restore any in-progress mid-level words if saved
            if (_gameModel != null && ServiceLocator.Current.Has<IProgressionService>())
            {
                var prog = ServiceLocator.Current.Get<IProgressionService>();
                if (prog.TryGetSavedLevelState(_gameModel.CurrentLevelIndex.Value, out var savedSolved, out var savedBonus, out var savedHints))
                {
                    if (savedSolved != null && savedSolved.Count > 0 && levelData.targetWords != null)
                    {
                        var validTargets = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                        foreach (var entry in levelData.targetWords)
                        {
                            if (!string.IsNullOrEmpty(entry.word)) validTargets.Add(entry.word.ToUpperInvariant());
                        }

                        foreach (string w in savedSolved)
                        {
                            string upper = w.Trim().ToUpperInvariant();
                            if (validTargets.Contains(upper))
                            {
                                if (gridController != null) gridController.TryRevealWord(upper, out _);
                                if (!_gameModel.SolvedTargetWords.Contains(upper))
                                {
                                    _gameModel.SolvedTargetWords.Add(upper);
                                }
                            }
                        }
                        _gameModel.SolvedWordsCount.Value = _gameModel.SolvedTargetWords.Count;
                    }
                    if (savedBonus != null && savedBonus.Count > 0)
                    {
                        foreach (string b in savedBonus) _gameModel.FoundBonusWords.Add(b);
                        _gameModel.BonusWordsCount.Value = _gameModel.FoundBonusWords.Count;
                    }
                    _gameModel.HintsUsed.Value = savedHints;
                }
            }

            if (wheelController != null)
            {
                wheelController.SetupWheel(levelData.wheelLetters);
            }
        }

        public void HandleWordSubmitted(string word)
        {
            EnsureServices();

            if (string.IsNullOrWhiteSpace(word) || _gameModel == null || _currentLevelData == null) return;

            string upperWord = word.Trim().ToUpperInvariant();
            List<string> targets = new List<string>();
            foreach (var t in _currentLevelData.targetWords) targets.Add(t.word.ToUpperInvariant());

            bool isTargetMatch = targets.Contains(upperWord);

            // A repeat is a repeat whether the word was a grid target or a bonus find.
            // Only solved targets were checked before, so re-swiping a bonus word fell
            // through to the invalid-word path and got rejected.
            bool isAlreadySolved = _gameModel.SolvedTargetWords.Contains(upperWord)
                                   || _gameModel.FoundBonusWords.Contains(upperWord);
            bool isBonusWord = false;

            if (!isTargetMatch && !isAlreadySolved)
            {
                isBonusWord = WordDictionary.IsBonusWord(upperWord, targets) && !_gameModel.FoundBonusWords.Contains(upperWord);
            }

            WordSubmittedEventData eventData = new WordSubmittedEventData
            {
                SubmittedWord = upperWord,
                IsTargetMatch = isTargetMatch && !isAlreadySolved,
                IsBonusWord = isBonusWord,
                IsAlreadySolved = isAlreadySolved,
                MatchScore = upperWord.Length * 10
            };

            if (eventData.IsTargetMatch && gridController != null)
            {
                gridController.TryRevealWord(upperWord, out Vector3 wordCenter);
                if (_audioManager != null) _audioManager.PlayWordMatchedSound();
                if (_particleService != null) _particleService.PlayWordMatchBurst(wordCenter);
                HapticManager.Play(HapticType.Medium);
            }
            else if (eventData.IsBonusWord)
            {
                if (_audioManager != null) _audioManager.PlayBonusWordSound();
                // A bonus word is never on the grid, so the wheel it was swiped on is the only
                // position it actually belongs to.
                if (_particleService != null)
                {
                    _particleService.PlayBonusWordSparkle(
                        wheelController != null ? wheelController.transform.position : transform.position);
                }
                HapticManager.Play(HapticType.Medium);
            }
            else if (eventData.IsAlreadySolved)
            {
                // Already credited - acknowledge it without the failure buzz.
                if (_audioManager != null) _audioManager.PlayButtonClickSound();
                HapticManager.Play(HapticType.Light);
            }
            else
            {
                if (_audioManager != null) _audioManager.PlayWrongWordSound();
                HapticManager.Play(HapticType.Failure);
            }

            _gameModel.NotifyWordSubmitted(eventData);
        }

        /// <summary>Coin price of one revealed tile. The HUD label reads from here too.</summary>
        public const int SingleTileHintCost = 20;

        public bool UseSingleTileHint()
        {
            EnsureServices();

            if (_gameModel == null) return false;

            // Nothing left to reveal - checked before spending, otherwise the player is
            // charged 20 coins for a hint that does nothing.
            if (gridController == null || !gridController.HasHiddenTiles())
            {
                if (_audioManager != null) _audioManager.PlayWrongWordSound();
                return false;
            }

            if (!_gameModel.SpendCoins(SingleTileHintCost))
            {
                // Too few coins. Silence here reads as a broken button, so the refusal
                // gets the same rejection cue as an invalid word.
                if (_audioManager != null) _audioManager.PlayWrongWordSound();
                HapticManager.Play(HapticType.Failure);
                return false;
            }

            if (!gridController.RevealRandomHiddenTile(out Vector3 revealedTilePos)) return false;

            if (_audioManager != null) _audioManager.PlayHintSound();
            if (_particleService != null) _particleService.PlayTileRevealSparkle(revealedTilePos);
            HapticManager.Play(HapticType.Light);
            _gameModel.NotifyHintUsed(HintType.SingleTile);
            return true;
        }

        public void ShuffleWheel()
        {
            if (wheelController != null)
            {
                wheelController.ShuffleWheel();
                if (_gameModel != null) _gameModel.NotifyHintUsed(HintType.ShuffleWheel);
            }
        }
    }
}
