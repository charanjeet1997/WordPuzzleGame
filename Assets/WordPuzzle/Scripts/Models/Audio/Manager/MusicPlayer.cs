using System.Collections.Generic;
using UnityEngine;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Models;
using WordPuzzle.Services;

namespace WordPuzzle.Audio
{
    /// <summary>
    /// Background music. Drives AudioManager's Background channel source - the same source the
    /// mixer routing and channel setup already point at - rather than a second one of its own.
    ///
    /// It sets the clip directly instead of calling AudioManager.Play() because that path is
    /// built for one-shots: it resets loop/volume/pitch per call and has no notion of a track
    /// ending. Tracks are played one after another in a shuffled order and the list wraps, so
    /// the playlist never runs out and never repeats the same track twice in a row.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        [Header("Playlist (Direct References - No Resources.Load)")]
        [SerializeField] private List<AudioClip> tracks = new List<AudioClip>();

        [Header("Playback")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.35f;
        [SerializeField] private bool shuffle = true;
        [SerializeField] private float crossfadeSeconds = 1.2f;

        private const string PrefKeyMusicEnabled = "MusicEnabled";

        private AudioSource _source;
        private WondersOfWordGameModel _gameModel;

        /// <summary>False on the splash and the main menu, where the playlist stays silent.</summary>
        private bool _inGameplay;
        private readonly List<int> _order = new List<int>();
        private int _orderIndex = -1;
        private float _fadeTarget;

        /// <summary>Whether music should play. Persisted across sessions.</summary>
        public bool MusicEnabled { get; private set; } = true;

        public string CurrentTrackName =>
            _source != null && _source.clip != null ? _source.clip.name : string.Empty;

        private void Awake()
        {
            ResolveSource();

            MusicEnabled = GameStorage.GetInt(PrefKeyMusicEnabled, 1) == 1;
            _fadeTarget = MusicEnabled ? volume : 0f;
            if (_source != null) _source.volume = _fadeTarget;

            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<MusicPlayer>())
            {
                ServiceLocator.Current.Register<MusicPlayer>(this);
            }
        }

        private void OnDestroy()
        {
            OnDestroyModelBinding();
            if (ServiceLocator.Current != null
                && ServiceLocator.Current.Has<MusicPlayer>()
                && ServiceLocator.Current.Get<MusicPlayer>() == this)
            {
                ServiceLocator.Current.Unregister<MusicPlayer>();
            }
        }

        private void Start()
        {
            // AudioManager registers in Awake, so the Background source is only reliably
            // reachable from Start onwards.
            ResolveSource();
            BuildOrder();

            // Music belongs to gameplay only - the splash and the menu stay silent - so
            // playback follows game state rather than starting on load.
            if (ServiceLocator.Current != null && ServiceLocator.Current.Has<WondersOfWordGameModel>())
            {
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();
                _gameModel.State.Bind(this, OnGameStateChanged);
                ApplyStateGate(_gameModel.State.Value);
            }
            else
            {
                // No model to follow (a test scene, say): behave as before rather than
                // leaving the playlist permanently silent.
                _inGameplay = true;
                if (MusicEnabled) PlayNext();
            }
        }

        private void OnDestroyModelBinding()
        {
            if (_gameModel != null)
            {
                _gameModel.State.Unbind(OnGameStateChanged);
                _gameModel = null;
            }
        }

        private void OnGameStateChanged(GameState state) => ApplyStateGate(state);

        /// <summary>
        /// Starts the playlist on entering gameplay and stops it on returning to the menu.
        /// Paused and LevelComplete still count as in-game: the track should carry across a
        /// pause popup and the victory card rather than cutting out and restarting.
        /// </summary>
        private void ApplyStateGate(GameState state)
        {
            bool shouldPlay = state != GameState.MainMenu;
            if (shouldPlay == _inGameplay) return;

            _inGameplay = shouldPlay;

            if (!shouldPlay)
            {
                _fadeTarget = 0f;
                if (_source != null) _source.Stop();
                return;
            }

            _fadeTarget = MusicEnabled ? volume : 0f;
            if (MusicEnabled) PlayNext();
        }

