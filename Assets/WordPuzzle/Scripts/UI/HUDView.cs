using UnityEngine.UIElements;
using ServiceLocatorFramework;
using DataBindingFramework;
using WordPuzzle.Models;
using WordPuzzle.Managers;
using WordPuzzle.Gameplay;
using WordPuzzle.Audio;

namespace WordPuzzle.UI
{
    public class HUDView : BaseUI
    {
        private Label _levelLabel;
        private Label _coinsLabel;
        private Label _wordPreviewLabel;
        private VisualElement _wordPreviewBox;
        private Button _hintButton;
        private Button _shuffleButton;
        private Button _pauseButton;

        private VisualElement _wordToast;
        private Label _toastWordLabel;
        private Label _toastNoteLabel;
        private IObserver<string> _bonusWordObserver;
        private IObserver<string> _wrongWordObserver;
        private IObserver<string> _repeatWordObserver;
        private IVisualElementScheduledItem _errorClearTask;
        private IVisualElementScheduledItem _toastHideTask;

        private const string ToastVisibleClass = "word-toast--visible";
        private const string ToastNeutralClass = "word-toast--neutral";
        private const string PreviewErrorClass = "word-preview-box--error";
        private const string PreviewTextErrorClass = "word-preview-text--error";
        private const long ErrorFlashMs = 600;
        private const long ToastVisibleMs = 1400;

        private WondersOfWordGameModel _gameModel;
        private GameManager _gameManager;
        private GameplayHandler _gameplayHandler;
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

            // Match UXML names: btn-pause, btn-shuffle, btn-hint, lbl-level-number, lbl-coins, lbl-word-preview
            _levelLabel = rootElement.Q<Label>("lbl-level-number") ?? rootElement.Q<Label>("LevelLabel") ?? rootElement.Q<Label>(className: "hud-level-text");
            _coinsLabel = rootElement.Q<Label>("lbl-coins") ?? rootElement.Q<Label>("CoinsLabel") ?? rootElement.Q<Label>(className: "coin-text");
            _wordPreviewBox = rootElement.Q<VisualElement>(className: "word-preview-box");
            _wordPreviewLabel = rootElement.Q<Label>("lbl-word-preview") ?? rootElement.Q<Label>("WordPreviewLabel") ?? rootElement.Q<Label>(className: "word-preview-text");
            _hintButton = rootElement.Q<Button>("btn-hint") ?? rootElement.Q<Button>("HintButton") ?? rootElement.Q<Button>(className: "hint-color");
            _shuffleButton = rootElement.Q<Button>("btn-shuffle") ?? rootElement.Q<Button>("ShuffleButton") ?? rootElement.Q<Button>(className: "action-btn-pill");
            _pauseButton = rootElement.Q<Button>("btn-pause") ?? rootElement.Q<Button>("PauseButton") ?? rootElement.Q<Button>(className: "icon-btn-round");

            _wordToast = rootElement.Q<VisualElement>("word-toast");
            _toastWordLabel = rootElement.Q<Label>("lbl-toast-word");
            _toastNoteLabel = rootElement.Q<Label>("lbl-toast-note");
            if (_wordToast != null) _wordToast.RemoveFromClassList(ToastVisibleClass);

            BindBonusWordObserver();

            if (_hintButton != null) _hintButton.clicked += OnHintClicked;
            if (_shuffleButton != null) _shuffleButton.clicked += OnShuffleClicked;
            if (_pauseButton != null) _pauseButton.clicked += OnPauseClicked;

            if (_gameModel != null)
            {
                if (_coinsLabel != null)
                {
                    _gameModel.Coins.Bind(_bindingOwner, (coins) => _coinsLabel.text = coins.ToString());
                    _coinsLabel.text = _gameModel.Coins.Value.ToString();
                }

                if (_levelLabel != null)
                {
                    _gameModel.CurrentLevelIndex.Bind(_bindingOwner, (lvl) => _levelLabel.text = $"Level {lvl}");
                    _levelLabel.text = $"Level {_gameModel.CurrentLevelIndex.Value}";
                }

                if (_wordPreviewLabel != null)
                {
                    _gameModel.CurrentWordPreview.Bind(_bindingOwner, (preview) => _wordPreviewLabel.text = preview);
                    _wordPreviewLabel.text = _gameModel.CurrentWordPreview.Value;
                }
            }
        }

