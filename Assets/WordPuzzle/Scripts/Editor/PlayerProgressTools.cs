#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using WordPuzzle.Models;

namespace WordPuzzle.Editor
{
    /// <summary>
    /// Progress is persisted to PlayerPrefs, so the Editor resumes wherever the last play
    /// session finished. That is correct for players but makes testing look like the game
    /// starts on a random level.
    /// </summary>
    public static class PlayerProgressTools
    {
        [MenuItem("WordPuzzle/Reset Player Progress")]
        public static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog(
                    "Reset player progress",
                    "Clears the saved level, coins and sound setting.\n\n" +
                    "The next play session starts on level 1.",
                    "Reset", "Cancel"))
            {
                return;
            }

            PlayerPrefs.DeleteKey(WondersOfWordGameModel.KEY_CURRENT_LEVEL_INDEX);
            PlayerPrefs.DeleteKey(WondersOfWordGameModel.KEY_COINS);
            PlayerPrefs.DeleteKey("SoundEnabled");
            PlayerPrefs.Save();

            Debug.Log("[WordPuzzle] Player progress reset - next run starts on level 1.");
        }

        [MenuItem("WordPuzzle/Show Saved Progress")]
        public static void ShowProgress()
        {
            int level = PlayerPrefs.GetInt(WondersOfWordGameModel.KEY_CURRENT_LEVEL_INDEX, 1);
            int coins = PlayerPrefs.GetInt(WondersOfWordGameModel.KEY_COINS, 100);
            bool sound = PlayerPrefs.GetInt("SoundEnabled", 1) == 1;

            EditorUtility.DisplayDialog("Saved progress",
                $"Level: {level}\nCoins: {coins}\nSound: {(sound ? "on" : "off")}", "OK");
        }
    }
}
#endif
