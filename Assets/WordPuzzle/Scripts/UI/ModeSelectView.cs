using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;
using WordPuzzle.Managers;
using WordPuzzle.Models;
using WordPuzzle.Services;

namespace WordPuzzle.UI
{
    /// <summary>
    /// Draggable mode carousel. Three cards are on screen at once - the centre one large and
    /// highlighted, its neighbours shrunk and dimmed - and the whole track follows the finger
    /// rather than swapping cards on release.
    ///
    /// Cards are built here instead of in UXML because the track repeats the mode list: it is
    /// tiled so the carousel can wrap, and it silently re-centres after each snap, which makes
    /// it scroll forever in both directions without a visible jump.
    /// </summary>
    public class ModeSelectView : BaseUI
    {
        private static readonly GameMode[] Modes =
            { GameMode.Classic, GameMode.TimeTrial, GameMode.Endless };

        /// <summary>How many times the mode list is tiled. Odd, so there is a middle copy to sit in.</summary>
        private const int Repeats = 3;

        private const float CardWidth = 480f;
        private const float CardGap = 36f;
        private const float Step = CardWidth + CardGap;

        private const string TrackSnapClass = "mode-track--snapping";
        private const string IconTimedClass = "mode-icon--timed";
        private const string IconEndlessClass = "mode-icon--endless";
        private const string DotActiveClass = "mode-dot--active";

        private const float SwipeThresholdPx = 40f;
        private const long SnapMs = 240;

        private VisualElement _viewport;
        private VisualElement _track;
        private VisualElement _dots;
        private Button _playButton;
        private Label _backLabel;

        private readonly List<VisualElement> _cards = new List<VisualElement>();

        private AudioManager _audioManager;
        private GameManager _gameManager;
        private WondersOfWordGameModel _gameModel;

        // Index into the tiled card list, not into Modes.
        private int _slot;
        private float _viewportWidth;
        private bool _dragging;
        private float _dragStartX;
        private float _dragStartOffset;
        private float _offset;

        private GameMode SelectedMode => Modes[_slot % Modes.Length];

        protected override void OnInitialize()
        {
            if (ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();
            if (ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            _viewport = rootElement.Q<VisualElement>("mode-viewport");
            _track = rootElement.Q<VisualElement>("mode-track");
            _dots = rootElement.Q<VisualElement>("mode-dots");
            _playButton = rootElement.Q<Button>("btn-play-mode");
            _backLabel = rootElement.Q<Label>("lbl-mode-back");

            BuildCards();

            // Start in the middle copy so there is room to wrap either way.
            int startMode = System.Array.IndexOf(Modes, GameModeContext.Current);
            if (startMode < 0) startMode = 0;
            _slot = (Repeats / 2) * Modes.Length + startMode;

            if (_playButton != null) _playButton.clicked += OnPlay;
            if (_backLabel != null) _backLabel.RegisterCallback<ClickEvent>(OnBack);

            if (_viewport != null)
            {
                _viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
                _viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                _viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
                _viewport.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

                // Width is unknown until the panel lays out, and centring depends on it.
                _viewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometry);
            }
        }

        protected override void OnHide()
        {
            if (_playButton != null) _playButton.clicked -= OnPlay;
            if (_backLabel != null) _backLabel.UnregisterCallback<ClickEvent>(OnBack);

            if (_viewport != null)
            {
                _viewport.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                _viewport.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                _viewport.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                _viewport.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
                _viewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometry);
            }

            _cards.Clear();
        }

        /// <summary>Tiles the mode list into the track so the carousel has cards to wrap through.</summary>
        private void BuildCards()
        {
            if (_track == null) return;

            _track.Clear();
            _cards.Clear();

            for (int repeat = 0; repeat < Repeats; repeat++)
            {
                for (int i = 0; i < Modes.Length; i++)
                {
                    VisualElement card = BuildCard(Modes[i]);
                    _track.Add(card);
                    _cards.Add(card);
                }
            }
        }

        private VisualElement BuildCard(GameMode mode)
        {
            bool timed = mode == GameMode.TimeTrial;

            var card = new VisualElement();
            card.AddToClassList("mode-card");
            card.pickingMode = PickingMode.Ignore;   // drags belong to the viewport

            var icon = new VisualElement();
            icon.AddToClassList("mode-icon");
            if (mode == GameMode.TimeTrial) icon.AddToClassList(IconTimedClass);
            if (mode == GameMode.Endless) icon.AddToClassList(IconEndlessClass);
            card.Add(icon);

            var name = new Label(GameModeContext.DisplayName(mode));
            name.AddToClassList("mode-name");
            card.Add(name);

            var desc = new Label(GameModeContext.Description(mode));
            desc.AddToClassList("mode-desc");
            card.Add(desc);

            // Each card reads its own mode's saved progress: the two campaigns advance
            // independently, so the player needs to see both before choosing.
            int level = Mathf.Max(1, PlayerPrefs.GetInt(
                GameModeContext.KeyFor(mode, "WordPuzzle_CurrentLevel"), 1));

            var progress = new Label($"LEVEL {level}");
            progress.AddToClassList("mode-stat");
            card.Add(progress);

            // Endless has no level to resume - a run always starts fresh - so it shows the
            // best run instead of a position in the campaign.
            if (mode == GameMode.Endless)
            {
                int bestRun = PlayerPrefs.GetInt(GameModeContext.KeyFor(mode, "WordPuzzle_BestRun"), 0);
                progress.text = bestRun > 0 ? $"BEST RUN  {bestRun}" : "NO RUN YET";
            }
            else if (timed)
            {
                float best = PlayerPrefs.GetFloat(
                    GameModeContext.KeyFor(mode, "WordPuzzle_BestTime") + "_Lvl_" + level, 0f);

                var extra = new Label(best > 0f ? $"BEST  {FormatTime(best)}" : "NO TIME SET YET");
                extra.AddToClassList("mode-stat");
                extra.AddToClassList("mode-stat--dim");
                card.Add(extra);
            }

            return card;
        }

