using UnityEngine.UIElements;
using ServiceLocatorFramework;
using DataBindingFramework;
using WordPuzzle.Models;
using WordPuzzle.Managers;
using WordPuzzle.Gameplay;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;
using WordPuzzle.Services;

namespace WordPuzzle.UI
{
    public class HUDView : BaseUI
    {
        private Label _levelLabel;
        private Label _chapterLabel;
        private Label _timerLabel;
        private VisualElement _timerPill;
        private Label _coinsLabel;
        private Label _wordPreviewLabel;
        private VisualElement _wordPreviewBox;
        private Button _hintButton;
        private Button _shuffleButton;
        private Button _pauseButton;

        private VisualElement _wordToast;
        private Label _toastWordLabel;
        private Label _toastNoteLabel;
        private IObserver<string> _matchedWordObserver;
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

        // A definition needs longer on screen than "BONUS WORD" does - roughly the time it
        // takes to read a short sentence, without stalling the next swipe.
        private const long ToastWithMeaningMs = 3200;

        private WordDefinitionService _definitions;
        private bool _warnedNoDefinitionService;
        private bool _warnedDefinitionsNotReady;
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
            _chapterLabel = rootElement.Q<Label>("lbl-chapter-title");
            _timerPill = rootElement.Q<VisualElement>("timer-pill");
            _timerLabel = rootElement.Q<Label>("lbl-timer");

            // Hidden outright in Classic rather than left at 0:00, which would read as a
            // stopped clock instead of "this mode has no clock".
            if (_timerPill != null)
            {
                _timerPill.style.display = GameModeContext.IsTimed ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
                    _gameModel.Coins.Bind(_bindingOwner, (coins) =>
                    {
                        _coinsLabel.text = coins.ToString();
                        RefreshHintAffordability(coins);
                    });
                    _coinsLabel.text = _gameModel.Coins.Value.ToString();
                }

                RefreshHintAffordability(_gameModel.Coins.Value);

                if (_levelLabel != null)
                {
                    _gameModel.CurrentLevelIndex.Bind(_bindingOwner, (lvl) => _levelLabel.text = $"Level {lvl}");
                    _levelLabel.text = $"Level {_gameModel.CurrentLevelIndex.Value}";
                }

                if (_timerLabel != null && GameModeContext.IsTimed)
                {
                    _gameModel.LevelSeconds.Bind(_bindingOwner, (t) => _timerLabel.text = ModeSelectView.FormatTime(t));
                    _timerLabel.text = ModeSelectView.FormatTime(_gameModel.LevelSeconds.Value);
                }

