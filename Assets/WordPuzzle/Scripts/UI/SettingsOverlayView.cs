using UnityEngine;
using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;
using WordPuzzle.Managers;
using WordPuzzle.Models;

namespace WordPuzzle.UI
{
    public class SettingsOverlayView : BaseUI
    {
        /// <summary>Matches the default the model loads coins with on a first run.</summary>
        private const int StartingCoins = 100;

        private VisualElement _soundIcon;
        private Label _soundLabel;
        private Button _soundToggle;
        private VisualElement _musicIcon;
        private Label _musicLabel;
        private Button _musicToggle;
        private VisualElement _vibrationIcon;
        private Label _vibrationLabel;
        private Button _vibrationToggle;
        private Button _resetButton;
        private Button _closeButton;

        private AudioManager _audioManager;
        private MusicPlayer _musicPlayer;
        private UIManager _uiManager;

        protected override void OnInitialize()
        {
            if (ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (ServiceLocator.Current.Has<MusicPlayer>())
                _musicPlayer = ServiceLocator.Current.Get<MusicPlayer>();
            if (ServiceLocator.Current.Has<UIManager>())
                _uiManager = ServiceLocator.Current.Get<UIManager>();
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            _soundIcon = rootElement.Q<VisualElement>("icon-sound");
            _soundLabel = rootElement.Q<Label>("lbl-sound-state");
            _soundToggle = rootElement.Q<Button>("btn-sound-toggle");
            _musicIcon = rootElement.Q<VisualElement>("icon-music");
            _musicLabel = rootElement.Q<Label>("lbl-music-state");
            _musicToggle = rootElement.Q<Button>("btn-music-toggle");
            _vibrationIcon = rootElement.Q<VisualElement>("icon-vibration");
            _vibrationLabel = rootElement.Q<Label>("lbl-vibration-state");
            _vibrationToggle = rootElement.Q<Button>("btn-vibration-toggle");
            _resetButton = rootElement.Q<Button>("btn-reset");
            _closeButton = rootElement.Q<Button>("btn-close");

            if (_soundToggle != null) _soundToggle.clicked += OnSoundToggled;
            if (_musicToggle != null) _musicToggle.clicked += OnMusicToggled;
            if (_vibrationToggle != null) _vibrationToggle.clicked += OnVibrationToggled;
            if (_resetButton != null) _resetButton.clicked += OnResetClicked;
            if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

            // Desktop and the Editor have no motor, so the row would offer a switch that
            // changes nothing observable. HapticManager.IsSupported exists for exactly this.
            VisualElement vibrationRow = rootElement.Q<VisualElement>("row-vibration");
            if (vibrationRow != null)
            {
                vibrationRow.style.display =
                    HapticManager.IsSupported ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RefreshSoundRow();
            RefreshMusicRow();
            RefreshVibrationRow();
        }

        protected override void OnHide()
        {
            if (_soundToggle != null) _soundToggle.clicked -= OnSoundToggled;
            if (_musicToggle != null) _musicToggle.clicked -= OnMusicToggled;
            if (_vibrationToggle != null) _vibrationToggle.clicked -= OnVibrationToggled;
            if (_resetButton != null) _resetButton.clicked -= OnResetClicked;
            if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
        }

        /// <summary>Icon, caption and switch all read from the one source of truth.</summary>
        private void RefreshSoundRow()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();

            bool on = _audioManager == null || _audioManager.SoundEnabled;

            if (_soundIcon != null)
            {
                _soundIcon.EnableInClassList("icon-sound-on", on);
                _soundIcon.EnableInClassList("icon-sound-off", !on);
            }
            if (_soundLabel != null) _soundLabel.text = on ? "SOUND: ON" : "SOUND: OFF";
            if (_soundToggle != null)
            {
                _soundToggle.text = on ? "ON" : "OFF";
                _soundToggle.EnableInClassList("settings-toggle--off", !on);
            }
        }

        private void OnSoundToggled()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_audioManager == null) return;

            // Click first, then toggle, so switching sound off still confirms the tap.
            _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);
            _audioManager.ToggleSound();
            RefreshSoundRow();
        }

        private void RefreshMusicRow()
        {
            if (_musicPlayer == null && ServiceLocator.Current.Has<MusicPlayer>())
                _musicPlayer = ServiceLocator.Current.Get<MusicPlayer>();

            bool on = _musicPlayer == null || _musicPlayer.MusicEnabled;

            if (_musicIcon != null)
            {
                _musicIcon.EnableInClassList("icon-music-on", on);
                _musicIcon.EnableInClassList("icon-music-off", !on);
            }
            if (_musicLabel != null) _musicLabel.text = on ? "MUSIC: ON" : "MUSIC: OFF";
            if (_musicToggle != null)
            {
                _musicToggle.text = on ? "ON" : "OFF";
                _musicToggle.EnableInClassList("settings-toggle--off", !on);
            }
        }

        private void OnMusicToggled()
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

            if (_musicPlayer == null && ServiceLocator.Current.Has<MusicPlayer>())
                _musicPlayer = ServiceLocator.Current.Get<MusicPlayer>();
            if (_musicPlayer == null) return;

            _musicPlayer.ToggleMusic();
            RefreshMusicRow();
        }

        private void RefreshVibrationRow()
        {
            bool on = HapticManager.Enabled;

            if (_vibrationIcon != null)
            {
                _vibrationIcon.EnableInClassList("icon-vibration-on", on);
                _vibrationIcon.EnableInClassList("icon-vibration-off", !on);
            }
            if (_vibrationLabel != null) _vibrationLabel.text = on ? "VIBRATION: ON" : "VIBRATION: OFF";
            if (_vibrationToggle != null)
            {
                _vibrationToggle.text = on ? "ON" : "OFF";
                _vibrationToggle.EnableInClassList("settings-toggle--off", !on);
            }
        }

        private void OnVibrationToggled()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_audioManager != null) _audioManager.PlayButtonClickSound();

            // No tap fired here on purpose: SetEnabled plays the confirmation buzz after
            // switching on, and firing one first would both buzz while turning it off and
            // get swallowed by the repeat throttle.
            HapticManager.Toggle();
            RefreshVibrationRow();
        }

        private void OnResetClicked()
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

            if (ServiceLocator.Current.Has<WordPuzzle.Services.IProgressionService>())
            {
                ServiceLocator.Current.Get<WordPuzzle.Services.IProgressionService>().ResetAllProgress();
            }

            PlayerPrefs.DeleteKey(WondersOfWordGameModel.KEY_CURRENT_LEVEL_INDEX);
            PlayerPrefs.SetInt(WondersOfWordGameModel.KEY_COINS, StartingCoins);
            PlayerPrefs.Save();

            if (ServiceLocator.Current.Has<WondersOfWordGameModel>())
            {
                var model = ServiceLocator.Current.Get<WondersOfWordGameModel>();
                model.CurrentLevelIndex.Value = 1;
                model.Coins.Value = StartingCoins;
            }
        }

        private void OnCloseClicked()
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);
            if (_uiManager == null && ServiceLocator.Current.Has<UIManager>())
                _uiManager = ServiceLocator.Current.Get<UIManager>();

            if (_uiManager != null && config != null) _uiManager.HideOverlay(config);
        }
    }
}
