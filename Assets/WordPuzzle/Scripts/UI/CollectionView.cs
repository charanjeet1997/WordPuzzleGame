using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;
using WordPuzzle.Managers;
using WordPuzzle.Services;

namespace WordPuzzle.UI
{
    /// <summary>
    /// The word collection - every collectable word in the game with its meaning, and how many
    /// the player has discovered so far.
    ///
    /// The list is virtualised: there are over six thousand words, and building a row per word
    /// would stall the frame and hold thousands of live elements. ListView creates a handful of
    /// rows and rebinds them as the player scrolls.
    /// </summary>
    public class CollectionView : BaseUI
    {
        private enum Filter { All, Found, Locked }

        private const string ChipOnClass = "filter-chip--on";
        private const string ChipLockedClass = "word-chip--locked";
        private const string ChipSelectedClass = "word-chip--selected";

        /// <summary>Chips per ListView row. Three fit the panel interior at 276px each.</summary>
        private const int ChipsPerRow = 3;

        private Label _countLabel;
        private VisualElement _progressFill;
        private ListView _list;
        private Button _allChip;
        private Button _foundChip;
        private Button _lockedChip;
        private Button _closeButton;

        private WordCollectionService _collection;
        private WordDefinitionService _definitions;
        private AudioManager _audioManager;
        private UIManager _uiManager;

        // Rows of words rather than single words: the ListView virtualises rows, and each
        // row lays out three chips.
        private readonly List<string[]> _rows = new List<string[]>();
        private readonly List<string> _visible = new List<string>();
        private Filter _filter = Filter.All;
        private string _selected;

        private Label _detailWord;
        private Label _detailPos;
        private Label _detailMeaning;

        protected override void OnInitialize()
        {
            if (ServiceLocator.Current.Has<WordCollectionService>())
                _collection = ServiceLocator.Current.Get<WordCollectionService>();
            if (ServiceLocator.Current.Has<WordDefinitionService>())
                _definitions = ServiceLocator.Current.Get<WordDefinitionService>();
            if (ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
            if (ServiceLocator.Current.Has<UIManager>())
                _uiManager = ServiceLocator.Current.Get<UIManager>();
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            _countLabel = rootElement.Q<Label>("lbl-collection-count");
            _progressFill = rootElement.Q<VisualElement>("progress-fill");
            _list = rootElement.Q<ListView>("collection-list");
            _allChip = rootElement.Q<Button>("btn-filter-all");
            _foundChip = rootElement.Q<Button>("btn-filter-found");
            _lockedChip = rootElement.Q<Button>("btn-filter-missing");
            _closeButton = rootElement.Q<Button>("btn-collection-close");
            _detailWord = rootElement.Q<Label>("lbl-detail-word");
            _detailPos = rootElement.Q<Label>("lbl-detail-pos");
            _detailMeaning = rootElement.Q<Label>("lbl-detail-meaning");

            if (_collection == null && ServiceLocator.Current.Has<WordCollectionService>())
                _collection = ServiceLocator.Current.Get<WordCollectionService>();
            if (_definitions == null && ServiceLocator.Current.Has<WordDefinitionService>())
                _definitions = ServiceLocator.Current.Get<WordDefinitionService>();

            if (_allChip != null) _allChip.clicked += OnFilterAll;
            if (_foundChip != null) _foundChip.clicked += OnFilterFound;
            if (_lockedChip != null) _lockedChip.clicked += OnFilterLocked;
            if (_closeButton != null) _closeButton.clicked += OnClose;

            SetupList();
            RefreshSummary();
            RebuildRows();
        }

        protected override void OnHide()
        {
            if (_allChip != null) _allChip.clicked -= OnFilterAll;
            if (_foundChip != null) _foundChip.clicked -= OnFilterFound;
            if (_lockedChip != null) _lockedChip.clicked -= OnFilterLocked;
            if (_closeButton != null) _closeButton.clicked -= OnClose;

            // Discoveries are batched in memory; leaving the screen is a natural flush point.
            _collection?.Save();
        }

        private void SetupList()
        {
            if (_list == null) return;

            _list.makeItem = MakeRow;
            _list.bindItem = BindRow;
            _list.itemsSource = _rows;
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("chip-row");

            for (int i = 0; i < ChipsPerRow; i++)
            {
                var chip = new Button { name = "chip-" + i };
                chip.AddToClassList("word-chip");
                row.Add(chip);
            }

            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count) return;

            string[] words = _rows[index];

            for (int i = 0; i < ChipsPerRow; i++)
            {
                var chip = element.Q<Button>("chip-" + i);
                if (chip == null) continue;

                // Rebinding reuses elements, so the previous row's handler must come off
                // first or a chip accumulates clicks for every word it has ever shown.
                if (chip.userData is System.Action previous)
                {
                    chip.clicked -= previous;
                    chip.userData = null;
                }

                if (i >= words.Length || string.IsNullOrEmpty(words[i]))
                {
                    // Padding on the final row: kept in the tree so the row holds its shape.
                    chip.visible = false;
                    continue;
                }

                string word = words[i];
                bool found = _collection != null && _collection.IsDiscovered(word);

                chip.visible = true;
                // Letters stay hidden until found, but the length does not: an undiscovered
                // entry should read as something to find, not as an empty slot.
                chip.text = found ? word : new string('?', word.Length);
                chip.EnableInClassList(ChipLockedClass, !found);
                chip.EnableInClassList(ChipSelectedClass, found && word == _selected);

                System.Action handler = () => OnChipClicked(word, found);
                chip.userData = handler;
                chip.clicked += handler;
            }
        }

