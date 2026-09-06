using UnityEngine.UIElements;
using ServiceLocatorFramework;
using DataBindingFramework;
using WordPuzzle.Models;
using WordPuzzle.Data;
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
        private Button _wordsButton;
        private Button _wordsCloseButton;
        private Label _wordsCountLabel;
        private VisualElement _wordsPanel;
        private VisualElement _wordsPanelList;

        private VisualElement _wordToast;
        private Button _toastCloseButton;
        private Label _toastWordLabel;
        private Label _toastStatusLabel;
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
        private const string WordsPanelOpenClass = "words-panel--open";
        private const long ErrorFlashMs = 600;
        private const long ToastVisibleMs = 1400;

        // A definition needs longer on screen than "BONUS WORD" does - roughly the time it
        // takes to read a short sentence, without stalling the next swipe.
        private const long ToastWithMeaningMs = 6000;

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

            _wordsButton = rootElement.Q<Button>("btn-words");
            _wordsCloseButton = rootElement.Q<Button>("btn-words-close");
            _wordsCountLabel = rootElement.Q<Label>("lbl-words-count");
            _wordsPanel = rootElement.Q<VisualElement>("words-panel");
            _wordsPanelList = rootElement.Q<VisualElement>("words-panel-list");

            // Closed on every show: the panel is part of the shared document, so without this
            // it would still be open from the previous level.
            _wordsPanel?.RemoveFromClassList(WordsPanelOpenClass);

            _wordToast = rootElement.Q<VisualElement>("word-toast");
            _toastCloseButton = rootElement.Q<Button>("btn-toast-close");
            _toastWordLabel = rootElement.Q<Label>("lbl-toast-word");
            _toastStatusLabel = rootElement.Q<Label>("lbl-toast-status");
            _toastNoteLabel = rootElement.Q<Label>("lbl-toast-note");
            if (_wordToast != null) _wordToast.RemoveFromClassList(ToastVisibleClass);

            BindBonusWordObserver();

            if (_hintButton != null) _hintButton.clicked += OnHintClicked;
            if (_shuffleButton != null) _shuffleButton.clicked += OnShuffleClicked;
            if (_pauseButton != null) _pauseButton.clicked += OnPauseClicked;
            if (_wordsButton != null) _wordsButton.clicked += OnWordsClicked;
            if (_wordsCloseButton != null) _wordsCloseButton.clicked += OnWordsCloseClicked;
            if (_toastCloseButton != null) _toastCloseButton.clicked += OnToastCloseClicked;

            RefreshWordsCount();

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

                // Bound rather than set once, so the caption counts up as words are solved
                // instead of showing the count the level started with.
                _gameModel.SolvedWordsCount.Bind(_bindingOwner, (_) => RefreshWordsCount());
                _gameModel.TargetWordsTotal.Bind(_bindingOwner, (_) => RefreshWordsCount());

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
                    _gameModel.CurrentWordPreview.Bind(_bindingOwner, (preview) =>
                    {
                        _wordPreviewLabel.text = preview ?? string.Empty;
                        if (_wordPreviewBox != null)
                        {
                            _wordPreviewBox.style.display = string.IsNullOrEmpty(preview) ? DisplayStyle.None : DisplayStyle.Flex;
                        }
                    });
                    string currentPreview = _gameModel.CurrentWordPreview.Value;
                    _wordPreviewLabel.text = currentPreview ?? string.Empty;
                    if (_wordPreviewBox != null)
                    {
                        _wordPreviewBox.style.display = string.IsNullOrEmpty(currentPreview) ? DisplayStyle.None : DisplayStyle.Flex;
                    }
                }
            }
        }

        protected override void OnHide()
        {
            if (_hintButton != null) _hintButton.clicked -= OnHintClicked;
            if (_shuffleButton != null) _shuffleButton.clicked -= OnShuffleClicked;
            if (_pauseButton != null) _pauseButton.clicked -= OnPauseClicked;
            if (_wordsButton != null) _wordsButton.clicked -= OnWordsClicked;
            if (_wordsCloseButton != null) _wordsCloseButton.clicked -= OnWordsCloseClicked;
            if (_toastCloseButton != null) _toastCloseButton.clicked -= OnToastCloseClicked;

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

        private void OnToastCloseClicked()
        {
            _toastHideTask?.Pause();
            _toastHideTask = null;
            _wordToast?.RemoveFromClassList(ToastVisibleClass);
            _audioManager?.PlayButtonClickSound();
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
                        "Run Aurora Words > Setup Scene to add it.");
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

            // WordNet glosses can run long, and a cut mid-sentence is worse than no definition
            // at all - "who purchases securities in one market for immediate resale in..." tells
            // the player nothing. The toast is sized for a couple of lines now, so the cap is
            // generous enough that most glosses survive whole.
            if (meaning.Length > 220) meaning = meaning.Substring(0, 217).TrimEnd() + "...";

            return string.IsNullOrEmpty(prefix) ? meaning : $"{prefix}\n{meaning}";
        }

        /// <summary>
        /// Splits what <see cref="MeaningNote"/> produced back into its status caption and the
        /// meaning, so each can carry its own type. The two arrive joined because the note is
        /// also used where only one line is wanted.
        /// </summary>
        private static void SplitNote(string note, out string status, out string meaning)
        {
            status = null;
            meaning = note;

            if (string.IsNullOrEmpty(note)) return;

            int newline = note.IndexOf('\n');
            if (newline < 0)
            {
                // A bare caption with no definition behind it - "WORD FOUND", "ALREADY FOUND".
                // Those are statuses, not meanings, so they belong on the status line.
                bool isCaption = note.Length <= 24 && note.ToUpperInvariant() == note;
                if (isCaption)
                {
                    status = note;
                    meaning = null;
                }
                return;
            }

            status = note.Substring(0, newline);
            meaning = note.Substring(newline + 1);
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

        /// <summary>
        /// Rebuilds and opens the word panel. Built on open rather than kept in sync, because
        /// it is only ever read while it is on screen and a stale list would be worse than a
        /// momentary rebuild.
        /// </summary>
        private void OnWordsClicked()
        {
            if (_wordsPanel == null) return;

            BuildWordsPanel();
            _wordsPanel.AddToClassList(WordsPanelOpenClass);
            _audioManager?.PlayButtonClickSound();
        }

        private void OnWordsCloseClicked()
        {
            _wordsPanel?.RemoveFromClassList(WordsPanelOpenClass);
            _audioManager?.PlayButtonClickSound();
        }

        /// <summary>
        /// One chip per target word: solved ones spelled out, the rest as one dash per letter
        /// so the panel says how many are left and how long they are without giving the answer
        /// away - that is what the hint button is for. Bonus words already found follow, since
        /// they are not part of the level's target set.
        /// </summary>
        private void BuildWordsPanel()
        {
            if (_wordsPanelList == null) return;

            _wordsPanelList.Clear();

            LevelData level = _gameManager?.GetCurrentLevelData();
            if (level?.targetWords == null) return;

            foreach (TargetWordEntry entry in level.targetWords)
            {
                if (entry == null || string.IsNullOrEmpty(entry.word)) continue;

                string word = entry.word.ToUpperInvariant();
                bool found = _gameModel != null && _gameModel.SolvedTargetWords.Contains(word);
                _wordsPanelList.Add(BuildWordRow(word, found, false));
            }

            if (_gameModel == null || _gameModel.FoundBonusWords.Count == 0) return;

            var subhead = new Label("BONUS WORDS");
            subhead.AddToClassList("words-panel-subhead");
            _wordsPanelList.Add(subhead);

            foreach (string bonus in _gameModel.FoundBonusWords)
            {
                _wordsPanelList.Add(BuildWordRow(bonus.ToUpperInvariant(), true, true));
            }
        }

        /// <summary>
        /// One row per word: the word, its part of speech, and its meaning. An unsolved word
        /// shows dashes and no meaning - the definition would name the word outright, which is
        /// what the hint button is for.
        /// </summary>
        private VisualElement BuildWordRow(string word, bool found, bool bonus)
        {
            if (_definitions == null && ServiceLocator.Current.Has<WordDefinitionService>())
                _definitions = ServiceLocator.Current.Get<WordDefinitionService>();

            var row = new VisualElement();
            row.AddToClassList("word-row");
            if (!found) row.AddToClassList("word-row--hidden");

            var wordLabel = new Label(found ? word : new string('-', word.Length));
            wordLabel.AddToClassList("word-row-word");
            if (bonus) wordLabel.AddToClassList("word-row-word--bonus");
            row.Add(wordLabel);

            if (!found || _definitions == null || !_definitions.IsReady) return row;

            string pos = _definitions.GetPrimaryPartOfSpeech(word);
            if (!string.IsNullOrEmpty(pos))
            {
                var posLabel = new Label(pos);
                posLabel.AddToClassList("word-row-pos");
                row.Add(posLabel);
            }

            string meaning = _definitions.GetPrimaryMeaning(word);
            if (!string.IsNullOrEmpty(meaning))
            {
                var meaningLabel = new Label(meaning);
                meaningLabel.AddToClassList("word-row-text");
                row.Add(meaningLabel);
            }

            return row;
        }

        /// <summary>Caption under the WORDS button: how many of the level's words are solved.</summary>
        private void RefreshWordsCount()
        {
            if (_wordsCountLabel == null || _gameModel == null) return;

            _wordsCountLabel.text =
                $"WORDS ({_gameModel.SolvedWordsCount.Value}/{_gameModel.TargetWordsTotal.Value})";
        }

        private void ShowToast(string word, string note, bool neutral)
        {
            if (_wordToast == null || string.IsNullOrEmpty(word)) return;

            SplitNote(note, out string status, out string meaning);

            if (_toastWordLabel != null) _toastWordLabel.text = word.ToUpperInvariant();

            // Hidden rather than blanked: an empty label still takes its margins, which left a
            // gap under the word on every toast that carried no status.
            if (_toastStatusLabel != null)
            {
                _toastStatusLabel.text = status ?? string.Empty;
                _toastStatusLabel.style.display =
                    string.IsNullOrEmpty(status) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_toastNoteLabel != null)
            {
                _toastNoteLabel.text = meaning ?? string.Empty;
                _toastNoteLabel.style.display =
                    string.IsNullOrEmpty(meaning) ? DisplayStyle.None : DisplayStyle.Flex;
            }

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
                costLabel.text = remaining > 0 ? $"HINT ({remaining})" : "HINT (0)";
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
