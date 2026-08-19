using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Models;
using WordPuzzle.Managers;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;
using WordPuzzle.Services;

namespace WordPuzzle.UI
{
    public class LevelCompleteView : BaseUI
    {
        private Button _nextLevelButton;
        private Label _titleLabel;
        private Label _coinsEarnedLabel;
        private Label _scoreLabel;
        private Label _mainMenuLabel;
        private VisualElement _meaningsList;
        private Label _timeLabel;
        private WordDefinitionService _definitions;

        private const string CardShownClass = "victory-card--shown";
        private const string StarShownClass = "star--shown";

        private WondersOfWordGameModel _gameModel;
        private GameManager _gameManager;
        private AudioManager _audioManager;

        protected override void OnInitialize()
        {
            if (ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();
            if (ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();
            if (ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (ServiceLocator.Current.Has<WordDefinitionService>())
                _definitions = ServiceLocator.Current.Get<WordDefinitionService>();
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            // Match UXML name: btn-next-level
            _nextLevelButton = rootElement.Q<Button>("btn-next-level") ?? rootElement.Q<Button>("NextLevelButton") ?? rootElement.Q<Button>(className: "btn-victory-primary");
            _titleLabel = rootElement.Q<Label>("TitleLabel") ?? rootElement.Q<Label>(className: "victory-title");
            _coinsEarnedLabel = rootElement.Q<Label>("lbl-reward") ?? rootElement.Q<Label>("CoinsEarnedLabel") ?? rootElement.Q<Label>(className: "reward-text");
            _scoreLabel = rootElement.Q<Label>("lbl-score");

            // A Label, not a Button, so it needs an explicit click handler - it looked
            // tappable and did nothing.
            _mainMenuLabel = rootElement.Q<Label>("lbl-subtitle");
            _meaningsList = rootElement.Q<VisualElement>("meanings-list");
            _timeLabel = rootElement.Q<Label>("lbl-time");

            if (_nextLevelButton != null)
            {
                _nextLevelButton.clicked += OnNextLevelClicked;
            }

            if (_mainMenuLabel != null)
            {
                _mainMenuLabel.RegisterCallback<ClickEvent>(OnMainMenuClicked);
            }

            if (_coinsEarnedLabel != null)
            {
                _coinsEarnedLabel.text = "+50";
            }

            RefreshResult();
            RefreshTime();
            BuildMeaningsList();
            RefreshOnboardingTip();
        }

        /// <summary>
        /// Shows the clear time in Time Trial, and calls out a new record. Hidden entirely in
        /// Classic, where there is no clock to report.
        /// </summary>
        private void RefreshTime()
        {
            if (_timeLabel == null) return;

            if (!GameModeContext.IsTimed || _gameModel == null)
            {
                _timeLabel.style.display = DisplayStyle.None;
                return;
            }

            _timeLabel.style.display = DisplayStyle.Flex;

            float seconds = _gameModel.LevelSeconds.Value;
            float best = 0f;
            if (ServiceLocator.Current.Has<IProgressionService>())
            {
                best = ServiceLocator.Current.Get<IProgressionService>()
                    .GetBestTime(_gameModel.CurrentLevelIndex.Value);
            }

            // The time was submitted before this screen opened, so an equal best means the run
            // just set it.
            bool isRecord = best > 0f && seconds <= best + 0.01f;

            _timeLabel.text = isRecord
                ? $"TIME: {ModeSelectView.FormatTime(seconds)}  -  NEW BEST!"
                : $"TIME: {ModeSelectView.FormatTime(seconds)}   BEST: {ModeSelectView.FormatTime(best)}";
        }

        /// <summary>
        /// Lists every target word cleared this level with its dictionary meaning. Target words
        /// only: bonus finds include abbreviations and function words that WordNet has nothing
        /// useful to say about, so they would pad the list with "no definition" rows.
        /// </summary>
        private void BuildMeaningsList()
        {
            if (_meaningsList == null) return;
            _meaningsList.Clear();

            if (_definitions == null && ServiceLocator.Current.Has<WordDefinitionService>())
                _definitions = ServiceLocator.Current.Get<WordDefinitionService>();

            if (_gameModel == null || _definitions == null || !_definitions.IsReady)
            {
                ShowMeaningsBlock(false);
                return;
            }

            var words = new System.Collections.Generic.List<string>(_gameModel.SolvedTargetWords);
            words.Sort((a, b) => b.Length.CompareTo(a.Length));   // longest first: the best find leads

            int shown = 0;
            foreach (string word in words)
            {
                string meaning = _definitions.GetPrimaryMeaning(word);
                if (string.IsNullOrEmpty(meaning)) continue;      // nothing to teach, so no row

                _meaningsList.Add(BuildRow(word, meaning));
                shown++;
            }

            // An empty scroll view leaves a gap above the button, so the whole block goes away.
            ShowMeaningsBlock(shown > 0);
        }

        /// <summary>
        /// One line, shown only on the clear that first fills the collection, telling the
        /// player where their words went. Any earlier and there would be nothing to look at.
        /// </summary>
        private void RefreshOnboardingTip()
        {
            Label tip = rootElement?.Q<Label>("lbl-onboarding-tip");
            if (tip == null) return;

            bool show = OnboardingFlow.Step == OnboardingStep.FindCollection;
            tip.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) tip.text = "Every word you find is saved. See them all in WORD COLLECTION on the main menu.";
        }

        private void ShowMeaningsBlock(bool visible)
        {
            DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            VisualElement scroll = rootElement?.Q<VisualElement>("meanings-scroll");
            if (scroll != null) scroll.style.display = display;

            Label heading = rootElement?.Q<Label>(className: "meanings-heading");
            if (heading != null) heading.style.display = display;
        }

        private VisualElement BuildRow(string word, string meaning)
        {
            var row = new VisualElement();
            row.AddToClassList("meaning-row");

            var wordLabel = new Label(word.ToUpperInvariant());
            wordLabel.AddToClassList("meaning-word");
            row.Add(wordLabel);

            string pos = _definitions.GetPrimaryPartOfSpeech(word);
            string baseForm = _definitions.GetBaseForm(word);

            // "noun · form of CAT" tells the player why the definition reads singular.
            string caption = pos;
            if (!string.IsNullOrEmpty(baseForm))
            {
                caption = string.IsNullOrEmpty(caption)
                    ? $"form of {baseForm}"
                    : $"{caption} · form of {baseForm}";
            }

            if (!string.IsNullOrEmpty(caption))
            {
                var posLabel = new Label(caption);
                posLabel.AddToClassList("meaning-pos");
                row.Add(posLabel);
            }

            var meaningLabel = new Label(meaning);
            meaningLabel.AddToClassList("meaning-text");
            row.Add(meaningLabel);

            return row;
        }

        protected override void OnHide()
        {
            if (_nextLevelButton != null)
            {
                _nextLevelButton.clicked -= OnNextLevelClicked;
            }

            if (_mainMenuLabel != null)
            {
                _mainMenuLabel.UnregisterCallback<ClickEvent>(OnMainMenuClicked);
            }
        }

        /// <summary>
        /// Fills in the star rating and score. Stars start at 3 and drop one per tile hint
        /// taken, so a hinted clear can never show a full-marks result.
        /// </summary>
        private void RefreshResult()
        {
            if (_gameModel == null && ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();

            VisualElement card = rootElement.Q<VisualElement>(className: "victory-card");

            // The card and stars start transparent so they can animate in. Adding the class on a
            // later frame is what lets the transition run - setting it in the same frame as the
            // initial state would snap straight to the end value.
            if (card != null)
            {
                card.RemoveFromClassList(CardShownClass);
                card.schedule.Execute(() => card.AddToClassList(CardShownClass)).StartingIn(16);
            }

            if (_gameModel == null) return;

            int stars = _gameModel.StarsEarned;

            for (int i = 0; i < 3; i++)
            {
                VisualElement star = rootElement.Q<VisualElement>($"star-{i}");
                if (star == null) continue;

                star.EnableInClassList("star--earned", i < stars);
                star.EnableInClassList("star--empty", i >= stars);
                star.RemoveFromClassList(StarShownClass);

                // Stagger so the stars pop in one after another instead of all at once.
                int index = i;
                star.schedule.Execute(() => star.AddToClassList(StarShownClass))
                    .StartingIn(150 + index * 130);
            }

            if (_scoreLabel != null)
            {
                int hints = _gameModel.HintsUsed.Value;
                _scoreLabel.text = hints > 0
                    ? $"SCORE: {_gameModel.Score.Value}   (-{hints * WondersOfWordGameModel.HINT_SCORE_PENALTY} FOR {hints} HINT{(hints > 1 ? "S" : "")})"
                    : $"SCORE: {_gameModel.Score.Value}";
            }
        }

        private void OnMainMenuClicked(ClickEvent evt)
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (config != null && ServiceLocator.Current.Has<UIManager>())
                ServiceLocator.Current.Get<UIManager>().HideOverlay(config);

            if (_gameManager != null) _gameManager.QuitToMainMenu();
        }

        private void OnNextLevelClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);
            if (_gameManager != null)
            {
                _gameManager.LoadNextLevel();
            }
        }
    }
}