        protected override void OnHide()
        {
            if (_hintButton != null) _hintButton.clicked -= OnHintClicked;
            if (_shuffleButton != null) _shuffleButton.clicked -= OnShuffleClicked;
            if (_pauseButton != null) _pauseButton.clicked -= OnPauseClicked;

            if (_bonusWordObserver != null)
            {
                _bonusWordObserver.Unbind(OnBonusWordFound);
                _bonusWordObserver = null;
            }

            if (_wrongWordObserver != null)
            {
                _wrongWordObserver.Unbind(OnWrongWord);
                _wrongWordObserver = null;
            }

            if (_repeatWordObserver != null)
            {
                _repeatWordObserver.Unbind(OnWordAlreadyFound);
                _repeatWordObserver = null;
            }

            _errorClearTask?.Pause();
            _errorClearTask = null;

            _toastHideTask?.Pause();
            _toastHideTask = null;
        }

        private void BindBonusWordObserver()
        {
            if (_bonusWordObserver != null) return;
            if (!ServiceLocator.Current.Has<IObserverManager>()) return;

            var observerManager = ServiceLocator.Current.Get<IObserverManager>();
            _bonusWordObserver = observerManager.GetOrCreateObserver<string>(WondersOfWordGameModel.OBS_BONUS_WORD_FOUND);
            _bonusWordObserver.Bind(_bindingOwner, OnBonusWordFound);

            _wrongWordObserver = observerManager.GetOrCreateObserver<string>(WondersOfWordGameModel.OBS_WRONG_WORD);
            _wrongWordObserver.Bind(_bindingOwner, OnWrongWord);

            _repeatWordObserver = observerManager.GetOrCreateObserver<string>(WondersOfWordGameModel.OBS_WORD_ALREADY_FOUND);
            _repeatWordObserver.Bind(_bindingOwner, OnWordAlreadyFound);
        }

        /// <summary>
        /// A real word that this level does not ask for - surface it briefly instead of
        /// letting the swipe read as a plain failure.
        /// </summary>
        private void OnBonusWordFound(string word)
        {
            // A bonus word pays coins, so the caption reads as a reward. "Not in this level"
            // described the grid but looked like a rejection for something that scored.
            ShowToast(word, $"BONUS WORD  +{WondersOfWordGameModel.COINS_BONUS_WORD}", false);
        }

        /// <summary>Word was already credited this level - acknowledge, do not reward.</summary>
        private void OnWordAlreadyFound(string word)
        {
            ShowToast(word, "ALREADY FOUND", true);
        }

        private void ShowToast(string word, string note, bool neutral)
        {
            if (_wordToast == null || string.IsNullOrEmpty(word)) return;

            if (_toastWordLabel != null) _toastWordLabel.text = word.ToUpperInvariant();
            if (_toastNoteLabel != null) _toastNoteLabel.text = note;

            _wordToast.EnableInClassList(ToastNeutralClass, neutral);
            _wordToast.AddToClassList(ToastVisibleClass);

            _toastHideTask?.Pause();
            _toastHideTask = _wordToast.schedule
                .Execute(() => _wordToast.RemoveFromClassList(ToastVisibleClass))
                .StartingIn(ToastVisibleMs);
        }

        /// <summary>
        /// A word the dictionary does not contain. Nothing listened to this before, so a
        /// rejected swipe gave no visual feedback at all - it read as unresponsive input.
        /// </summary>
        private void OnWrongWord(string word)
        {
            if (_wordPreviewBox == null) return;

            _wordPreviewBox.AddToClassList(PreviewErrorClass);
            _wordPreviewLabel?.AddToClassList(PreviewTextErrorClass);

            _errorClearTask?.Pause();
            _errorClearTask = _wordPreviewBox.schedule.Execute(() =>
            {
                _wordPreviewBox.RemoveFromClassList(PreviewErrorClass);
                _wordPreviewLabel?.RemoveFromClassList(PreviewTextErrorClass);
            }).StartingIn(ErrorFlashMs);
        }

        private void OnHintClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_audioManager != null) _audioManager.PlayButtonClickSound();

            if (_gameplayHandler == null && ServiceLocator.Current.Has<GameplayHandler>())
            {
                _gameplayHandler = ServiceLocator.Current.Get<GameplayHandler>();
            }

            if (_gameplayHandler != null)
            {
                _gameplayHandler.UseSingleTileHint();
            }
        }

        private void OnShuffleClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_audioManager != null) _audioManager.PlayButtonClickSound();

            if (_gameplayHandler == null && ServiceLocator.Current.Has<GameplayHandler>())
            {
                _gameplayHandler = ServiceLocator.Current.Get<GameplayHandler>();
            }
            if (_gameplayHandler != null)
            {
                _gameplayHandler.ShuffleWheel();
            }
        }

        private void OnPauseClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_audioManager != null) _audioManager.PlayButtonClickSound();

            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
            {
                _gameManager = ServiceLocator.Current.Get<GameManager>();
            }
            if (_gameManager != null)
            {
                _gameManager.PauseGame();
            }
        }
    }
}
