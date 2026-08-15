using UnityEngine;
using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Models;
using WordPuzzle.Managers;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;

namespace WordPuzzle.UI
{
    public class MainMenuView : BaseUI
    {
        private Button _playButton;
        private Button _settingsButton;
        private Button _soundButton;
        private Label _titleLabel;
        private Label _coinsLabel;
        private Label _chapterLabel;
        private Label _chapterNameLabel;
        private Label _levelLabel;
        private Label _wordCountLabel;
        private VisualElement _progressDots;

        /// <summary>Levels per chapter, matching how the generator stamps chapterTitle.</summary>
        private const int LevelsPerChapter = 20;

        private WondersOfWordGameModel _gameModel;
        private GameManager _gameManager;
        private AudioManager _audioManager;
        private UnityEngine.Object _bindingOwner;

        protected override void OnInitialize()
        {
            _bindingOwner = this;
            if (ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();
            if (ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();
            if (ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            // Match UXML names: btn-play, btn-settings, btn-sound
            _playButton = rootElement.Q<Button>("btn-play") ?? rootElement.Q<Button>("PlayButton") ?? rootElement.Q<Button>(className: "btn-primary") ?? rootElement.Q<Button>();
            _settingsButton = rootElement.Q<Button>("btn-settings") ?? rootElement.Q<Button>("SettingsButton");
            _soundButton = rootElement.Q<Button>("btn-sound") ?? rootElement.Q<Button>("SoundButton");
            
            _titleLabel = rootElement.Q<Label>("TitleLabel") ?? rootElement.Q<Label>(className: "game-title");
            _chapterLabel = rootElement.Q<Label>("lbl-chapter-info");
            _chapterNameLabel = rootElement.Q<Label>("lbl-chapter-name");
            _levelLabel = rootElement.Q<Label>("lbl-level-number");
            _wordCountLabel = rootElement.Q<Label>("lbl-word-count");
            _progressDots = rootElement.Q<VisualElement>("progress-dots");
            _coinsLabel = rootElement.Q<Label>("CoinsLabel") ?? rootElement.Q<Label>(className: "coins-text");

            if (_playButton != null)
            {
                _playButton.clicked += OnPlayClicked;
            }

            if (_soundButton != null)
            {
                _soundButton.clicked += OnSoundClicked;
            }

            if (_settingsButton != null)
            {
                _settingsButton.clicked += OnSettingsClicked;
            }

            RefreshSoundIcon();
            RefreshChapterCard();

            if (_gameModel != null && _coinsLabel != null)
            {
                _gameModel.Coins.Bind(_bindingOwner, (coins) => _coinsLabel.text = coins.ToString());
                _coinsLabel.text = _gameModel.Coins.Value.ToString();
            }
        }

        protected override void OnHide()
        {
            if (_playButton != null)
            {
                _playButton.clicked -= OnPlayClicked;
            }

            if (_soundButton != null)
            {
                _soundButton.clicked -= OnSoundClicked;
            }

            if (_settingsButton != null)
            {
                _settingsButton.clicked -= OnSettingsClicked;
            }
        }

        private void OnPlayClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);
            if (_gameManager != null)
            {
                _gameManager.ShowModeSelect();
            }
        }

        /// <summary>
        /// Fills the menu card from the level the player is actually on. Every field here was
        /// hardcoded placeholder text before, so the card never moved off "CHAPTER 1 / LEVEL 1".
        /// </summary>
        private void RefreshChapterCard()
        {
            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();
            if (_gameModel == null && ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();

            int level = _gameModel != null ? _gameModel.CurrentLevelIndex.Value : 1;
            int chapter = ((level - 1) / LevelsPerChapter) + 1;
            int levelInChapter = ((level - 1) % LevelsPerChapter) + 1;

            if (_chapterLabel != null) _chapterLabel.text = $"CHAPTER {chapter}";
            if (_levelLabel != null) _levelLabel.text = $"LEVEL {level}";

            LevelData data = _gameManager != null ? _gameManager.GetCurrentLevelData() : null;

            if (_wordCountLabel != null)
            {
                int words = data != null && data.targetWords != null ? data.targetWords.Count : 0;
                _wordCountLabel.text = words > 0 ? $"{words} WORDS" : string.Empty;
            }

            // The generator stamps chapterTitle as plain "Chapter N" with no flavour name, so
            // fall back to the wheel letters rather than printing the chapter number twice.
            if (_chapterNameLabel != null)
            {
                string name = null;
                if (data != null)
                {
                    int dash = data.chapterTitle != null ? data.chapterTitle.IndexOf(" - ") : -1;
                    if (dash >= 0) name = data.chapterTitle.Substring(dash + 3);
                    else if (!string.IsNullOrEmpty(data.wheelLetters)) name = data.wheelLetters;
                }
                _chapterNameLabel.text = string.IsNullOrEmpty(name) ? "Ready to play" : name;
            }

            RefreshProgressDots(levelInChapter);
        }

        /// <summary>Fills dots in proportion to progress through the current chapter.</summary>
        private void RefreshProgressDots(int levelInChapter)
        {
            if (_progressDots == null) return;

            int total = _progressDots.childCount;
            if (total == 0) return;

            int filled = Mathf.Clamp(
                Mathf.CeilToInt(levelInChapter / (float)LevelsPerChapter * total), 1, total);

            for (int i = 0; i < total; i++)
            {
                _progressDots[i].EnableInClassList("dot--on", i < filled);
            }
        }

        private void OnSettingsClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);
            if (_gameManager != null) _gameManager.ShowSettings();
        }

        private void OnSoundClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();

            if (_audioManager == null) return;

            // Click first, then toggle, so switching sound off still confirms the tap.
            _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);
            _audioManager.ToggleSound();
            RefreshSoundIcon();
        }

        /// <summary>Swaps the speaker art between the on and crossed-out off icons.</summary>
        private void RefreshSoundIcon()
        {
            if (_soundButton == null) return;

            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();

            bool on = _audioManager == null || _audioManager.SoundEnabled;

            VisualElement icon = _soundButton.Q<VisualElement>("icon-sound");
            if (icon != null)
            {
                icon.EnableInClassList("icon-sound-on", on);
                icon.EnableInClassList("icon-sound-off", !on);
            }
        }
    }
}
