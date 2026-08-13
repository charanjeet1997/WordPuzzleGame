using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DataBindingFramework;

namespace WordPuzzle.Audio
{
    [Serializable]
    public class AudioProvider
    {
        [Header("Audio Database Entries")]
        [SerializeField] private List<AudioData> audioDatabase = new List<AudioData>();

        #region STATIC_OBSERVER_SIGNAL
        public static Observer<AudioData> OnGlobalPlayAudioObserver { get; } = new Observer<AudioData>();
        #endregion

        public void PlayAudio(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var data = audioDatabase?.FirstOrDefault(e => e.id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (data != null)
            {
                OnGlobalPlayAudioObserver.Notify(data);
            }
            else
            {
                Debug.LogWarning($"[AudioProvider] AudioData with ID '{id}' not found in database.");
            }
        }

        public void PlayAudio(AudioType type)
        {
            if (audioDatabase == null || audioDatabase.Count == 0) return;
            var dataList = audioDatabase.Where(e => e.type == type).ToList();
            if (dataList.Count > 0)
            {
                var selected = dataList[UnityEngine.Random.Range(0, dataList.Count)];
                OnGlobalPlayAudioObserver.Notify(selected);
            }
        }

        public AudioData GetAudioData(string id)
        {
            return audioDatabase?.FirstOrDefault(e => e.id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public void AddAudioData(AudioData data)
        {
            if (data != null && !string.IsNullOrEmpty(data.id))
            {
                if (audioDatabase == null) audioDatabase = new List<AudioData>();
                audioDatabase.Add(data);
            }
        }
    }
}
