using UnityEngine;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Models;
using WordPuzzle.Audio;

namespace WordPuzzle.Services
{
    /// <summary>
    /// Initialises the CrazyGames SDK and reports gameplay state to it.
    ///
    /// The portal requires the game to tell it when play actually starts and stops - it uses
    /// that to decide when an ad break is acceptable and to measure real playtime, which is
    /// the number their promotion decisions are based on. Reporting it from the game's own
    /// state machine keeps it honest: menus, the pause popup and the victory card are not
    /// gameplay, and none of them should count.
    ///
    /// Compiled out entirely except in a CrazyGames WebGL build.
    /// </summary>
    public class CrazyGamesBridge : MonoBehaviour
    {
#if CRAZYGAMES
        private WondersOfWordGameModel _model;
        private bool _gameplayRunning;

        private void Start()
        {
            // Nothing else may touch the SDK until this completes; GameStorage checks
            // IsInitialized for exactly that reason and falls back to PlayerPrefs until then.
            CrazyGames.CrazySDK.Init(OnSdkReady);
        }

        private void OnSdkReady()
        {
            Debug.Log("[CrazyGames] SDK initialised.");

            // The portal can mute a game from its own chrome - during an ad, or when the
            // player mutes the page. Honouring it is a submission option ("supports
            // CrazyGames muting audio through SDK") and a hard rule while an ad is running.
            CrazyGames.CrazySDK.Game.AddSettingsChangeListener(OnPortalSettingsChanged);
            ApplyPortalMute(CrazyGames.CrazySDK.Game.Settings);

            if (ServiceLocator.Current != null && ServiceLocator.Current.Has<WondersOfWordGameModel>())
            {
                _model = ServiceLocator.Current.Get<WondersOfWordGameModel>();
                _model.State.Bind(this, OnGameStateChanged);
                OnGameStateChanged(_model.State.Value);
            }
        }

        private void OnDestroy()
        {
            CrazyGames.CrazySDK.Game.RemoveSettingsChangeListener(OnPortalSettingsChanged);

            if (_model != null)
            {
                _model.State.Unbind(OnGameStateChanged);
                _model = null;
            }

            // Leaving with the session open would inflate playtime for as long as the tab
            // stays around.
            if (_gameplayRunning) StopGameplay();
        }

        private void OnPortalSettingsChanged(CrazyGames.GameSettings settings) => ApplyPortalMute(settings);

        /// <summary>
        /// Mutes at the listener rather than per source: the game has music, one-shot SFX and
        /// UI clicks on separate channels, and the portal's mute has to silence all of them
        /// without disturbing the player's own sound and music settings, which must survive
        /// unmuting.
        /// </summary>
        private void ApplyPortalMute(CrazyGames.GameSettings settings)
        {
            if (settings == null) return;

            AudioListener.pause = settings.muteAudio;

            if (settings.muteAudio)
            {
                AudioListener.volume = 0f;
                return;
            }

            // Unmuting restores the player's own setting rather than forcing full volume:
            // AudioManager drives the same listener for its SOUND toggle, and a player who
            // turned sound off must not have it switched back on by the portal.
            bool soundOn = true;
            if (ServiceLocator.Current != null && ServiceLocator.Current.Has<Audio.AudioManager>())
            {
                soundOn = ServiceLocator.Current.Get<Audio.AudioManager>().SoundEnabled;
            }

            AudioListener.volume = soundOn ? 1f : 0f;
        }

        private void OnGameStateChanged(GameState state)
        {
            // Paused and LevelComplete deliberately stop the session: the player is reading a
            // card or looking at a menu, and counting that as play would misreport engagement.
            if (state == GameState.Playing) StartGameplay();
            else StopGameplay();
        }

        private void StartGameplay()
        {
            if (_gameplayRunning) return;

            _gameplayRunning = true;
            CrazyGames.CrazySDK.Game.GameplayStart();
        }

        private void StopGameplay()
        {
            if (!_gameplayRunning) return;

            _gameplayRunning = false;
            CrazyGames.CrazySDK.Game.GameplayStop();
        }

        /// <summary>
        /// Marks a moment of genuine satisfaction. The portal uses these to learn where a
        /// player is enjoying themselves, so it goes on level completion rather than on
        /// every solved word, which would flatten the signal.
        /// </summary>
        public static void ReportHappyMoment()
        {
            if (CrazyGames.CrazySDK.IsAvailable) CrazyGames.CrazySDK.Game.HappyTime();
        }
#else
        /// <summary>No-op off CrazyGames, so call sites need no platform guards.</summary>
        public static void ReportHappyMoment() { }
#endif
    }
}
