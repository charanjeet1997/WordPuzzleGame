using UnityEngine;

namespace WordPuzzle.Services
{
    /// <summary>
    /// Every persisted value in the game goes through here rather than touching PlayerPrefs
    /// directly, so where a save lives is one decision made in one place.
    ///
    /// On CrazyGames it routes to their Data module, which syncs a signed-in player's progress
    /// across devices and migrates guest data into the account on login - their docs are
    /// explicit that a game should rely on it rather than keeping its own local store.
    /// Everywhere else (Android, itch.io, the editor) it is PlayerPrefs.
    ///
    /// The API is deliberately PlayerPrefs-shaped: both backends expose the same operations,
    /// so no call site has to care which one is active.
    /// </summary>
    public static class GameStorage
    {
#if CRAZYGAMES
        // Only ever true in a CrazyGames WebGL build: the SDK reports unavailable elsewhere,
        // and falling back keeps the editor and other portals working from PlayerPrefs.
        private static bool UseCrazyData =>
            CrazyGames.CrazySDK.IsAvailable && CrazyGames.CrazySDK.IsInitialized;
#else
        private const bool UseCrazyData = false;
#endif

        public static int GetInt(string key, int defaultValue = 0)
        {
#if CRAZYGAMES
            if (UseCrazyData) return CrazyGames.CrazySDK.Data.GetInt(key, defaultValue);
#endif
            return PlayerPrefs.GetInt(key, defaultValue);
        }

        public static void SetInt(string key, int value)
        {
#if CRAZYGAMES
            if (UseCrazyData)
            {
                CrazyGames.CrazySDK.Data.SetInt(key, value);
                return;
            }
#endif
            PlayerPrefs.SetInt(key, value);
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
#if CRAZYGAMES
            if (UseCrazyData) return CrazyGames.CrazySDK.Data.GetFloat(key, defaultValue);
#endif
            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        public static void SetFloat(string key, float value)
        {
#if CRAZYGAMES
            if (UseCrazyData)
            {
                CrazyGames.CrazySDK.Data.SetFloat(key, value);
                return;
            }
#endif
            PlayerPrefs.SetFloat(key, value);
        }

        public static string GetString(string key, string defaultValue = "")
        {
#if CRAZYGAMES
            if (UseCrazyData) return CrazyGames.CrazySDK.Data.GetString(key, defaultValue);
#endif
            return PlayerPrefs.GetString(key, defaultValue);
        }

        public static void SetString(string key, string value)
        {
#if CRAZYGAMES
            if (UseCrazyData)
            {
                CrazyGames.CrazySDK.Data.SetString(key, value);
                return;
            }
#endif
            PlayerPrefs.SetString(key, value);
        }

        public static bool HasKey(string key)
        {
#if CRAZYGAMES
            if (UseCrazyData) return CrazyGames.CrazySDK.Data.HasKey(key);
#endif
            return PlayerPrefs.HasKey(key);
        }

        public static void DeleteKey(string key)
        {
#if CRAZYGAMES
            if (UseCrazyData)
            {
                CrazyGames.CrazySDK.Data.DeleteKey(key);
                return;
            }
#endif
            PlayerPrefs.DeleteKey(key);
        }

        public static void DeleteAll()
        {
#if CRAZYGAMES
            if (UseCrazyData)
            {
                CrazyGames.CrazySDK.Data.DeleteAll();
                return;
            }
#endif
            PlayerPrefs.DeleteAll();
        }

        /// <summary>
        /// Flushes pending writes. A no-op on CrazyGames, which debounces and persists on its
        /// own schedule - calling their setters is already the commit.
        /// </summary>
        public static void Save()
        {
#if CRAZYGAMES
            if (UseCrazyData) return;
#endif
            PlayerPrefs.Save();
        }
    }
}
