using System;
using UnityEngine;

namespace WordPuzzle.Models
{
    /// <summary>How the player is playing the campaign.</summary>
    public enum GameMode
    {
        /// <summary>No clock. Solve at your own pace.</summary>
        Classic = 0,

        /// <summary>Same puzzles, timed. The clock counts up and a best time is kept per level.</summary>
        TimeTrial = 1,

        /// <summary>
        /// Levels chained under one shared clock. Each solved word adds seconds; the run ends
        /// when the clock does, and the score is how far you got.
        /// </summary>
        Endless = 2
    }

    /// <summary>
    /// The mode currently being played, and the key suffix that keeps each mode's progress
    /// apart. Every saved value that describes campaign position - level reached, stars, saved
    /// level state, best times - is written under a mode-scoped key, so reaching level 40 in
    /// Classic leaves Time Trial sitting wherever it was.
    ///
    /// Static rather than a service because persistence code reads it while building key names,
    /// long before any ServiceLocator lookup would be convenient.
    /// </summary>
    public static class GameModeContext
    {
        private const string PrefKeyLastMode = "LastGameMode";

        private static GameMode _current = GameMode.Classic;
        private static bool _loaded;

        /// <summary>Raised after the mode changes, so progress holders can reload their values.</summary>
        public static event Action<GameMode> ModeChanged;

        public static GameMode Current
        {
            get
            {
                if (!_loaded)
                {
                    _current = (GameMode)PlayerPrefs.GetInt(PrefKeyLastMode, (int)GameMode.Classic);
                    _loaded = true;
                }
                return _current;
            }
        }

        /// <summary>
        /// Appended to every progress-related PlayerPrefs key. Classic deliberately maps to an
        /// empty suffix so existing saves keep working - a player mid-campaign does not lose
        /// their position when this feature ships.
        /// </summary>
        public static string KeySuffix => SuffixFor(Current);

        /// <summary>True when a clock is on screen, in either timed mode.</summary>
        public static bool IsTimed => Current != GameMode.Classic;

        /// <summary>True when the clock counts down and can end the run.</summary>
        public static bool IsCountdown => Current == GameMode.Endless;

        public static void SetMode(GameMode mode)
        {
            if (_loaded && _current == mode) return;

            _current = mode;
            _loaded = true;
            PlayerPrefs.SetInt(PrefKeyLastMode, (int)mode);
            PlayerPrefs.Save();

            ModeChanged?.Invoke(mode);
        }

        /// <summary>Mode-scoped key for a base PlayerPrefs name.</summary>
        public static string Key(string baseKey) => baseKey + KeySuffix;

        /// <summary>
        /// Key for a mode other than the active one. Lets a UI read another mode's saved
        /// progress without switching to it - browsing must not disturb saved state.
        /// </summary>
        public static string KeyFor(GameMode mode, string baseKey) => baseKey + SuffixFor(mode);

        /// <summary>
        /// Classic maps to an empty suffix so saves written before modes existed keep working -
        /// a player mid-campaign does not lose their position when this ships.
        /// </summary>
        private static string SuffixFor(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.TimeTrial: return "_TimeTrial";
                case GameMode.Endless: return "_Endless";
                default: return string.Empty;
            }
        }

        public static string DisplayName(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.TimeTrial: return "TIME TRIAL";
                case GameMode.Endless: return "ENDLESS";
                default: return "CLASSIC";
            }
        }

        public static string Description(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.TimeTrial:
                    return "Same puzzles, against the clock. Your best time is kept for every level.";
                case GameMode.Endless:
                    return "One clock, level after level. Every word you find buys more time.";
                default:
                    return "Solve at your own pace. No timer, no pressure.";
            }
        }
    }
}
