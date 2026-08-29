#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WordPuzzle.Editor
{
    /// <summary>
    /// One-click chores that would otherwise be manual clicking around the Inspector.
    /// </summary>
    public static class ProjectMaintenance
    {
        /// <summary>
        /// Strips components whose script no longer exists - the "Missing (Mono Script)" rows
        /// left behind when a script is deleted while the scene still references it. Unity
        /// cannot fix these from the Inspector without selecting each object by hand.
        /// </summary>
        [MenuItem("Aurora Words/Clean Missing Scripts In Scene")]
        public static void CleanMissingScripts()
        {
            int removed = 0;
            int objectsTouched = 0;

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    if (count <= 0) continue;

                    removed += count;
                    objectsTouched++;
                }
            }

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"<color=green>[Aurora Words]</color> Removed {removed} missing script(s) " +
                          $"from {objectsTouched} object(s). Save the scene to keep the change.");
            }
            else
            {
                Debug.Log("[Aurora Words] No missing scripts in the active scene.");
            }
        }

        /// <summary>
        /// Regenerates the campaign with the settings this game actually ships with, so the
        /// database can be rebuilt without opening the generator window and re-entering them.
        /// Replaces the existing levels outright - that is the point, since the database still
        /// holds words from before the list was cleaned.
        /// </summary>
        [MenuItem("Aurora Words/Regenerate Levels (1000, Short To Long)")]
        public static void RegenerateLevels()
        {
            const string wordListPath = "Assets/WordPuzzle/Resources/word_list_targets.txt";

            var wordList = AssetDatabase.LoadAssetAtPath<TextAsset>(wordListPath);
            if (wordList == null)
            {
                Debug.LogError($"[Aurora Words] {wordListPath} not found.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Regenerate levels?",
                    "This replaces every level in LevelDatabase.asset using the cleaned target " +
                    "word list.\n\nPlayers mid-campaign will get different puzzles at the same " +
                    "level numbers, and saved mid-level state is discarded by its fingerprint.",
                    "Regenerate", "Cancel"))
            {
                return;
            }

            LevelGeneratorWindow.GenerateDefaultCampaign(wordList);
        }
    }
}
#endif
