using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    // ÒôÆµÄ£¿éÅäÖÃ£ºÌá¹©¡°±àºÅ -> AudioClip¡±µÄ¾²Ì¬Ó³Éä
    [CreateAssetMenu(fileName = "AudioSO", menuName = "BBBNexus/Player/Modules/AudioSO")]
    public sealed class AudioSO : ScriptableObject
    {
        [Serializable]
        public struct AudioEntry
        {
            public int Id;
            public AudioClip Clip;
        }

        [Header("Audio Map (Id -> Clip)")]
        [SerializeField] private List<AudioEntry> _entries = new List<AudioEntry>();

        private Dictionary<int, AudioClip> _cache;

        private void OnEnable() => BuildCache();
        private void OnValidate() => BuildCache();

        private void BuildCache()
        {
            if (_cache == null) _cache = new Dictionary<int, AudioClip>();
            else _cache.Clear();

            if (_entries == null) return;

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Clip == null) continue;

                // ºóÐ´¸²¸ÇÇ°Ð´£º·½±ãÔÚ Inspector Àï¿ìËÙ¸²¸Ç
                _cache[e.Id] = e.Clip;
            }
        }

        public bool TryGetClip(int id, out AudioClip clip)
        {
            clip = null;
            if (_cache == null) BuildCache();
            return _cache != null && _cache.TryGetValue(id, out clip) && clip != null;
        }
    }
}
