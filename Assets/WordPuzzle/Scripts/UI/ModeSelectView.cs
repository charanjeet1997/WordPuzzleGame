using UnityEngine;
using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;
using WordPuzzle.Managers;
using WordPuzzle.Models;
using WordPuzzle.Services;

namespace WordPuzzle.UI
{
    /// <summary>
    /// Mode picker: two buttons, one selected, then PLAY.
    ///
    /// This replaced a drag carousel. With only two options a carousel asked the player to
    /// drag in order to discover something that always fits on screen, and hid half the
    /// choice behind a gesture for no gain.
    /// </summary>
    public class ModeSelectView : BaseUI
    {
        private const string SelectedClass = "mode-option--selected";

        private Button _classicButton;
        private Button _timeTrialButton;
        private Button _playButton;
        private Label _backLabel;

        private AudioManager _audioManager;
        private GameManager _gameManager;
        private WondersOfWordGameModel _gameModel;

        private GameMode _selected = GameMode.Classic;

        protected override void OnInitialize()
        {
            if (ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();
            if (ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            _classicButton = rootElement.Q<Button>("btn-mode-classic");
            _timeTrialButton = rootElement.Q<Button>("btn-mode-timetrial");
            _playButton = rootElement.Q<Button>("btn-play-mode");
            _backLabel = rootElement.Q<Label>("lbl-mode-back");

            if (_classicButton != null) _classicButton.clicked += OnClassicClicked;
            if (_timeTrialButton != null) _timeTrialButton.clicked += OnTimeTrialClicked;
            if (_playButton != null) _playButton.clicked += OnPlay;
            if (_backLabel != null) _backLabel.RegisterCallback<ClickEvent>(OnBack);

            // Opens on whatever was played last, so the common case is one tap on PLAY.
            _selected = GameModeContext.Current;

            RefreshSelection();
            RefreshProgressLabels();
        }

        protected override void OnHide()
        {
            if (_classicButton != null) _classicButton.clicked -= OnClassicClicked;
            if (_timeTrialButton != null) _timeTrialButton.clicked -= OnTimeTrialClicked;
            if (_playButton != null) _playButton.clicked -= OnPlay;
            if (_backLabel != null) _backLabel.UnregisterCallback<ClickEvent>(OnBack);
        }

        private void OnClassicClicked() => Select(GameMode.Classic);

        private void OnTimeTrialClicked() => Select(GameMode.TimeTrial);

        private void Select(GameMode mode)
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Selection);

            _selected = mode;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            _classicButton?.EnableInClassList(SelectedClass, _selected == GameMode.Classic);
            _timeTrialButton?.EnableInClassList(SelectedClass, _selected == GameMode.TimeTrial);
        }

        /// <summary>
        /// Each button shows its own mode's saved position. The two campaigns advance
        /// independently, so the player needs to see both before choosing.
        /// </summary>
        private void RefreshProgressLabels()
        {
            SetProgress("lbl-classic-progress", GameMode.Classic);
            SetProgress("lbl-timetrial-progress", GameMode.TimeTrial);
        }

        private void SetProgress(string labelName, GameMode mode)
        {
            Label label = rootElement?.Q<Label>(labelName);
            if (label == null) return;

            int level = Mathf.Max(1, GameStorage.GetInt(
                GameModeContext.KeyFor(mode, "WordPuzzle_CurrentLevel"), 1));

            if (mode == GameMode.TimeTrial)
            {
                float best = GameStorage.GetFloat(
                    GameModeContext.KeyFor(mode, "WordPuzzle_BestTime") + "_Lvl_" + level, 0f);

                label.text = best > 0f
                    ? $"LEVEL {level}   BEST {FormatTime(best)}"
                    : $"LEVEL {level}";
                return;
            }

            label.text = $"LEVEL {level}";
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }

        private void OnPlay()
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

            // Set before the reload: the model reads mode-scoped keys.
            AnalyticsService.ModeSelected(GameModeContext.DisplayName(_selected));
            GameModeContext.SetMode(_selected);
            if (_gameModel != null) _gameModel.ReloadForCurrentMode();

            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (config != null && ServiceLocator.Current.Has<UIManager>())
                ServiceLocator.Current.Get<UIManager>().HideOverlay(config);

            if (_gameManager != null) _gameManager.StartCurrentLevel();
        }

        private void OnBack(ClickEvent evt)
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

            if (config != null && ServiceLocator.Current.Has<UIManager>())
                ServiceLocator.Current.Get<UIManager>().HideOverlay(config);
        }
    }
}
