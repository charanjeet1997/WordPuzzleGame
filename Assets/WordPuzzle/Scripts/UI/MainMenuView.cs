using UnityEngine;
using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Models;
using WordPuzzle.Managers;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;
using WordPuzzle.Services;

namespace WordPuzzle.UI
{
    public class MainMenuView : BaseUI
    {
        private Button _playButton;
        private Button _collectionButton;
        private Button _settingsButton;
        private Button _soundButton;
        private Label _titleLabel;
        private Label _coinsLabel;
        private VisualElement _chapterListItems;
        private ScrollView _chapterCarousel;

        // Drag-to-scroll state. UI Toolkit's ScrollView scrolls by wheel and by scrollbar, but
        // has no mouse drag, so with the bar hidden the strip had no way to move at all.
        private bool _draggingCarousel;
        private float _dragStartX;
        private float _dragStartOffset;

        /// <summary>Levels per chapter, matching how the generator stamps chapterTitle.</summary>
        private const int LevelsPerChapter = 20;

        private WondersOfWordGameModel _gameModel;
        private GameManager _gameManager;
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

            // Match UXML names: btn-play, btn-settings, btn-sound
            _playButton = rootElement.Q<Button>("btn-play") ?? rootElement.Q<Button>("PlayButton") ?? rootElement.Q<Button>(className: "btn-primary") ?? rootElement.Q<Button>();
            _collectionButton = rootElement.Q<Button>("btn-collection");
            _settingsButton = rootElement.Q<Button>("btn-settings") ?? rootElement.Q<Button>("SettingsButton");
            _soundButton = rootElement.Q<Button>("btn-sound") ?? rootElement.Q<Button>("SoundButton");
            
            _titleLabel = rootElement.Q<Label>("TitleLabel") ?? rootElement.Q<Label>(className: "game-title");
            _chapterListItems = rootElement.Q<VisualElement>("chapter-carousel-items");
            _chapterCarousel = rootElement.Q<ScrollView>("chapter-carousel");
            HookCarouselDrag();
            _coinsLabel = rootElement.Q<Label>("CoinsLabel") ?? rootElement.Q<Label>(className: "coins-text");

            if (_playButton != null)
            {
                _playButton.clicked += OnPlayClicked;
            }

            if (_soundButton != null)
            {
                _soundButton.clicked += OnSoundClicked;
            }

            if (_settingsButton != null)
            {
                _settingsButton.clicked += OnSettingsClicked;
            }

            if (_collectionButton != null)
            {
                _collectionButton.clicked += OnCollectionClicked;
                RefreshCollectionCount();
            }

            RefreshSoundIcon();
            RefreshChapterCard();

            if (_gameModel != null && _coinsLabel != null)
            {
                _gameModel.Coins.Bind(_bindingOwner, (coins) => _coinsLabel.text = coins.ToString());
                _coinsLabel.text = _gameModel.Coins.Value.ToString();
            }
        }

        protected override void OnHide()
        {
            if (_playButton != null)
            {
                _playButton.clicked -= OnPlayClicked;
            }

            if (_soundButton != null)
            {
                _soundButton.clicked -= OnSoundClicked;
            }

            if (_settingsButton != null)
            {
                _settingsButton.clicked -= OnSettingsClicked;
            }

            if (_collectionButton != null)
            {
                _collectionButton.clicked -= OnCollectionClicked;
            }
        }

        /// <summary>
        /// Makes the chapter strip draggable with the mouse or a finger. Registered on the
        /// ScrollView itself so a drag started anywhere over the cards counts, which is how a
        /// carousel is expected to behave - there is no visible bar to grab.
        /// </summary>
        private void HookCarouselDrag()
        {
            if (_chapterCarousel == null) return;

            _chapterCarousel.RegisterCallback<PointerDownEvent>(OnCarouselPointerDown);
            _chapterCarousel.RegisterCallback<PointerMoveEvent>(OnCarouselPointerMove);
            _chapterCarousel.RegisterCallback<PointerUpEvent>(OnCarouselPointerUp);
            _chapterCarousel.RegisterCallback<PointerCaptureOutEvent>(_ => _draggingCarousel = false);
        }

        private void OnCarouselPointerDown(PointerDownEvent evt)
        {
            _draggingCarousel = true;
            _dragStartX = evt.position.x;
            _dragStartOffset = _chapterCarousel.scrollOffset.x;

            // Captured so the drag keeps tracking once the pointer leaves the strip; without
            // it a fast flick stops the moment the cursor crosses the edge.
            _chapterCarousel.CapturePointer(evt.pointerId);
        }

