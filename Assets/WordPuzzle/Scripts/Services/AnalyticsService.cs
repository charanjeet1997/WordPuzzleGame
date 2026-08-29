using System.Collections.Generic;
using UnityEngine;

namespace WordPuzzle.Services
{
    /// <summary>
    /// Game events, in one place, provider-agnostic.
    ///
    /// No provider is attached yet: events are raised, logged in the editor, and dropped in a
    /// build. Firebase or any other backend subscribes to EventTracked without a single call
    /// site changing.
    ///
    /// Instrumenting now rather than later matters because a publisher's first question is
    /// about D1 retention and level drop-off, and that data only exists if it was being
    /// recorded before anyone asked.
    /// </summary>
    public static class AnalyticsService
    {
        /// <summary>Prints every event to the console. Off by default; noisy but useful.</summary>
        public static bool LogToConsole =
#if UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>
        /// Raised for every event, so a provider can subscribe without this class knowing
        /// anything about it. Firebase would attach here.
        /// </summary>
        public static event System.Action<string, Dictionary<string, object>> EventTracked;

        public static void Track(string eventName, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(eventName)) return;

            parameters ??= new Dictionary<string, object>();
            EventTracked?.Invoke(eventName, parameters);

            if (!LogToConsole) return;

            var sb = new System.Text.StringBuilder("[Analytics] ").Append(eventName);
            foreach (var pair in parameters) sb.Append("  ").Append(pair.Key).Append('=').Append(pair.Value);
            Debug.Log(sb.ToString());
        }

        // ------------------------------------------------------------------ game events
        // Named and shaped to match what a publisher actually asks for: where players stop,
        // how long levels take, and which optional systems get used.

        public static void LevelStarted(int level, string mode) =>
            Track("level_started", new Dictionary<string, object> { { "level", level }, { "mode", mode } });

        public static void LevelCompleted(int level, string mode, float seconds, int hintsUsed) =>
            Track("level_completed", new Dictionary<string, object>
            {
                { "level", level }, { "mode", mode },
                { "seconds", Mathf.RoundToInt(seconds) }, { "hints_used", hintsUsed }
            });

        public static void HintUsed(int level, int coinsLeft) =>
            Track("hint_used", new Dictionary<string, object> { { "level", level }, { "coins_left", coinsLeft } });

        public static void ShuffleUsed(int level) =>
            Track("shuffle_used", new Dictionary<string, object> { { "level", level } });

        public static void BonusWordFound(string word, int level) =>
            Track("bonus_word_found", new Dictionary<string, object> { { "word", word }, { "level", level } });

        public static void ModeSelected(string mode) =>
            Track("mode_selected", new Dictionary<string, object> { { "mode", mode } });

        public static void CollectionOpened(int discovered, int total) =>
            Track("collection_opened", new Dictionary<string, object>
            {
                { "discovered", discovered }, { "total", total }
            });

        /// <summary>The onboarding funnel: where first-time players drop out.</summary>
        public static void OnboardingStep(string step) =>
            Track("onboarding_step", new Dictionary<string, object> { { "step", step } });
    }
}