        /// <summary>
        /// Prefers the Background channel source from AudioManager (already wired to the
        /// mixer group by scene setup) and only falls back to a local source when this
        /// component is used outside the standard Managers/Audio rig.
        /// </summary>
        private void ResolveSource()
        {
            if (_source != null) return;

            if (ServiceLocator.Current != null && ServiceLocator.Current.Has<AudioManager>())
            {
                _source = ServiceLocator.Current.Get<AudioManager>().GetSource(AudioType.Background);
            }

            if (_source == null) _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();

            _source.playOnAwake = false;
            _source.loop = false; // Advancing to the next track is what makes it a playlist.
            _source.spatialBlend = 0f;
        }

        private void Update()
        {
            if (_source == null) return;

            // Fade rather than a hard cut, so toggling music off mid-phrase is not jarring.
            if (crossfadeSeconds > 0.01f)
            {
                _source.volume = Mathf.MoveTowards(
                    _source.volume, _fadeTarget, (volume / crossfadeSeconds) * Time.unscaledDeltaTime);
            }
            else
            {
                _source.volume = _fadeTarget;
            }

            // Advance when the current track ends. Checked here rather than with a coroutine
            // so a paused game (timeScale 0) still hands over to the next track.
            if (_inGameplay && MusicEnabled && !_source.isPlaying && _source.clip != null)
            {
                PlayNext();
            }
        }

        public void SetMusicEnabled(bool enabled)
        {
            MusicEnabled = enabled;
            GameStorage.SetInt(PrefKeyMusicEnabled, enabled ? 1 : 0);
            GameStorage.Save();

            _fadeTarget = enabled ? volume : 0f;

            if (enabled)
            {
                // Only resumes if we are actually in a level: switching music on from the
                // menu should not start a track over the menu.
                if (_inGameplay && _source != null && !_source.isPlaying) PlayNext();
            }
            else if (_source != null)
            {
                // Stopped outright, not paused: the fade is cosmetic and a silent source
                // still burns a voice.
                _source.Stop();
                _source.volume = 0f;
            }
        }

        public bool ToggleMusic()
        {
            SetMusicEnabled(!MusicEnabled);
            return MusicEnabled;
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            _fadeTarget = MusicEnabled ? volume : 0f;
        }

        /// <summary>Skips to the next track in the playlist.</summary>
        public void PlayNext()
        {
            if (_source == null || tracks == null) return;

            if (_order.Count == 0) BuildOrder();
            if (_order.Count == 0) return;

            _orderIndex++;
            if (_orderIndex >= _order.Count)
            {
                // Reshuffle on wrap so the playlist order is not identical every cycle.
                _orderIndex = 0;
                if (shuffle) BuildOrder();
            }

            AudioClip clip = tracks[_order[_orderIndex]];
            if (clip == null) return;

            _source.clip = clip;
            _source.volume = MusicEnabled ? volume : 0f;
            _source.Play();
        }

        private void BuildOrder()
        {
            if (tracks == null) return;

            int lastPlayed = _orderIndex >= 0 && _orderIndex < _order.Count ? _order[_orderIndex] : -1;
            _order.Clear();

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] != null) _order.Add(i);
            }

            if (!shuffle || _order.Count < 2) return;

            for (int i = _order.Count - 1; i > 0; i--)
            {
                int k = Random.Range(0, i + 1);
                (_order[i], _order[k]) = (_order[k], _order[i]);
            }

            // Avoid replaying the track that just finished as the first of the new cycle.
            if (_order.Count > 1 && _order[0] == lastPlayed)
            {
                (_order[0], _order[_order.Count - 1]) = (_order[_order.Count - 1], _order[0]);
            }
        }

#if UNITY_EDITOR
        public void EditorSetTracks(List<AudioClip> clips)
        {
            tracks = clips;
        }
#endif
    }
}
