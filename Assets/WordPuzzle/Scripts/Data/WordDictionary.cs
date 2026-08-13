using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordPuzzle.Data
{
    public static class WordDictionary
    {
        /// <summary>Newline-separated word list under Resources, without the file extension.</summary>
        private const string WordListResourcePath = "word_list";

        private static HashSet<string> _words;

        /// <summary>
        /// The active word list. Loads the Resources word list on first use and falls back to
        /// the small built-in set if that asset is missing, so bonus-word detection still works
        /// in a project that has not generated a list yet.
        /// </summary>
        private static HashSet<string> Words
        {
            get
            {
                if (_words != null) return _words;

                TextAsset asset = Resources.Load<TextAsset>(WordListResourcePath);
                if (asset == null)
                {
                    Debug.LogWarning($"[WordDictionary] Resources/{WordListResourcePath}.txt not found; " +
                                     $"falling back to the built-in {BuiltInWords.Count}-word list.");
                    _words = BuiltInWords;
                    return _words;
                }

                _words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in asset.text.Split('\n', '\r'))
                {
                    string word = line.Trim();
                    if (word.Length >= 3) _words.Add(word);
                }

                if (_words.Count == 0) _words = BuiltInWords;
                return _words;
            }
        }

        private static readonly HashSet<string> BuiltInWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 3-letter words
            "CAT", "DOG", "BAT", "RAT", "SUN", "FUN", "RUN", "MAP", "PEN", "PIN",
            "CUP", "CAP", "HAT", "HOT", "BUS", "BUG", "BOY", "TOY", "KEY", "BAG",
            "RED", "BED", "BOX", "FOX", "FAN", "VAN", "WIN", "CAN", "PAN", "TOP",
            "LOG", "PIG", "COW", "JAM", "JAR", "NET", "WEB", "NIP", "LIP", "ARM",
            "LEG", "EYE", "EAR", "ICE", "SEA", "SKY", "AIR", "FLY", "ANT", "BEE",
            "BOW", "ROW", "SEW", "CUT", "FIT", "HIT", "SIT", "GET", "LET", "SET",
            "BIG", "SAD", "NEW", "OLD", "DRY", "WET", "FAR", "NEAR", "NOW", "ONE",
            "TWO", "TEN", "SIX", "DAY", "SON", "MAN", "ZOO", "ZIP", "PET", "POT",

            // 4-letter words
            "CATS", "DOGS", "BIRD", "FISH", "DUCK", "FROG", "LION", "BEAR", "WOLF", "DEER",
            "STAR", "MOON", "SUNS", "RAIN", "SNOW", "WIND", "FIRE", "WAVE", "PARK", "TREE",
            "ROSE", "LEAF", "SEED", "BOOK", "PAGE", "DESK", "BALL", "GAME", "PLAY", "SONG",
            "SING", "RING", "KING", "WISH", "HOPE", "LOVE", "LIFE", "TIME", "HOME", "CITY",
            "TOWN", "ROAD", "SHIP", "BOAT", "CAR", "AUTO", "TRAIN", "BIKE", "DOOR", "WALL",
            "ROOF", "ROOM", "SOFA", "LAMP", "CAKE", "MILK", "SOUP", "RICE", "MEAT", "FISH",
            "APPLE", "PEAR", "PLUM", "CORN", "BEAN", "PINK", "BLUE", "GOLD", "FAST", "SLOW",
            "HIGH", "LOW", "WARM", "COLD", "COOL", "NICE", "GOOD", "FINE", "BEST", "FREE",
            "OPEN", "EASY", "SAFE", "TALL", "LONG", "TINY", "SOFT", "HARD", "SWEET", "PURE",

            // 5-letter words
            "APPLE", "PEACH", "GRAPE", "LEMON", "MELON", "WATER", "OCEAN", "RIVER", "BEACH", "CLOUD",
            "STORM", "PLANT", "FLOWER", "GRASS", "TREES", "EARTH", "WORLD", "SPACE", "PLANET", "LIGHT",
            "SHINE", "NIGHT", "DREAM", "HEART", "SMILE", "LAUGH", "HAPPY", "PEACE", "MUSIC", "DANCE",
            "GUITAR", "PIANO", "HOUSE", "CABIN", "GLASS", "CLOCK", "PAPER", "CHAIR", "TABLE", "BREAD",
            "CHEESE", "HONEY", "SWEET", "FRUIT", "SUGAR", "FLOUR", "CRISP", "SMART", "BRAVE", "CLEVER",

            // 6-letter words
            "CASTLE", "GARDEN", "FOREST", "ISLAND", "SUMMER", "SPRING", "WINTER", "AUTUMN", "SUNSET", "SUNRISE",
            "BRIDGE", "STREET", "FLOWER", "PLANET", "SILVER", "GOLDEN", "YELLOW", "ORANGE", "PURPLE", "FLOWER",
            "GUITAR", "VIOLIN", "PENCIL", "CAMERA", "WINDOW", "BOTTLE", "COFFEE", "BUTTER", "COOKIE", "BANANA"
        };

        /// <summary>Every word in the active list. Used by the level authoring tools.</summary>
        public static IEnumerable<string> AllWords => Words;

        public static int Count => Words.Count;

        public static bool IsValidWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            return Words.Contains(word.Trim());
        }

        public static bool IsBonusWord(string word, IEnumerable<string> targetWords)
        {
            if (!IsValidWord(word)) return false;
            foreach (var target in targetWords)
            {
                if (string.Equals(target, word, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // Not a bonus word; it's a target word
                }
            }
            return true;
        }

        public static List<string> FindPossibleSubwords(string letters)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(letters)) return result;

            string upperLetters = letters.ToUpperInvariant();
            Dictionary<char, int> letterCounts = GetLetterCounts(upperLetters);

            foreach (var word in Words)
            {
                if (CanFormWord(word, letterCounts))
                {
                    result.Add(word);
                }
            }

            return result;
        }

        private static bool CanFormWord(string word, Dictionary<char, int> availableCounts)
        {
            Dictionary<char, int> wordCounts = GetLetterCounts(word);
            foreach (var kvp in wordCounts)
            {
                if (!availableCounts.TryGetValue(kvp.Key, out int count) || count < kvp.Value)
                {
                    return false;
                }
            }
            return true;
        }

        private static Dictionary<char, int> GetLetterCounts(string text)
        {
            var counts = new Dictionary<char, int>();
            foreach (char c in text)
            {
                char upper = char.ToUpperInvariant(c);
                if (counts.ContainsKey(upper))
                    counts[upper]++;
                else
                    counts[upper] = 1;
            }
            return counts;
        }
    }
}
