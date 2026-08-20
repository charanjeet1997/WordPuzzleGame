using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ServiceLocatorFramework;
using WordPuzzle.Services;

namespace WordPuzzle.Audio
{
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Sources Setup")]
        [SerializeField] private List<AudioSourceData> audioSourceList = new List<AudioSourceData>();

        [Header("Audio Clips (Direct References - No Resources.Load)")]
        public AudioClip clipSwipeChar;
        public AudioClip clipWordMatched;
        public AudioClip clipWrongWord;
        public AudioClip clipBonusWord;
        public AudioClip clipHint;
        public AudioClip clipShuffle;
        public AudioClip clipFanfare;
        public AudioClip clipButtonClick;

        private const string PrefKeySoundEnabled = "SoundEnabled";

        /// <summary>Whether audio is audible. Persisted across sessions.</summary>
        public bool SoundEnabled { get; private set; } = true;

        private void Awake()
        {
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<AudioManager>())
            {
                ServiceLocator.Current.Register<AudioManager>(this);
            }

            SoundEnabled = GameStorage.GetInt(PrefKeySoundEnabled, 1) == 1;
            ApplySoundState();
        }

        /// <summary>
        /// Muting is applied on the listener rather than per-source: Play() resets
        /// source.mute to false on every call, so a per-source mute would be undone
        /// by the next sound that plays.
        /// </summary>
        private void ApplySoundState()
        {
            AudioListener.volume = SoundEnabled ? 1f : 0f;
        }

        public void SetSoundEnabled(bool enabled)
        {
            SoundEnabled = enabled;
            GameStorage.SetInt(PrefKeySoundEnabled, enabled ? 1 : 0);
            GameStorage.Save();
            ApplySoundState();
        }

        public bool ToggleSound()
        {
            SetSoundEnabled(!SoundEnabled);
            return SoundEnabled;
        }

        private void OnDestroy()
        {
            if (ServiceLocator.Current != null && ServiceLocator.Current.Has<AudioManager>())
            {
                ServiceLocator.Current.Unregister<AudioManager>();
            }
        }

        private void OnEnable()
        {
            AudioProvider.OnGlobalPlayAudioObserver.Bind(this, PlayFromObserver, "PlayFromObserver");
        }

        private void OnDisable()
        {
            AudioProvider.OnGlobalPlayAudioObserver.Unbind(PlayFromObserver);
        }

        public void PlaySwipeCharSound() => PlayClip(clipSwipeChar, AudioType.SFX);
        public void PlayWordMatchedSound() => PlayClip(clipWordMatched, AudioType.SFX);
        public void PlayWrongWordSound() => PlayClip(clipWrongWord, AudioType.SFX);
        public void PlayBonusWordSound() => PlayClip(clipBonusWord, AudioType.SFX);
        public void PlayHintSound() => PlayClip(clipHint, AudioType.SFX);
        public void PlayShuffleSound() => PlayClip(clipShuffle, AudioType.SFX);
        public void PlayFanfareSound() => PlayClip(clipFanfare, AudioType.SFX);
        public void PlayButtonClickSound() => PlayClip(clipButtonClick, AudioType.UI);

        public void PlayClip(AudioClip clip, AudioType type = AudioType.SFX, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            Play(new AudioData
            {
                clip = clip,
                type = type,
                volume = volume,
                pitch = pitch,
                loop = false
            });
        }

        public void Play(AudioData data)
        {
            if (data == null || data.clip == null) return;

            AudioSourceData sourceData = GetAudioSourceData(data.type);
            AudioSource source = (sourceData != null && sourceData.source != null) 
                ? sourceData.source 
                : GetComponentInChildren<AudioSource>();

            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            source.spatialBlend = 0f;
            source.mute = false;
            source.enabled = true;
            source.pitch = data.pitch <= 0.01f ? 1.0f : data.pitch;
            float finalVolume = Mathf.Clamp01(data.volume <= 0.01f ? 0.8f : data.volume);
            source.volume = finalVolume;

            bool isLooping = data.loop || data.type == AudioType.Background;
            source.loop = isLooping;

            if (isLooping)
            {
                source.clip = data.clip;
                if (!source.isPlaying)
                {
                    source.Play();
                }
            }
            else
            {
                source.PlayOneShot(data.clip, finalVolume);
            }
        }

        public void Pause(AudioType type = AudioType.Background)
        {
            AudioSourceData sourceData = GetAudioSourceData(type);
            if (sourceData != null && sourceData.source != null)
            {
                sourceData.source.Pause();
            }
        }

        public void Stop(AudioType type = AudioType.Background)
        {
            AudioSourceData sourceData = GetAudioSourceData(type);
            if (sourceData != null && sourceData.source != null)
            {
                sourceData.source.Stop();
            }
        }

        private void PlayFromObserver(AudioData data)
        {
            Play(data);
        }

        /// <summary>The configured source for a channel, so MusicPlayer can drive Background
        /// directly instead of standing up a second source for the same job.</summary>
        public AudioSource GetSource(AudioType type)
        {
            AudioSourceData data = GetAudioSourceData(type);
            return data != null ? data.source : null;
        }

        private AudioSourceData GetAudioSourceData(AudioType type)
        {
            if (audioSourceList == null) return null;
            return audioSourceList.FirstOrDefault(x => x.type == type);
        }
    }
}
