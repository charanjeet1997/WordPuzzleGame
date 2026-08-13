using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Models;
using WordPuzzle.Managers;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;

namespace WordPuzzle.UI
{
    public class LevelCompleteView : BaseUI
    {
        private Button _nextLevelButton;
        private Label _titleLabel;
        private Label _coinsEarnedLabel;
        private Label _scoreLabel;

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
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            // Match UXML name: btn-next-level
            _nextLevelButton = rootElement.Q<Button>("btn-next-level") ?? rootElement.Q<Button>("NextLevelButton") ?? rootElement.Q<Button>(className: "btn-victory-primary");
            _titleLabel = rootElement.Q<Label>("TitleLabel") ?? rootElement.Q<Label>(className: "victory-title");
            _coinsEarnedLabel = rootElement.Q<Label>("lbl-reward") ?? rootElement.Q<Label>("CoinsEarnedLabel") ?? rootElement.Q<Label>(className: "reward-text");
            _scoreLabel = rootElement.Q<Label>("lbl-score");

            if (_nextLevelButton != null)
            {
                _nextLevelButton.clicked += OnNextLevelClicked;
            }

            if (_coinsEarnedLabel != null)
            {
                _coinsEarnedLabel.text = "+50";
            }

            RefreshResult();
        }

        protected override void OnHide()
        {
            if (_nextLevelButton != null)
            {
                _nextLevelButton.clicked -= OnNextLevelClicked;
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
