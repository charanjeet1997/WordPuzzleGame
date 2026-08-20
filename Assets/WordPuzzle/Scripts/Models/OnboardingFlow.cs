using UnityEngine;
using WordPuzzle.Services;

namespace WordPuzzle.Models
{
    /// <summary>
    /// Where the player is in first-run teaching. Two steps only, and each is triggered by the
    /// player finishing something rather than by a screen appearing - a prompt shown before the
    /// player has any reason to care is a prompt they dismiss without reading.
    /// </summary>
    public enum OnboardingStep
    {
        /// <summary>Nothing learned yet. The wheel hint is running in the level.</summary>
        LearnSwipe = 0,

        /// <summary>
        /// Swipe learned and a level cleared, so the collection now has words in it and is
        /// worth pointing at. The menu button pulses until they open it.
        /// </summary>
        FindCollection = 1,

        /// <summary>Done. Nothing is highlighted again.</summary>
        Complete = 2
    }

    /// <summary>
    /// Tracks first-run progress across screens. Static because it is read from the level, the
    /// victory card and the main menu, all of which are built and destroyed independently.
    /// </summary>
    public static class OnboardingFlow
    {
        private const string PrefKeyStep = "OnboardingStep";

        public static OnboardingStep Step
        {
            get => (OnboardingStep)GameStorage.GetInt(PrefKeyStep, (int)OnboardingStep.LearnSwipe);
            private set
            {
                GameStorage.SetInt(PrefKeyStep, (int)value);
                GameStorage.Save();
            }
        }

        public static bool IsComplete => Step == OnboardingStep.Complete;

        /// <summary>Called once the player has solved words unaided.</summary>
        public static void MarkSwipeLearned()
        {
            if (Step == OnboardingStep.LearnSwipe) Step = OnboardingStep.FindCollection;
        }

        /// <summary>Called when the collection screen is opened for the first time.</summary>
        public static void MarkCollectionSeen()
        {
            if (Step == OnboardingStep.FindCollection) Step = OnboardingStep.Complete;
        }

        /// <summary>Replays the whole flow. Wired to Reset Progress.</summary>
        public static void Clear() => GameStorage.DeleteKey(PrefKeyStep);
    }
}
