using UnityEngine;

namespace WordPuzzle.Services
{
    /// <summary>
    /// Every persisted value in the game goes through here rather than touching PlayerPrefs
    /// directly, so where a save lives is one decision made in one place.
    ///
    /// Today that is always PlayerPrefs. The indirection is kept because the alternatives are
    /// real and platform-specific - a portal's own storage on web, or a cloud save on mobile -
    /// and swapping one in means editing this file rather than the twelve call sites that
    /// read and write progress.
    /// </summary>
    public static class GameStorage
    {
        public static int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(key, defaultValue);

        public static void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public static float GetFloat(string key, float defaultValue = 0f) => PlayerPrefs.GetFloat(key, defaultValue);

        public static void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public static string GetString(string key, string defaultValue = "") => PlayerPrefs.GetString(key, defaultValue);

        public static void SetString(string key, string value) => PlayerPrefs.SetString(key, value);

        public static bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public static void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);

        public static void DeleteAll() => PlayerPrefs.DeleteAll();

        /// <summary>Flushes pending writes to disk.</summary>
        public static void Save() => PlayerPrefs.Save();
    }
}
