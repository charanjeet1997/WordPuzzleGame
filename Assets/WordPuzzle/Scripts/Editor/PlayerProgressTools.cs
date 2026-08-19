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
        [MenuItem("Aurora Words/Reset Player Progress")]
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
            PlayerPrefs.DeleteKey("WordPuzzle_CurrentLevel");
            PlayerPrefs.DeleteKey("WordPuzzle_HighestLevel");
            PlayerPrefs.DeleteKey("WordPuzzle_Coins");
            PlayerPrefs.DeleteKey("WordPuzzle_TotalScore");
            PlayerPrefs.DeleteKey("WordPuzzle_TotalWords");
            PlayerPrefs.DeleteKey("WordPuzzle_TotalBonusWords");
            for (int i = 1; i <= 200; i++)
            {
                PlayerPrefs.DeleteKey("WordPuzzle_Stars_Lvl_" + i);
                PlayerPrefs.DeleteKey("WordPuzzle_State_Lvl_" + i);
            }
            PlayerPrefs.DeleteKey("SoundEnabled");
            PlayerPrefs.Save();

            Debug.Log("[Aurora Words] Player progress reset - next run starts on level 1.");
        }

        [MenuItem("Aurora Words/Show Saved Progress")]
        public static void ShowProgress()
        {
            int level = PlayerPrefs.GetInt("WordPuzzle_CurrentLevel", PlayerPrefs.GetInt(WondersOfWordGameModel.KEY_CURRENT_LEVEL_INDEX, 1));
            int highestLevel = PlayerPrefs.GetInt("WordPuzzle_HighestLevel", level);
            int coins = PlayerPrefs.GetInt("WordPuzzle_Coins", PlayerPrefs.GetInt(WondersOfWordGameModel.KEY_COINS, 100));
            int score = PlayerPrefs.GetInt("WordPuzzle_TotalScore", 0);
            int words = PlayerPrefs.GetInt("WordPuzzle_TotalWords", 0);
            bool sound = PlayerPrefs.GetInt("SoundEnabled", 1) == 1;

            EditorUtility.DisplayDialog("Saved progress",
                $"Current Level: {level}\nHighest Level: {highestLevel}\nCoins: {coins}\nTotal Score: {score}\nWords Found: {words}\nSound: {(sound ? "on" : "off")}", "OK");
        }
    }
}
#endif
