using UnityEngine;

namespace WordPuzzle.Feedback
{
    /// <summary>Intensity of a haptic tap, mapped per platform in <see cref="HapticManager"/>.</summary>
    public enum HapticType
    {
        Selection,  // lightest - letter swipes, per-item ticks
        Light,      // UI buttons
        Medium,     // word matched, bonus word
        Heavy,      // level complete
        Failure     // wrong / already-found word
    }

    /// <summary>
    /// Device vibration. Static rather than a service so gameplay code can fire a tap without
    /// holding a reference or null-checking a manager - the platform guards live in one place.
    /// The enabled flag persists across sessions, like the sound flag on AudioManager.
    /// </summary>
    public static class HapticManager
    {
        private const string PrefKeyHapticsEnabled = "HapticsEnabled";

        // A burst of taps (a fast swipe across the wheel) would otherwise queue up on the
        // device and buzz continuously well after the gesture ended.
        private const float MinIntervalSeconds = 0.03f;

        private static bool _loaded;
        private static bool _enabled = true;
        private static float _lastPlayTime = -1f;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static int _androidApiLevel;
        private static bool _androidProbed;
#endif

        /// <summary>Whether device vibration is allowed. Persisted across sessions.</summary>
        public static bool Enabled
        {
            get
            {
                if (!_loaded)
                {
                    _enabled = PlayerPrefs.GetInt(PrefKeyHapticsEnabled, 1) == 1;
                    _loaded = true;
                }
                return _enabled;
            }
        }

        /// <summary>False on platforms with no vibration motor, so the settings row can hide itself.</summary>
        public static bool IsSupported =>
            Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer;

        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            _loaded = true;
            PlayerPrefs.SetInt(PrefKeyHapticsEnabled, enabled ? 1 : 0);
            PlayerPrefs.Save();

            // Confirm the switch with the thing being switched on.
            if (enabled) Play(HapticType.Light);
        }

        public static bool Toggle()
        {
            SetEnabled(!Enabled);
            return Enabled;
        }

        public static void Play(HapticType type)
        {
            if (!Enabled) return;

            if (Time.unscaledTime - _lastPlayTime < MinIntervalSeconds) return;
            _lastPlayTime = Time.unscaledTime;

            Vibrate(DurationFor(type), AmplitudeFor(type));
        }

        private static long DurationFor(HapticType type)
        {
            switch (type)
            {
                case HapticType.Selection: return 12L;
                case HapticType.Light: return 20L;
                case HapticType.Medium: return 40L;
                case HapticType.Heavy: return 90L;
                case HapticType.Failure: return 60L;
                default: return 20L;
            }
        }

        /// <summary>0-255, honoured on Android 8+ devices with amplitude control.</summary>
        private static int AmplitudeFor(HapticType type)
        {
            switch (type)
            {
                case HapticType.Selection: return 60;
                case HapticType.Light: return 100;
                case HapticType.Medium: return 170;
                case HapticType.Heavy: return 255;
                case HapticType.Failure: return 200;
                default: return 128;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Vibrate(long milliseconds, int amplitude)
        {
            AndroidJavaObject vibrator = GetVibrator();
            if (vibrator == null) return;

            try
            {
                // VibrationEffect (API 26+) is what gives short taps their intended weight;
                // the legacy vibrate(long) call runs the motor flat out regardless of amplitude.
                if (_androidApiLevel >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                               "createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HapticManager] Vibration failed: {e.Message}");
            }
        }

        private static AndroidJavaObject GetVibrator()
        {
            if (_androidProbed) return _vibrator;
            _androidProbed = true;

            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _androidApiLevel = version.GetStatic<int>("SDK_INT");
                }

                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (_vibrator != null && !_vibrator.Call<bool>("hasVibrator")) _vibrator = null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HapticManager] Vibrator unavailable: {e.Message}");
                _vibrator = null;
            }

            return _vibrator;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        private static void Vibrate(long milliseconds, int amplitude)
        {
            // iOS exposes no duration control through Unity's public API; every tap is the
            // system's standard vibration. Weight comes from how often it is fired, not length.
            Handheld.Vibrate();
        }
#else
        private static void Vibrate(long milliseconds, int amplitude)
        {
            // Editor and desktop have no motor. Kept as a no-op so call sites stay unguarded.
        }
#endif
    }
}
