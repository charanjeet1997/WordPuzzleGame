using System.Collections.Generic;
using UnityEngine;
using CommanTickManager;
using ServiceLocatorFramework;
using DataBindingFramework;
using Games.WorldSystem;
using WordPuzzle.Data;
using WordPuzzle.Models;
using WordPuzzle.UI;
using WordPuzzle.Gameplay;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;
using WordPuzzle.Services;

namespace WordPuzzle.Managers
{
    public class GameManager : MonoBehaviour, ITick
    {
        [Header("World System Settings")]
        public WorldName worldName = WorldName.WordPuzzleWorld;

        [Header("Level Data References (Direct - No Resources.Load)")]
        public LevelDatabase levelDatabase;
        public List<LevelData> levels = new List<LevelData>();

        [Header("View Config References (Direct - No Resources.Load)")]
        public ViewConfig configMainMenu;
        public ViewConfig configHUD;
        public ViewConfig configPause;
        public ViewConfig configLevelComplete;
        public ViewConfig configSettings;
        public ViewConfig configModeSelect;

        [Header("Level Complete")]
        [Tooltip("Seconds the last word's meaning stays on screen before the victory card appears.")]
        public float levelCompleteDelay = 2.6f;

        private Coroutine _levelCompleteRoutine;
        private WondersOfWordGameModel _model;
        private UIManager _uiManager;
        private GameplayHandler _gameplayHandler;
        private IObserver<int> _levelCompletedObserver;

        private void Awake()
        {
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<GameManager>())
            {
                ServiceLocator.Current.Register<GameManager>(this);
            }
        }

        private void OnEnable()
        {
            if (ProcessingUpdate.Instance != null)
            {
                ProcessingUpdate.Instance.Add(this);
            }
        }

        private void OnDisable()
        {
            if (ProcessingUpdate.Instance != null)
            {
                ProcessingUpdate.Instance.Remove(this);
            }
        }

        private void OnDestroy()
        {
            CancelPendingLevelComplete();
            if (_levelCompletedObserver != null)
            {
                _levelCompletedObserver.Unbind(OnLevelCompletedNotification);
                _levelCompletedObserver = null;
            }

            if (ServiceLocator.Current.Has<GameManager>())
            {
                ServiceLocator.Current.Unregister<GameManager>();
            }
        }

