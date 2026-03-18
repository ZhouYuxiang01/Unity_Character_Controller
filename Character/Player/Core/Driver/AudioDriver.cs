using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 音频驱动器：运行时根据编号播放音效。
    /// 设计目标：调用方只关心“编号”，不关心 AudioClip 引用来源。
    /// </summary>
    public sealed class AudioDriver
    {
        private readonly Transform _emitter;
        private readonly AudioSource _source;
        private readonly AudioSO _audio;

        public AudioDriver(Transform emitter, AudioSource source, AudioSO audio)
        {
            _emitter = emitter;
            _source = source;
            _audio = audio;
        }

        /// <summary>
        /// 按编号播放音频。
        /// 未找到编号/未配置模块/AudioSource为空时直接忽略。
        /// </summary>
        public void Play(int id)
        {
            if (_audio == null || _source == null) return;
            if (!_audio.TryGetClip(id, out var clip) || clip == null) return;

            // 需求只说“播放音频”，这里用 PlayOneShot 避免打断循环BGM等。
            _source.PlayOneShot(clip);
        }
    }
}
