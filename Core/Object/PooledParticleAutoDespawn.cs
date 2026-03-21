using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Standalone 粒子 VFX 的对象池支持脚本：
    /// - 适用于“单独 Spawn 的 VFX prefab”（例如子弹命中爆炸）
    /// - OnSpawned：清空并重播所有粒子
    /// - Update：当所有粒子都结束后自动 Despawn（若实例不是池化实例则 Destroy，避免刷警告）
    /// </summary>
    public sealed class PooledParticleAutoDespawn : MonoBehaviour, IPoolable
    {
        [SerializeField] private bool _includeChildren = true;

        [Tooltip("兜底最大存活时间，防止粒子永远 IsAlive=true 导致不回收。0=禁用")]
        [Min(0f)]
        [SerializeField] private float _maxLifeTime = 10f;

        private ParticleSystem[] _systems;
        private float _killAt;

        private bool UsePool => SimpleObjectPoolSystem.Shared != null;

        private void Awake()
        {
            Cache();
        }

        public void OnSpawned()
        {
            Cache();

            for (int i = 0; i < _systems.Length; i++)
            {
                var ps = _systems[i];
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }

            _killAt = _maxLifeTime > 0f ? Time.time + _maxLifeTime : 0f;
        }

        public void OnDespawned()
        {
            _killAt = 0f;
            Cache();
            for (int i = 0; i < _systems.Length; i++)
            {
                var ps = _systems[i];
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Update()
        {
            if (_systems == null || _systems.Length == 0) return;

            bool alive = false;
            for (int i = 0; i < _systems.Length; i++)
            {
                var ps = _systems[i];
                if (ps == null) continue;
                if (ps.IsAlive(true)) { alive = true; break; }
            }

            if (!alive || (_killAt > 0f && Time.time >= _killAt))
            {
                DespawnSelfSafe();
            }
        }

        private void Cache()
        {
            if (_systems != null && _systems.Length > 0) return;
            _systems = _includeChildren
                ? GetComponentsInChildren<ParticleSystem>(true)
                : GetComponents<ParticleSystem>();
        }

        private void DespawnSelfSafe()
        {
            if (UsePool)
            {
                if (!SimpleObjectPoolSystem.Shared.TryDespawn(gameObject))
                    Destroy(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
