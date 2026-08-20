using UnityEngine;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Models;

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

            if (ServiceLocator.Current != null && ServiceLocator.Current.Has<WondersOfWordGameModel>())
            {
                _model = ServiceLocator.Current.Get<WondersOfWordGameModel>();
                _model.State.Bind(this, OnGameStateChanged);
                OnGameStateChanged(_model.State.Value);
            }
        }

        private void OnDestroy()
        {
            if (_model != null)
            {
                _model.State.Unbind(OnGameStateChanged);
                _model = null;
            }

            // Leaving with the session open would inflate playtime for as long as the tab
            // stays around.
            if (_gameplayRunning) StopGameplay();
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
