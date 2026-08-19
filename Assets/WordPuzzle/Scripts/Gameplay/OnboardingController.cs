using System.Collections.Generic;
using UnityEngine;
using ServiceLocatorFramework;
using DataBindingFramework;
using WordPuzzle.Data;
using WordPuzzle.Models;

namespace WordPuzzle.Gameplay
{
    /// <summary>
    /// Teaches the swipe, and nothing else. Everything else in this game is self-evident from
    /// looking at it; dragging through letters is the one mechanic a first-time player can fail
    /// to discover, and a player who never finds a word never sees anything else we built.
    ///
    /// Deliberately not a multi-step tutorial: each additional forced step measurably costs
    /// first-session completion, and hints, shuffle and coins all explain themselves once the
    /// player is solving words.
    /// </summary>
    public class OnboardingController : MonoBehaviour
    {
        [Tooltip("Words the player must solve before the hint stops appearing for good.")]
        [SerializeField] private int wordsUntilLearned = 2;

        [Tooltip("Seconds of no input before the hint reappears.")]
        [SerializeField] private float idleBeforeHint = 5f;

        [Tooltip("Shorter on the very first word: a new player is looking at an unexplained screen.")]
        [SerializeField] private float idleBeforeFirstHint = 1.5f;

        [SerializeField] private OnboardingHint hint;

        private GameplayHandler _handler;
        private LetterWheelController _wheel;
        private WondersOfWordGameModel _model;

        private IObserver<char> _swipeObserver;
        private IObserver<string> _matchedObserver;

        private readonly List<Vector3> _path = new List<Vector3>();
        private float _idleTime;
        private int _wordsSolvedSinceStart;
        private bool _active;

        /// <summary>False once the player has proven they can swipe, and on every later session.</summary>
        public static bool Pending => OnboardingFlow.Step == OnboardingStep.LearnSwipe;

        /// <summary>
        /// Replays onboarding. Called from Reset Progress.
        ///
        /// Not named Reset: that is a MonoBehaviour message the editor invokes on the instance
        /// when the component is added, and a static method of that name makes AddComponent
        /// fail with "Failed to call static function Reset because an object was provided".
        /// </summary>
        public static void ClearOnboardingFlag() => OnboardingFlow.Clear();

        private void Awake()
        {
            if (hint == null) hint = GetComponentInChildren<OnboardingHint>();
            _handler = GetComponentInParent<GameplayHandler>();
            if (_handler != null) _wheel = _handler.wheelController;
        }

        private void OnEnable()
        {
            if (!Pending)
            {
                enabled = false;
                return;
            }

            if (!ServiceLocator.Current.Has<IObserverManager>()) return;

            var observers = ServiceLocator.Current.Get<IObserverManager>();

            // Any swipe character means the player is engaged: the hint gets out of the way.
            _swipeObserver = observers.GetOrCreateObserver<char>(WondersOfWordGameModel.OBS_SWIPE_CHAR_ADDED);
            _swipeObserver.Bind(this, OnSwipeChar);

            _matchedObserver = observers.GetOrCreateObserver<string>(WondersOfWordGameModel.OBS_WORD_MATCHED);
            _matchedObserver.Bind(this, OnWordMatched);

            HookWheel();

            _active = true;
            _idleTime = idleBeforeFirstHint;   // show almost immediately on a cold start
        }

        private void OnDisable()
        {
            if (_swipeObserver != null)
            {
                _swipeObserver.Unbind(OnSwipeChar);
                _swipeObserver = null;
            }

            if (_matchedObserver != null)
            {
                _matchedObserver.Unbind(OnWordMatched);
                _matchedObserver = null;
            }

            if (_wheel != null) _wheel.WheelRebuilt -= OnWheelRebuilt;

            hint?.Stop();
            _active = false;
        }

        private void HookWheel()
        {
            if (_wheel == null && _handler != null) _wheel = _handler.wheelController;
            if (_wheel == null) return;

            _wheel.WheelRebuilt -= OnWheelRebuilt;
            _wheel.WheelRebuilt += OnWheelRebuilt;
        }

        /// <summary>
        /// A shuffle or a new level moves every letter, so a path traced from the old positions
        /// now points at the wrong ones. Drop it and re-derive on the next idle.
        /// </summary>
        private void OnWheelRebuilt()
        {
            hint?.Stop();
            _idleTime = 0f;
        }

        private void OnSwipeChar(char letter)
        {
            _idleTime = 0f;
            hint?.Stop();
        }

        private void OnWordMatched(string word)
        {
            _idleTime = 0f;
            hint?.Stop();

            _wordsSolvedSinceStart++;
            if (_wordsSolvedSinceStart < wordsUntilLearned) return;

            // Proven. The flow moves on to pointing at the collection, which now has words
            // in it and so is finally worth mentioning.
            OnboardingFlow.MarkSwipeLearned();
            enabled = false;
        }

        private void Update()
        {
            if (!_active || hint == null) return;

            if (_model == null && ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _model = ServiceLocator.Current.Get<WondersOfWordGameModel>();

            // Only while actually playing: not over the pause popup or the victory card.
            if (_model == null || _model.State.Value != GameState.Playing)
            {
                hint.Stop();
                return;
            }

            if (hint.IsPlaying) return;

            _idleTime += Time.unscaledDeltaTime;
            if (_idleTime < idleBeforeHint) return;

            ShowHintForEasiestWord();
        }

        /// <summary>
        /// Demonstrates the shortest unsolved word. Shortest because the first success should be
        /// as cheap as possible - a seven-letter demonstration is a lot to follow and copy.
        /// </summary>
        private void ShowHintForEasiestWord()
        {
            if (_wheel == null && _handler != null) _wheel = _handler.wheelController;
            if (_wheel == null || _handler == null || _handler.CurrentLevelData == null) return;

            var targets = _handler.CurrentLevelData.targetWords;
            if (targets == null) return;

            string best = null;
            foreach (TargetWordEntry entry in targets)
            {
                if (entry == null || string.IsNullOrEmpty(entry.word)) continue;

                string word = entry.word.ToUpperInvariant();
                if (_model != null && _model.SolvedTargetWords.Contains(word)) continue;
                if (best == null || word.Length < best.Length) best = word;
            }

            if (best == null) return;
            if (!_wheel.TryGetSwipePath(best, _path)) return;

            // Re-measured every time: the wheel shrinks to fit letter count and screen shape,
            // and the path is rebuilt after every shuffle.
            hint.SetScaleReference(_wheel.NodeSizeInUse);
            hint.Play(_path);
            _idleTime = 0f;
        }
    }
}