                if (_chapterLabel != null)
                {
                    _gameModel.CurrentChapterTitle.Bind(_bindingOwner, (title) => _chapterLabel.text = title ?? string.Empty);
                    _chapterLabel.text = _gameModel.CurrentChapterTitle.Value ?? string.Empty;
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

            if (_matchedWordObserver != null)
            {
                _matchedWordObserver.Unbind(OnWordMatched);
                _matchedWordObserver = null;
            }

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

            // Solving a target word raised no HUD feedback at all before, so the meaning had
            // nowhere to appear until the level ended.
            _matchedWordObserver = observerManager.GetOrCreateObserver<string>(WondersOfWordGameModel.OBS_WORD_MATCHED);
            _matchedWordObserver.Bind(_bindingOwner, OnWordMatched);

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
        /// <summary>A required word was solved - confirm it and teach what it means.</summary>
        private void OnWordMatched(string word)
        {
            ShowToast(word, MeaningNote(word, null), false);
        }

        /// <summary>
        /// The caption under the toast word: the dictionary meaning when there is one, else
        /// the plain fallback. Bonus words keep their coin reward in the caption, because the
        /// reward is the more important half of that message.
        /// </summary>
        private string MeaningNote(string word, string prefix)
        {
            if (_definitions == null && ServiceLocator.Current.Has<WordDefinitionService>())
                _definitions = ServiceLocator.Current.Get<WordDefinitionService>();

            // Logged once rather than silently degrading: an empty caption looks identical
            // whether the service is absent, still loading, or simply has no entry.
            if (_definitions == null)
            {
                if (!_warnedNoDefinitionService)
                {
                    _warnedNoDefinitionService = true;
                    UnityEngine.Debug.LogWarning(
                        "[HUDView] WordDefinitionService is not in the scene, so word meanings cannot show. " +
                        "Run WordPuzzle > Setup Wonders of Word Scene to add it.");
                }
                return string.IsNullOrEmpty(prefix) ? "WORD FOUND" : prefix;
            }

            if (!_definitions.IsReady && !_warnedDefinitionsNotReady)
            {
                _warnedDefinitionsNotReady = true;
                UnityEngine.Debug.LogWarning("[HUDView] Definitions are still loading - this word showed no meaning.");
            }

            string meaning = _definitions.GetPrimaryMeaning(word);

            if (string.IsNullOrEmpty(meaning))
            {
                return string.IsNullOrEmpty(prefix) ? "WORD FOUND" : prefix;
            }

            // WordNet glosses can run long; the toast is one or two lines, not a paragraph.
            if (meaning.Length > 110) meaning = meaning.Substring(0, 107).TrimEnd() + "...";

            return string.IsNullOrEmpty(prefix) ? meaning : $"{prefix}\n{meaning}";
        }

        private void OnBonusWordFound(string word)
        {
            // A bonus word pays coins, so the caption reads as a reward. "Not in this level"
            // described the grid but looked like a rejection for something that scored.
            ShowToast(word, MeaningNote(word, $"BONUS WORD  +{WondersOfWordGameModel.COINS_BONUS_WORD}"), false);
        }

        /// <summary>Word was already credited this level - acknowledge, do not reward.</summary>
        private void OnWordAlreadyFound(string word)
        {
            // Still worth the definition: re-swiping a word is often the player checking what
            // it meant. The neutral styling keeps it from reading as a second reward.
            ShowToast(word, MeaningNote(word, "ALREADY FOUND"), true);
        }

        private void ShowToast(string word, string note, bool neutral)
        {
            if (_wordToast == null || string.IsNullOrEmpty(word)) return;

            if (_toastWordLabel != null) _toastWordLabel.text = word.ToUpperInvariant();
            if (_toastNoteLabel != null) _toastNoteLabel.text = note;

            _wordToast.EnableInClassList(ToastNeutralClass, neutral);
            _wordToast.AddToClassList(ToastVisibleClass);

            // A note carrying a definition stays up longer than a one-line caption.
            long visibleMs = note != null && note.Length > 24 ? ToastWithMeaningMs : ToastVisibleMs;

            _toastHideTask?.Pause();
            _toastHideTask = _wordToast.schedule
                .Execute(() => _wordToast.RemoveFromClassList(ToastVisibleClass))
                .StartingIn(visibleMs);
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

        /// <summary>
        /// Dims the hint button once the player cannot pay for it, and shows how many hints
        /// the current purse buys. The old caption was the price (a fixed "20"), which read as
        /// a counter that never moved - spending coins now visibly costs you something.
        /// </summary>
        private void RefreshHintAffordability(int coins)
        {
            int remaining = coins / GameplayHandler.SingleTileHintCost;

            Label costLabel = rootElement?.Q<Label>("lbl-hint-cost");
            if (costLabel != null)
            {
                costLabel.text = remaining > 0 ? $"HINT · {remaining} LEFT" : "HINT · 0 LEFT";
            }

            if (_hintButton == null) return;
            _hintButton.EnableInClassList("action-btn--unaffordable", remaining <= 0);
        }

        private void OnHintClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

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
            HapticManager.Play(HapticType.Light);

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
            HapticManager.Play(HapticType.Light);

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
