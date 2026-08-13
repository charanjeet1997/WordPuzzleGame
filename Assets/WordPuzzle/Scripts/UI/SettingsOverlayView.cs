using UnityEngine;
using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Audio;
using WordPuzzle.Managers;
using WordPuzzle.Models;

namespace WordPuzzle.UI
{
    public class SettingsOverlayView : BaseUI
    {
        private VisualElement _soundIcon;
        private Label _soundLabel;
        private Button _soundToggle;
        private Button _resetButton;
        private Button _closeButton;

        private AudioManager _audioManager;
        private UIManager _uiManager;

        protected override void OnInitialize()
        {
            if (ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (ServiceLocator.Current.Has<UIManager>())
                _uiManager = ServiceLocator.Current.Get<UIManager>();
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            _soundIcon = rootElement.Q<VisualElement>("icon-sound");
            _soundLabel = rootElement.Q<Label>("lbl-sound-state");
            _soundToggle = rootElement.Q<Button>("btn-sound-toggle");
            _resetButton = rootElement.Q<Button>("btn-reset");
            _closeButton = rootElement.Q<Button>("btn-close");

            if (_soundToggle != null) _soundToggle.clicked += OnSoundToggled;
            if (_resetButton != null) _resetButton.clicked += OnResetClicked;
            if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

            RefreshSoundRow();
        }

        protected override void OnHide()
        {
            if (_soundToggle != null) _soundToggle.clicked -= OnSoundToggled;
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
            _audioManager.ToggleSound();
            RefreshSoundRow();
        }

        private void OnResetClicked()
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();

            PlayerPrefs.DeleteKey(WondersOfWordGameModel.KEY_CURRENT_LEVEL_INDEX);
            PlayerPrefs.Save();

            if (ServiceLocator.Current.Has<WondersOfWordGameModel>())
            {
                ServiceLocator.Current.Get<WondersOfWordGameModel>().CurrentLevelIndex.Value = 1;
            }
        }

        private void OnCloseClicked()
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            if (_uiManager == null && ServiceLocator.Current.Has<UIManager>())
                _uiManager = ServiceLocator.Current.Get<UIManager>();

            if (_uiManager != null && config != null) _uiManager.HideOverlay(config);
        }
    }
}
