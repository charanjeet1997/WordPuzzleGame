using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Managers;
using WordPuzzle.Audio;

namespace WordPuzzle.UI
{
    public class PauseOverlayView : BaseUI
    {
        private Button _resumeButton;
        private Button _soundButton;
        private Button _mainMenuButton;

        private GameManager _gameManager;
        private AudioManager _audioManager;

        protected override void OnInitialize()
        {
            if (ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();
            if (ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            // Match UXML names: btn-resume, btn-sound-toggle, btn-quit
            _resumeButton = rootElement.Q<Button>("btn-resume") ?? rootElement.Q<Button>("ResumeButton") ?? rootElement.Q<Button>(className: "btn-modal-primary");
            _soundButton = rootElement.Q<Button>("btn-sound-toggle") ?? rootElement.Q<Button>("SoundButton") ?? rootElement.Q<Button>(className: "btn-modal-secondary");
            _mainMenuButton = rootElement.Q<Button>("btn-quit") ?? rootElement.Q<Button>("MainMenuButton");

            if (_resumeButton != null) _resumeButton.clicked += OnResumeClicked;
            if (_soundButton != null) _soundButton.clicked += OnSoundClicked;
            if (_mainMenuButton != null) _mainMenuButton.clicked += OnMainMenuClicked;

            RefreshSoundLabel();
        }

        /// <summary>Keeps the caption in step with the saved setting when the overlay opens.</summary>
        private void RefreshSoundLabel()
        {
            if (_soundButton == null) return;

            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();

            bool on = _audioManager == null || _audioManager.SoundEnabled;
            _soundButton.text = on ? "SOUND: ON" : "SOUND: OFF";
        }

        protected override void OnHide()
        {
            if (_resumeButton != null) _resumeButton.clicked -= OnResumeClicked;
            if (_soundButton != null) _soundButton.clicked -= OnSoundClicked;
            if (_mainMenuButton != null) _mainMenuButton.clicked -= OnMainMenuClicked;
        }

        private void OnResumeClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            if (_gameManager != null) _gameManager.ResumeGame();
        }

        private void OnSoundClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();

            if (_audioManager == null) return;

            // Click first, then toggle: turning sound off would otherwise silence its own
            // confirmation and the tap would feel unresponsive.
            _audioManager.PlayButtonClickSound();
            _audioManager.ToggleSound();
            RefreshSoundLabel();
        }

        private void OnMainMenuClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            if (_gameManager != null) _gameManager.QuitToMainMenu();
        }
    }
}
