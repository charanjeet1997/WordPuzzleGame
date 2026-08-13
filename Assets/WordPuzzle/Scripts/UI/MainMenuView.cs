using UnityEngine.UIElements;
using ServiceLocatorFramework;
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
                _gameManager.StartCurrentLevel();
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