        private void OnCarouselPointerMove(PointerMoveEvent evt)
        {
            if (!_draggingCarousel) return;

            // Dragging left moves the content left, so the offset moves opposite the pointer.
            float offset = _dragStartOffset - (evt.position.x - _dragStartX);
            _chapterCarousel.scrollOffset = new Vector2(offset, _chapterCarousel.scrollOffset.y);
        }

        private void OnCarouselPointerUp(PointerUpEvent evt)
        {
            if (!_draggingCarousel) return;

            _draggingCarousel = false;
            _chapterCarousel.ReleasePointer(evt.pointerId);
        }

        private void OnPlayClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);
            if (_gameManager != null)
            {
                _gameManager.ShowModeSelect();
            }
        }

        /// <summary>
        /// Works out which chapter the player is in and rebuilds the carousel around it. The
        /// card is no longer a single fixed element in the UXML - there is one per chapter and
        /// they are all built in <see cref="RefreshChapterList"/>.
        /// </summary>
        private void RefreshChapterCard()
        {
            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();
            if (_gameModel == null && ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();

            LevelDatabase db = _gameManager != null ? _gameManager.levelDatabase : null;
            int perChapter = db != null && db.LevelsPerChapter > 0 ? db.LevelsPerChapter : LevelsPerChapter;

            int level = _gameModel != null ? _gameModel.CurrentLevelIndex.Value : 1;
            RefreshChapterList(((level - 1) / perChapter) + 1);
        }

        /// <summary>
        /// Lists every chapter in the level database, marking the ones the player has not
        /// reached as locked. Built from the database rather than a fixed count, so adding
        /// levels adds chapters here without a second edit.
        /// </summary>
        private void RefreshChapterList(int currentChapter)
        {
            if (_chapterListItems == null) return;

            _chapterListItems.Clear();

            LevelDatabase db = _gameManager != null ? _gameManager.levelDatabase : null;
            if (db == null || db.Count <= 0) return;

            // The database owns the grouping; the constant here is only a fallback for a
            // database that has not been set up yet.
            int perChapter = db.LevelsPerChapter > 0 ? db.LevelsPerChapter : LevelsPerChapter;
            int chapters = Mathf.CeilToInt(db.Count / (float)perChapter);

            int level = _gameModel != null ? _gameModel.CurrentLevelIndex.Value : 1;

            for (int chapter = 1; chapter <= chapters; chapter++)
            {
                bool locked = chapter > currentChapter;
                bool current = chapter == currentChapter;

                var card = new VisualElement();
                card.AddToClassList("chapter-card");
                if (locked) card.AddToClassList("chapter-card--locked");
                else if (current) card.AddToClassList("chapter-card--current");

                // The last chapter is usually a part chapter, so its level count is whatever
                // the database has left rather than a full run.
                int levelsInChapter = Mathf.Min(perChapter, db.Count - (chapter - 1) * perChapter);

                var topRow = new VisualElement();
                topRow.AddToClassList("chapter-card-row");

                var title = new Label($"CHAPTER {chapter}");
                title.AddToClassList("chapter-title");
                topRow.Add(title);

                var meta = new Label($"{levelsInChapter} LEVELS · {ChapterLetters(db, chapter, perChapter, levelsInChapter)}");
                meta.AddToClassList("chapter-meta");
                topRow.Add(meta);
                card.Add(topRow);

                var name = new Label(ChapterName(db, chapter, perChapter, locked));
                name.AddToClassList("chapter-name");
                card.Add(name);

                var bottomRow = new VisualElement();
                bottomRow.AddToClassList("chapter-card-row");

                // Only the chapter in progress shows a level and dots: on a locked chapter
                // there is no progress to report, and on a finished one it is always full.
                var status = new Label(locked ? "LOCKED" : current ? $"LEVEL {level}" : "COMPLETE");
                status.AddToClassList("level-subtitle");
                bottomRow.Add(status);

                if (current)
                {
                    bottomRow.Add(BuildProgressDots(((level - 1) % perChapter) + 1, perChapter));
                }

                card.Add(bottomRow);
                _chapterListItems.Add(card);
            }
        }

        /// <summary>
        /// Wheel size across a chapter, as "4 LETTERS" or "4-6 LETTERS". This is what actually
        /// makes later chapters harder, so it belongs on the card next to the level count.
        /// Scanned across the whole chapter rather than read off its first level, because a
        /// chapter can step up in size partway through.
        /// </summary>
        private string ChapterLetters(LevelDatabase db, int chapter, int perChapter, int levelsInChapter)
        {
            int min = int.MaxValue;
            int max = 0;

            for (int i = 0; i < levelsInChapter; i++)
            {
                LevelData data = db.GetLevel((chapter - 1) * perChapter + 1 + i);
                int letters = data != null && data.wheelLetters != null ? data.wheelLetters.Length : 0;
                if (letters <= 0) continue;

                if (letters < min) min = letters;
                if (letters > max) max = letters;
            }

            if (max == 0) return "-";
            return min == max ? $"{max} LETTERS" : $"{min}-{max} LETTERS";
        }

        /// <summary>Five dots filled in proportion to progress through the chapter.</summary>
        private VisualElement BuildProgressDots(int levelInChapter, int perChapter)
        {
            const int DotCount = 5;

            var dots = new VisualElement();
            dots.AddToClassList("progress-dots");

            int on = Mathf.Clamp(
                Mathf.CeilToInt(levelInChapter / (float)perChapter * DotCount), 1, DotCount);

            for (int i = 0; i < DotCount; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("dot");
                if (i < on) dot.AddToClassList("dot--on");
                dots.Add(dot);
            }

            return dots;
        }

        /// <summary>
        /// Display name for a chapter, taken from the first level in it. Falls back to the
        /// chapter number, since the generator stamps some titles as a bare "Chapter N".
        /// </summary>
        private string ChapterName(LevelDatabase db, int chapter, int perChapter, bool locked)
        {
            LevelData first = db.GetLevel((chapter - 1) * perChapter + 1);
            string title = first != null ? first.chapterTitle : null;

            int dash = title != null ? title.IndexOf(" - ") : -1;
            if (dash >= 0) return title.Substring(dash + 3);

            return locked ? "Locked" : $"Chapter {chapter}";
        }

        /// <summary>The count is the hook - the button says how far along the collection is.</summary>
        private void RefreshCollectionCount()
        {
            Label count = rootElement?.Q<Label>("lbl-menu-collection-count");
            if (count == null) return;

            if (!ServiceLocator.Current.Has<WordCollectionService>())
            {
                count.text = "";
                return;
            }

            var collection = ServiceLocator.Current.Get<WordCollectionService>();
            count.text = $"{collection.DiscoveredCount} of {collection.TotalCount} words discovered";

            // Highlighted only while the player has words to look at but has never opened the
            // screen. It stops the moment they do, so it never nags.
            bool prompt = OnboardingFlow.Step == OnboardingStep.FindCollection
                          && collection.DiscoveredCount > 0;
            _collectionButton?.EnableInClassList("btn-collection--prompt", prompt);
        }

        private void OnCollectionClicked()
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (ServiceLocator.Current.Has<WordCollectionService>())
            {
                var collection = ServiceLocator.Current.Get<WordCollectionService>();
                AnalyticsService.CollectionOpened(collection.DiscoveredCount, collection.TotalCount);
            }

            OnboardingFlow.MarkCollectionSeen();
            RefreshCollectionCount();

            if (_gameManager != null) _gameManager.ShowCollection();
        }

        private void OnSettingsClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (_gameManager == null && ServiceLocator.Current.Has<GameManager>())
                _gameManager = ServiceLocator.Current.Get<GameManager>();

            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);
            if (_gameManager != null) _gameManager.ShowSettings();
        }

        private void OnSoundClicked()
        {
            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();

            if (_audioManager == null) return;

            // Click first, then toggle, so switching sound off still confirms the tap.
            _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);
            _audioManager.ToggleSound();
            RefreshSoundIcon();
        }

        /// <summary>Swaps the speaker art between the on and crossed-out off icons.</summary>
        private void RefreshSoundIcon()
        {
            if (_soundButton == null) return;

            if (_audioManager == null && ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();

            bool on = _audioManager == null || _audioManager.SoundEnabled;

            VisualElement icon = _soundButton.Q<VisualElement>("icon-sound");
            if (icon != null)
            {
                icon.EnableInClassList("icon-sound-on", on);
                icon.EnableInClassList("icon-sound-off", !on);
            }
        }
    }
}