        private void Start()
        {
            if (ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _model = ServiceLocator.Current.Get<WondersOfWordGameModel>();
            if (ServiceLocator.Current.Has<UIManager>())
                _uiManager = ServiceLocator.Current.Get<UIManager>();
            if (ServiceLocator.Current.Has<GameplayHandler>())
                _gameplayHandler = ServiceLocator.Current.Get<GameplayHandler>();

            if (ServiceLocator.Current.Has<IObserverManager>())
            {
                var observerMgr = ServiceLocator.Current.Get<IObserverManager>();
                _levelCompletedObserver = observerMgr.GetOrCreateObserver<int>(WondersOfWordGameModel.OBS_LEVEL_COMPLETED);
                _levelCompletedObserver.Bind(this, OnLevelCompletedNotification);
            }

            EnsureFallbackConfigs();

        }

        private void OnLevelCompletedNotification(int levelIndex)
        {
            // Deliberately not immediate: the final word's meaning toast is still on screen,
            // and the victory card would cover it before it could be read.
            if (_levelCompleteRoutine != null) StopCoroutine(_levelCompleteRoutine);
            _levelCompleteRoutine = StartCoroutine(ShowLevelCompleteAfterDelay());
        }

        private System.Collections.IEnumerator ShowLevelCompleteAfterDelay()
        {
            // Recorded before the state flip, while the level's elapsed time is still current.
            if (GameModeContext.IsTimed && _model != null && ServiceLocator.Current.Has<IProgressionService>())
            {
                ServiceLocator.Current.Get<IProgressionService>()
                    .SubmitTime(_model.CurrentLevelIndex.Value, _model.LevelSeconds.Value);
            }

            // The state flips now so no further swipes are scored during the pause, while the
            // card itself waits for the toast.
            if (_model != null) _model.State.Value = GameState.LevelComplete;

            // Unscaled: the level-complete path is allowed to run with a paused time scale.
            yield return new WaitForSecondsRealtime(levelCompleteDelay);

            _levelCompleteRoutine = null;
            ShowLevelComplete();
        }

        public void Tick()
        {
            if (_model != null && _model.State.Value == GameState.Playing)
            {
                // Scaled time on purpose: pausing the game must stop the clock, or players
                // would pause to think and keep their record.
                if (GameModeContext.IsTimed)
                {
                    _model.LevelSeconds.Value += Time.deltaTime;
                }

                if (WorldManager.Instance != null)
                {
                    var entities = WorldManager.Instance.GetCurrentWorldEntity();
                    if (entities != null)
                    {
                        WorldRunningStateProvider runningStateProvider = new WorldRunningStateProvider(entities);
                        WorldManager.Instance.ChangeWorldStateTo(0.1f, runningStateProvider);
                    }
                }
            }
        }

        public void StartCurrentLevel()
        {
            // A card queued by the previous level must not land on top of the new one.
            CancelPendingLevelComplete();

            // Each level is timed from zero, not from when the mode was entered.
            if (_model != null) _model.LevelSeconds.Value = 0f;

            SpawnWorldIfRequired();

            int lvlIndex = _model != null ? _model.CurrentLevelIndex.Value : 1;
            LevelData data = GetLevelData(lvlIndex);

            if (_gameplayHandler == null && ServiceLocator.Current.Has<GameplayHandler>())
            {
                _gameplayHandler = ServiceLocator.Current.Get<GameplayHandler>();
            }

            if (_gameplayHandler != null && data != null)
            {
                _gameplayHandler.LoadLevel(data);
            }

            if (WorldManager.Instance != null)
            {
                var entities = WorldManager.Instance.GetCurrentWorldEntity();
                if (entities != null)
                {
                    WorldInitStateProvider initStateProvider = new WorldInitStateProvider(entities);
                    WorldManager.Instance.ChangeWorldStateTo(0.1f, initStateProvider);
                }
            }

            if (_uiManager != null && configHUD != null)
            {
                _uiManager.ShowView(configHUD);
            }
        }

        private void SpawnWorldIfRequired()
        {
            World currentWorld = null;

            if (ServiceLocator.Current.Has<IWorldManager>())
            {
                var worldMgr = ServiceLocator.Current.Get<IWorldManager>();
                worldMgr.CreateWorld(worldName);
                if (worldMgr is WorldManager wm)
                {
                    currentWorld = wm.GetCurrentWorld();
                }
            }
            else if (WorldManager.Instance != null)
            {
                WorldManager.Instance.CreateWorld(worldName);
                currentWorld = WorldManager.Instance.GetCurrentWorld();
            }

            if (currentWorld != null)
            {
                _gameplayHandler = currentWorld.GetComponentInChildren<GameplayHandler>();
            }
        }

        public void LoadNextLevel()
        {
            if (_model != null)
            {
                _model.CurrentLevelIndex.Value += 1;
                _model.SaveProgress();
            }

            StartCurrentLevel();
        }

        /// <summary>
        /// Opens the mode carousel. PLAY routes here rather than straight into a level so the
        /// player picks which campaign to continue - the two advance independently.
        /// </summary>
        public void ShowModeSelect()
        {
            if (_uiManager == null && ServiceLocator.Current.Has<UIManager>())
                _uiManager = ServiceLocator.Current.Get<UIManager>();

            // Without the screen wired up, falling through to the last played mode beats
            // doing nothing when PLAY is pressed.
            if (_uiManager == null || configModeSelect == null)
            {
                StartCurrentLevel();
                return;
            }

            _uiManager.ShowOverlay(configModeSelect);
        }

        public void ShowSettings()
        {
            if (_uiManager == null && ServiceLocator.Current.Has<UIManager>())
                _uiManager = ServiceLocator.Current.Get<UIManager>();

            if (_uiManager != null && configSettings != null)
            {
                _uiManager.ShowOverlay(configSettings);
            }
        }

        public void PauseGame()
        {
            if (_model != null) _model.State.Value = GameState.Paused;
            if (WorldManager.Instance != null)
            {
                var entities = WorldManager.Instance.GetCurrentWorldEntity();
                if (entities != null)
                {
                    WorldPauseStateProvider pauseStateProvider = new WorldPauseStateProvider(entities);
                    WorldManager.Instance.ChangeWorldStateTo(0.1f, pauseStateProvider);
                }
            }
            if (_uiManager != null && configPause != null)
            {
                _uiManager.ShowOverlay(configPause);
            }
        }

        public void ResumeGame()
        {
            if (_model != null && _model.State.Value == GameState.Paused)
            {
                _model.State.Value = GameState.Playing;
                Time.timeScale = 1f;
            }
            if (_uiManager != null && configPause != null)
            {
                _uiManager.HideOverlay(configPause);
            }
        }

        public void QuitToMainMenu()
        {
            if (_model != null) _model.State.Value = GameState.MainMenu;

            if (WorldManager.Instance != null)
            {
                WorldManager.Instance.DestroyWorld(worldName);
            }

            if (_uiManager != null && configMainMenu != null)
            {
                _uiManager.ShowView(configMainMenu);
            }
        }

        /// <summary>Drops any queued victory card, e.g. when the player quits to the menu mid-delay.</summary>
        public void CancelPendingLevelComplete()
        {
            if (_levelCompleteRoutine == null) return;

            StopCoroutine(_levelCompleteRoutine);
            _levelCompleteRoutine = null;
        }

        public void ShowLevelComplete()
        {
            if (_model != null) _model.State.Value = GameState.LevelComplete;

            if (ServiceLocator.Current.Has<AudioManager>())
            {
                ServiceLocator.Current.Get<AudioManager>().PlayFanfareSound();
            }

            HapticManager.Play(HapticType.Heavy);

            if (_uiManager != null && configLevelComplete != null)
            {
                _uiManager.ShowOverlay(configLevelComplete);
            }
        }

        /// <summary>
        /// The level the player would start next. Exposed so the menu card can describe it
        /// instead of showing hardcoded placeholder text.
        /// </summary>
        public LevelData GetCurrentLevelData()
        {
            int index = _model != null ? _model.CurrentLevelIndex.Value : 1;
            return GetLevelData(index);
        }

        private LevelData GetLevelData(int levelNumber)
        {
            // Database first: it holds the authored campaign. The inline list stays as a
            // fallback so existing scenes keep working without a database assigned.
            if (levelDatabase != null && levelDatabase.Count > 0)
            {
                return levelDatabase.GetLevel(levelNumber);
            }

            if (levels != null && levels.Count > 0)
            {
                int index = (levelNumber - 1) % levels.Count;
                return levels[index];
            }

            return CreateFallbackLevel(levelNumber);
        }

        private LevelData CreateFallbackLevel(int levelNumber)
        {
            LevelData fallback = new LevelData();
            fallback.levelName = $"Level_{levelNumber:D4}";
            fallback.levelNumber = levelNumber;
            fallback.chapterTitle = $"Chapter {((levelNumber - 1) / 5) + 1} - Sunrise Trail";
            fallback.wheelLetters = "CATS";

            fallback.targetWords = new List<TargetWordEntry>
            {
                new TargetWordEntry { word = "CATS", startRow = 0, startCol = 0, orientation = WordOrientation.Horizontal },
                new TargetWordEntry { word = "ACT", startRow = 0, startCol = 1, orientation = WordOrientation.Vertical },
                new TargetWordEntry { word = "SAT", startRow = 2, startCol = 0, orientation = WordOrientation.Horizontal }
            };

            return fallback;
        }

        private void EnsureFallbackConfigs()
        {
            if (configMainMenu == null) configMainMenu = CreateFallbackConfig("MainMenu");
            if (configHUD == null) configHUD = CreateFallbackConfig("HUD");
            if (configPause == null) configPause = CreateFallbackConfig("PauseOverlay");
            if (configLevelComplete == null) configLevelComplete = CreateFallbackConfig("LevelComplete");
        }

        private ViewConfig CreateFallbackConfig(string viewId)
        {
            ViewConfig cfg = ScriptableObject.CreateInstance<ViewConfig>();
            cfg.viewId = viewId;
            cfg.shouldHidePreviousUI = (viewId == "MainMenu" || viewId == "HUD");
            return cfg;
        }
    }
}