        private void OnViewportGeometry(GeometryChangedEvent evt)
        {
            _viewportWidth = evt.newRect.width;
            ApplyOffset(TargetOffset(_slot), false);
            RefreshCardStates();
            RefreshDots();
        }

        /// <summary>Track translation that puts the given slot in the middle of the viewport.</summary>
        private float TargetOffset(int slot) => (_viewportWidth - CardWidth) * 0.5f - slot * Step;

        private void ApplyOffset(float offset, bool animate)
        {
            if (_track == null) return;

            _offset = offset;
            _track.EnableInClassList(TrackSnapClass, animate);
            _track.style.translate = new Translate(offset, 0f, 0f);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _dragging = true;
            _dragStartX = evt.position.x;
            _dragStartOffset = _offset;
            _viewport.CapturePointer(evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging) return;

            // No transition while dragging: the track must sit exactly under the finger.
            float delta = evt.position.x - _dragStartX;
            ApplyOffset(_dragStartOffset + delta, false);
            RefreshCardStates();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging) return;
            _viewport.ReleasePointer(evt.pointerId);
            EndDrag(evt.position.x - _dragStartX);
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            // Pointer left the panel mid-drag: settle rather than leaving the track adrift.
            if (_dragging) EndDrag(0f);
        }

        private void EndDrag(float dragDistance)
        {
            _dragging = false;

            int steps = 0;
            if (Mathf.Abs(dragDistance) >= SwipeThresholdPx)
            {
                // Long drags cross more than one card, so the count comes from the distance
                // rather than always being a single step.
                steps = -Mathf.RoundToInt(dragDistance / Step);
                if (steps == 0) steps = dragDistance < 0f ? 1 : -1;
            }

            GoToSlot(_slot + steps, steps != 0);
        }

        private void GoToSlot(int slot, bool feedback)
        {
            int clamped = Mathf.Clamp(slot, 0, _cards.Count - 1);
            bool changed = clamped != _slot;
            _slot = clamped;

            if (changed && feedback)
            {
                if (_audioManager != null) _audioManager.PlayButtonClickSound();
                HapticManager.Play(HapticType.Selection);
            }

            ApplyOffset(TargetOffset(_slot), true);
            RefreshCardStates();
            RefreshDots();

            // Once settled, hop back to the equivalent slot in the middle copy. Same card,
            // same pixel position, so the jump is invisible - this is what lets the carousel
            // run forever in either direction.
            _track?.schedule.Execute(Recentre).StartingIn(SnapMs);
        }

        private void Recentre()
        {
            if (_dragging) return;

            int middle = (Repeats / 2) * Modes.Length + (_slot % Modes.Length);
            if (middle == _slot) return;

            _slot = middle;
            ApplyOffset(TargetOffset(_slot), false);
            RefreshCardStates();
        }

        /// <summary>
        /// Scales cards by how close they are to centre, so the focused card is big and bright
        /// and its neighbours shrink away. Driven by live offset rather than by index, so it
        /// tracks continuously while a drag is in progress.
        /// </summary>
        private void RefreshCardStates()
        {
            if (_viewportWidth <= 1f) return;

            float centreX = _viewportWidth * 0.5f;

            for (int i = 0; i < _cards.Count; i++)
            {
                float cardCentre = _offset + i * Step + CardWidth * 0.5f;
                float distance = Mathf.Abs(cardCentre - centreX) / Step;

                // 1 at dead centre, 0 a full card away.
                float focus = Mathf.Clamp01(1f - distance);

                float scale = Mathf.Lerp(0.86f, 1f, focus);
                _cards[i].style.scale = new Scale(new Vector2(scale, scale));
                _cards[i].style.opacity = Mathf.Lerp(0.4f, 1f, focus);
            }
        }

        private void RefreshDots()
        {
            if (_dots == null) return;

            int active = _slot % Modes.Length;
            for (int i = 0; i < Modes.Length; i++)
            {
                _dots.Q<VisualElement>($"dot-{i}")?.EnableInClassList(DotActiveClass, i == active);
            }
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }

        private void OnPlay()
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

            // Set before the reload: the model reads mode-scoped keys.
            GameModeContext.SetMode(SelectedMode);
            if (_gameModel != null) _gameModel.ReloadForCurrentMode();

            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (config != null && ServiceLocator.Current.Has<UIManager>())
                ServiceLocator.Current.Get<UIManager>().HideOverlay(config);

            if (_gameManager != null) _gameManager.StartCurrentLevel();
        }

        private void OnBack(ClickEvent evt)
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

            if (config != null && ServiceLocator.Current.Has<UIManager>())
                ServiceLocator.Current.Get<UIManager>().HideOverlay(config);
        }
    }
}