        /// <summary>
        /// Fills the detail panel rather than expanding the row. Definitions run from four
        /// words to three lines, and letting them size the grid would make it lurch.
        /// </summary>
        private void OnChipClicked(string word, bool found)
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Selection);

            _selected = found ? word : null;

            if (!found)
            {
                if (_detailWord != null) _detailWord.text = new string('?', word.Length);
                if (_detailPos != null) _detailPos.text = word.Length + " letters";
                if (_detailMeaning != null)
                    _detailMeaning.text = "Not discovered yet - find it in a level to unlock its meaning.";
                _list?.RefreshItems();
                return;
            }

            string meaning = _definitions != null ? _definitions.GetPrimaryMeaning(word) : null;
            string pos = _definitions != null ? _definitions.GetPrimaryPartOfSpeech(word) : null;
            string baseForm = _definitions != null ? _definitions.GetBaseForm(word) : null;

            if (!string.IsNullOrEmpty(baseForm))
                pos = string.IsNullOrEmpty(pos) ? "form of " + baseForm : pos + " - form of " + baseForm;

            if (_detailWord != null) _detailWord.text = word;
            if (_detailPos != null) _detailPos.text = pos ?? "";
            if (_detailMeaning != null)
                _detailMeaning.text = string.IsNullOrEmpty(meaning) ? "No meaning available." : meaning;

            _list?.RefreshItems();
        }

        private void RefreshSummary()
        {
            if (_collection == null) return;

            if (_countLabel != null)
                _countLabel.text = $"{_collection.DiscoveredCount} / {_collection.TotalCount}";

            if (_progressFill != null)
                _progressFill.style.width = Length.Percent(_collection.CompletionFraction * 100f);
        }

        private void RebuildRows()
        {
            _rows.Clear();
            _visible.Clear();

            if (_collection != null)
            {
                // Discovered words lead. Alphabetical order buried six finds among thousands
                // of locked slots, so the screen opened on a wall of question marks and the
                // player's actual progress was invisible.
                var locked = new List<string>();

                foreach (string word in _collection.AllWords)
                {
                    bool found = _collection.IsDiscovered(word);
                    if (_filter == Filter.Found && !found) continue;
                    if (_filter == Filter.Locked && found) continue;

                    if (found) _visible.Add(word);
                    else locked.Add(word);
                }

                _visible.AddRange(locked);
            }

            for (int i = 0; i < _visible.Count; i += ChipsPerRow)
            {
                int count = Mathf.Min(ChipsPerRow, _visible.Count - i);
                var row = new string[ChipsPerRow];
                for (int c = 0; c < count; c++) row[c] = _visible[i + c];
                _rows.Add(row);
            }

            _list?.Rebuild();
        }

        private void SetFilter(Filter filter)
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Selection);

            _filter = filter;
            _allChip?.EnableInClassList(ChipOnClass, filter == Filter.All);
            _foundChip?.EnableInClassList(ChipOnClass, filter == Filter.Found);
            _lockedChip?.EnableInClassList(ChipOnClass, filter == Filter.Locked);

            RebuildRows();
        }

        private void OnFilterAll() => SetFilter(Filter.All);
        private void OnFilterFound() => SetFilter(Filter.Found);
        private void OnFilterLocked() => SetFilter(Filter.Locked);

        private void OnClose()
        {
            if (_audioManager != null) _audioManager.PlayButtonClickSound();
            HapticManager.Play(HapticType.Light);

            if (_uiManager == null && ServiceLocator.Current.Has<UIManager>())
                _uiManager = ServiceLocator.Current.Get<UIManager>();

            if (_uiManager != null && config != null) _uiManager.HideOverlay(config);
        }
    }
}
