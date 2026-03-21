using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 简洁对象池（非兼容重构版）：
    /// - 仅负责：预热、Spawn(激活)、Despawn(失活回收)
    /// - 不处理：父子级、Transform、Rigidbody/Trail 等任何状态
    /// - 状态复位由对象自身负责：实现 <see cref="IPoolable"/> 在 OnSpawned/OnDespawned 中处理
    /// </summary>
    public sealed class SimpleObjectPoolSystem : MonoBehaviour
    {

        [Serializable]
        public struct PrewarmEntry
        {
            public GameObject Prefab;
            [Min(0)] public int Count;
        }

        public static SimpleObjectPoolSystem Shared { get; private set; }

        [Header("Prewarm")]
        [SerializeField] private List<PrewarmEntry> _prewarm = new List<PrewarmEntry>();

        // prefab -> inactive instances
        private readonly Dictionary<GameObject, Queue<GameObject>> _pool = new Dictionary<GameObject, Queue<GameObject>>();
        // instance id -> prefab
        private readonly Dictionary<int, GameObject> _instanceToPrefab = new Dictionary<int, GameObject>();

        private void Awake()
        {
            if (Shared != null && Shared != this)
            {
                Destroy(gameObject);
                return;
            }

            Shared = this;
            PrewarmAll();
        }

        private void PrewarmAll()
        {
            if (_prewarm == null) return;
            for (int i = 0; i < _prewarm.Count; i++)
            {
                var e = _prewarm[i];
                if (e.Prefab == null || e.Count <= 0) continue;
                Prewarm(e.Prefab, e.Count);
            }
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            var q = GetOrCreateQueue(prefab);
            for (int i = 0; i < count; i++)
            {
                var inst = CreateInstance(prefab);
                InternalDespawn(inst, callCallbacks: true);
                q.Enqueue(inst);
            }
        }

        /// <summary>
        /// Spawn：取出或创建一个实例，并激活。
        /// 注意：不设置 parent，不改 transform。调用者自行定位。
        /// </summary>
        public GameObject Spawn(GameObject prefab)
        {
            if (prefab == null) return null;

            var q = GetOrCreateQueue(prefab);

            GameObject inst = null;
            while (q.Count > 0 && inst == null)
                inst = q.Dequeue();

            if (inst == null)
                inst = CreateInstance(prefab);

            InternalSpawn(inst, callCallbacks: true);
            return inst;
        }

        /// <summary>
        /// 尝试回收：如果 instance 不是由对象池创建的实例，则不会警告，返回 false。
        /// 适用于 VFX 这类“有时被 Instantiate，有时被池 Spawn”的资源。
        /// </summary>
        public bool TryDespawn(GameObject instance)
        {
            if (instance == null) return true;

            if (!_instanceToPrefab.TryGetValue(instance.GetInstanceID(), out var prefab) || prefab == null)
                return false;

            var q = GetOrCreateQueue(prefab);
            InternalDespawn(instance, callCallbacks: true);
            q.Enqueue(instance);
            return true;
        }

        /// <summary>
        /// Despawn：回收一个实例。
        /// </summary>
        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (!_instanceToPrefab.TryGetValue(instance.GetInstanceID(), out var prefab) || prefab == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[SimpleObjectPoolSystem] Despawn called for non-pooled instance: {instance.name}", instance);
#endif
                return;
            }

            var q = GetOrCreateQueue(prefab);
            InternalDespawn(instance, callCallbacks: true);
            q.Enqueue(instance);
        }

        private Queue<GameObject> GetOrCreateQueue(GameObject prefab)
        {
            if (!_pool.TryGetValue(prefab, out var q) || q == null)
            {
                q = new Queue<GameObject>();
                _pool[prefab] = q;
            }
            return q;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            var inst = Instantiate(prefab);
            _instanceToPrefab[inst.GetInstanceID()] = prefab;
            return inst;
        }

        private static void InternalSpawn(GameObject instance, bool callCallbacks)
        {
            if (instance == null) return;

            if (callCallbacks)
            {
                var poolables = instance.GetComponentsInChildren<IPoolable>(true);
                for (int i = 0; i < poolables.Length; i++)
                {
                    try { poolables[i]?.OnSpawned(); } catch { }
                }
            }

            instance.SetActive(true);
        }

        private static void InternalDespawn(GameObject instance, bool callCallbacks)
        {
            if (instance == null) return;

            if (callCallbacks)
            {
                var poolables = instance.GetComponentsInChildren<IPoolable>(true);
                for (int i = 0; i < poolables.Length; i++)
                {
                    try { poolables[i]?.OnDespawned(); } catch { }
                }
            }

            instance.SetActive(false);
        }
    }
}
