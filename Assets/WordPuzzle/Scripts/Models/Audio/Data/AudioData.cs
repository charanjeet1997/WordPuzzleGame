using System;
using UnityEngine;

namespace WordPuzzle.Audio
{
    public enum AudioType
    {
        Background,
        SFX,
        UI
    }

    [Serializable]
    public class AudioData
    {
        public string id;
        public AudioType type = AudioType.SFX;
        public AudioClip clip;
        public bool loop = false;
        [Range(0f, 1f)] public float volume = 1.0f;
        [Range(0.5f, 2f)] public float pitch = 1.0f;

        public AudioData() { }

        public AudioData(string id, AudioType type, AudioClip clip, bool loop = false, float volume = 1.0f, float pitch = 1.0f)
        {
            this.id = id;
            this.type = type;
            this.clip = clip;
            this.loop = loop;
            this.volume = volume;
            this.pitch = pitch;
        }
    }
}
