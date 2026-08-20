using System;
using UnityEngine;
using ServiceLocatorFramework;
using WordPuzzle.Gameplay;

namespace WordPuzzle.Services
{
    /// <summary>Which way round the screen is, in the only sense the layout cares about.</summary>
    public enum ScreenLayout
    {
        /// <summary>Taller than wide. Grid above, wheel below.</summary>
        Portrait,

        /// <summary>Wider than tall. Grid and wheel side by side.</summary>
        Landscape
    }

    /// <summary>
    /// Tracks the screen shape and tells everyone when it changes.
    ///
    /// A single source of truth on purpose: the grid, the wheel and the HUD all need to
    /// rearrange at the same moment, and three independent aspect checks would disagree for a
    /// frame and leave the wheel overlapping the grid. Polling rather than
    /// Screen.orientation because a resized desktop browser window changes shape without any
    /// device rotation, and that is the common case on CrazyGames.
    /// </summary>
    public class LayoutService : MonoBehaviour
    {
        /// <summary>
        /// Aspect below which the screen counts as portrait. Slightly under 1 so a square-ish
        /// window settles on one answer instead of flickering between the two.
        /// </summary>
        private const float LandscapeThreshold = 1.05f;

        private static ScreenLayout _current = ScreenLayout.Portrait;
        private static int _lastWidth;
        private static int _lastHeight;

        /// <summary>Raised after the layout changes. Listeners rebuild in response.</summary>
        public static event Action<ScreenLayout> LayoutChanged;

        public static ScreenLayout Current => _current;
        public static bool IsLandscape => _current == ScreenLayout.Landscape;

        private void Awake()
        {
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<LayoutService>())
            {
                ServiceLocator.Current.Register<LayoutService>(this);
            }

            Evaluate(force: true);
            EnsureBackgroundFitter();
        }

        /// <summary>
        /// Attaches the background cover-fitter if the scene does not already have one.
        ///
        /// Scene setup adds it too, but only for scenes rebuilt after it existed - an older
        /// scene silently kept a fixed-scale background and showed the camera's clear colour
        /// down the sides in landscape. Self-installing means the layout cannot be half-applied.
        /// </summary>
        private static void EnsureBackgroundFitter()
        {
            if (FindObjectOfType<BackgroundFitter>() != null) return;

            GameObject background = GameObject.Find("SceneBackground");
            if (background == null) return;

            if (background.GetComponent<SpriteRenderer>() == null) return;

            background.AddComponent<BackgroundFitter>();
            Debug.Log("[LayoutService] Added BackgroundFitter to SceneBackground.");
        }

        private void OnDestroy()
        {
            if (ServiceLocator.Current != null
                && ServiceLocator.Current.Has<LayoutService>()
                && ServiceLocator.Current.Get<LayoutService>() == this)
            {
                ServiceLocator.Current.Unregister<LayoutService>();
            }
        }

        private void Update()
        {
            // Cheap: two int comparisons, and only does real work when the window actually
            // changed size.
            if (Screen.width == _lastWidth && Screen.height == _lastHeight) return;
            Evaluate(force: false);
        }

        private static void Evaluate(bool force)
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            if (_lastHeight <= 0) return;

            float aspect = _lastWidth / (float)_lastHeight;
            ScreenLayout layout = aspect >= LandscapeThreshold ? ScreenLayout.Landscape : ScreenLayout.Portrait;

            if (!force && layout == _current) return;

            _current = layout;
            LayoutChanged?.Invoke(_current);
        }
    }
}
