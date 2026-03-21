using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 简单子弹（按当前项目约定重写）：
    /// - 有生命时间：到点才回收自身
    /// - 命中指定 Layer：播放击中特效
    /// - 在配置范围内对 IDamageable 发送伤害请求（范围伤害）
    /// - 支持对象池复用：实现 IPoolable，在 OnSpawned/OnDespawned 中复位
    /// </summary>
    public sealed class SimpleProjectile : MonoBehaviour, IPoolable
    {
        [Header("Lifetime")]
        [Min(0f)] public float lifeTime = 5f;

        [Header("Hit")]
        [Tooltip("哪些 Layer 的碰撞会被视为命中。")]
        public LayerMask HitLayers = ~0;

        [Tooltip("命中时播放的特效 Prefab（建议挂 PooledParticleAutoDespawn）。")]
        public GameObject hitVFXPrefab;

        [Tooltip("命中音效（可由武器在发射时注入）。")]
        public AudioClip hitSound;

        [Header("Damage")]
        [Tooltip("命中后在该半径内造成范围伤害。0 表示不造成伤害。")]
        [Min(0f)]
        public float DamageRadius = 0f;

        [Tooltip("伤害值。")]
        [Min(0f)]
        public float DamageAmount = 10f;

        [Tooltip("哪些 Layer 的对象会被纳入伤害检测。")]
        public LayerMask DamageLayers = ~0;

        [Tooltip("OverlapSphere 是否包含 Trigger。")]
        public QueryTriggerInteraction DamageQueryTrigger = QueryTriggerInteraction.Collide;

        [Header("Behavior")]
        [Tooltip("命中后是否立即回收。若关闭，则子弹会继续飞行直到 lifeTime 到期。")]
        public bool DespawnOnHit = true;

        [Header("Debug")]
        public bool debug;

        private float _despawnAt;
        private bool _hitProcessed;

        private readonly HashSet<int> _damagedTargetIds = new HashSet<int>();

        private bool UsePool => SimpleObjectPoolSystem.Shared != null;

        #region Pool
        public void OnSpawned()
        {
            _hitProcessed = false;
            _despawnAt = lifeTime > 0f ? (Time.time + lifeTime) : 0f;

            _damagedTargetIds.Clear();

            // 复用兜底：确保 collider 打开
            var cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = true;
            }

            // 清速度，避免复用继承（发射端会重新赋 velocity）
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        public void OnDespawned()
        {
            _hitProcessed = false;
            _despawnAt = 0f;

            _damagedTargetIds.Clear();

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        #endregion

        private void OnEnable()
        {
            // 非池化场景兜底
            if (_despawnAt <= 0f)
                _despawnAt = lifeTime > 0f ? (Time.time + lifeTime) : 0f;
        }

        private void Update()
        {
            if (_despawnAt > 0f && Time.time >= _despawnAt)
            {
                DespawnSelf();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleHit(collision.collider, collision.GetContact(0).point);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleHit(other, other.ClosestPoint(transform.position));
        }

        private void HandleHit(Collider other, Vector3 hitPoint)
        {
            if (other == null) return;
            if (_hitProcessed) return;

            // 命中层过滤
            if (((1 << other.gameObject.layer) & HitLayers.value) == 0)
                return;

            _hitProcessed = true;

            if (debug)
                Debug.Log($"[SimpleProjectile] Hit '{other.name}' layer={other.gameObject.layer} point={hitPoint}", other);

            SpawnHitVFX(hitPoint, other);
            ApplyAreaDamage(hitPoint);

            if (DespawnOnHit)
                DespawnSelf();
        }

        private void SpawnHitVFX(Vector3 hitPoint, Collider hitCollider)
        {
            if (hitVFXPrefab != null)
            {
                var rot = Quaternion.LookRotation(-transform.forward, Vector3.up);

                GameObject vfx;
                if (SimpleObjectPoolSystem.Shared != null)
                {
                    vfx = SimpleObjectPoolSystem.Shared.Spawn(hitVFXPrefab);
                    vfx.transform.SetPositionAndRotation(hitPoint, rot);
                }
                else
                {
                    vfx = Instantiate(hitVFXPrefab, hitPoint, rot);
                }
            }

            if (hitSound != null)
                AudioSource.PlayClipAtPoint(hitSound, hitPoint);
        }

        private void ApplyAreaDamage(Vector3 center)
        {
            if (DamageRadius <= 0f || DamageAmount <= 0f) return;

            // 如果 DamageLayers 配成 Nothing(0)，则回退使用 HitLayers，避免“看起来命中了但伤害层为空”
            var layers = DamageLayers.value != 0 ? DamageLayers : HitLayers;

            var cols = Physics.OverlapSphere(center, DamageRadius, layers, DamageQueryTrigger);
            if (cols == null || cols.Length == 0) return;

            _damagedTargetIds.Clear();

            var hitCount = 0;
            var req = new DamageRequest(DamageAmount);

            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;

                IDamageable d = FindDamageable(c);
                if (d == null) continue;

                int id = (d as Component) != null ? ((Component)d).GetInstanceID() : d.GetHashCode();
                if (!_damagedTargetIds.Add(id))
                    continue;

                d.RequestDamage(in req);
                hitCount++;

                if (debug)
                    Debug.Log($"[SimpleProjectile] Damage -> {d.GetType().Name} ({id})", (d as Component));
            }

            if (debug)
                Debug.Log($"[SimpleProjectile] AreaDamage radius={DamageRadius} amount={DamageAmount} targets={hitCount}");
        }

        private static IDamageable FindDamageable(Collider col)
        {
            if (col == null) return null;

            // 1) collider 层级
            var d = col.GetComponentInParent<IDamageable>();
            if (d != null) return d;

            // 2) 刚体根（常见：刚体在根，collider 在子)
            var rb = col.attachedRigidbody;
            if (rb != null)
            {
                d = rb.GetComponentInParent<IDamageable>();
                if (d != null) return d;
            }

            // 3) 根节点
            var root = col.transform.root;
            return root != null ? root.GetComponent<IDamageable>() : null;
        }

        private void DespawnSelf()
        {
            if (UsePool)
                SimpleObjectPoolSystem.Shared.Despawn(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
