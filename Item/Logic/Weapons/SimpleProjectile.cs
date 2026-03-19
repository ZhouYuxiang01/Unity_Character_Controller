using UnityEngine;

// 简单投射物 处理碰撞和销毁 支持特效音效爆炸和伤害
namespace BBBNexus
{
    public class SimpleProjectile : MonoBehaviour
    {
        [Header("Hit Effects")]
        // 击中特效
        public GameObject hitVFXPrefab;
        // 击中音效
        public AudioClip hitSound;

        [Header("Settings")]
        // 存活时长
        public float lifeTime = 5f;

        // 命中是否爆炸
        [Tooltip("命中时是否触发爆炸并对周围刚体施加冲击力")]
        public bool ExplodeOnImpact = false;
        [Tooltip("爆炸影响半径 米")]
        public float ExplosionRadius = 5f;
        [Tooltip("爆炸最大冲击力")]
        public float ExplosionForce = 700f;
        [Tooltip("爆炸向上修正 越大越向上")]
        public float ExplosionUpwardsModifier = 0.0f;
        [Tooltip("哪些层会受爆炸影响 LayerMask")]
        public LayerMask ExplosionAffectLayers = ~0;

        [Header("Damage Settings")]
        // 命中是否造成范围伤害
        [Tooltip("命中时是否对爆炸范围内的指定标签角色发送伤害请求")]
        public bool DealDamageOnImpact = false;
        [Tooltip("爆炸伤害量")]
        public float DamageAmount = 10f;

        // 是否已命中
        private bool _hasImpacted = false;

        // 初始化 设置存活时长
        private void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        // 碰撞检测
        private void OnCollisionEnter(Collision collision)
        {
            if (_hasImpacted) return;
            ContactPoint contact = collision.GetContact(0);
            HandleImpact(contact.point, contact.normal, collision.collider);
        }

        // 触发器检测
        private void OnTriggerEnter(Collider other)
        {
            if (_hasImpacted) return;
            Vector3 contactPoint = other.ClosestPoint(transform.position);
            Vector3 contactNormal = (transform.position - contactPoint).normalized;
            if (contactNormal.sqrMagnitude < 1e-6f)
            {
                contactNormal = -transform.forward;
            }
            HandleImpact(contactPoint, contactNormal, other);
        }

        // 处理命中逻辑
        private void HandleImpact(Vector3 point, Vector3 normal, Collider other)
        {
            if (_hasImpacted) return;
            _hasImpacted = true;
            if (hitVFXPrefab != null)
            {
                Instantiate(hitVFXPrefab, point, Quaternion.LookRotation(normal));
            }
            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, point);
            }
            if (ExplodeOnImpact)
            {
                ApplyExplosionPhysics(point);
            }
            if (DealDamageOnImpact && DamageAmount > 0f)
            {
                ApplyDamageToTargets(point);
            }
            else if (DamageAmount > 0f && other != null)
            {
                IDamageable directTarget = other.GetComponentInParent<IDamageable>();
                if (directTarget != null)
                {
                    var req = new DamageRequest(DamageAmount);
                    directTarget.RequestDamage(in req);
                }
            }
            Destroy(gameObject);
        }

        // 爆炸物理
        private void ApplyExplosionPhysics(Vector3 explosionCenter)
        {
            Collider[] hits = Physics.OverlapSphere(explosionCenter, ExplosionRadius, ExplosionAffectLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return;
            System.Collections.Generic.HashSet<Rigidbody> affected = new System.Collections.Generic.HashSet<Rigidbody>();
            foreach (var col in hits)
            {
                if (col == null) continue;
                Rigidbody rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                if (affected.Contains(rb)) continue;
                rb.AddExplosionForce(ExplosionForce, explosionCenter, ExplosionRadius, ExplosionUpwardsModifier, ForceMode.Impulse);
                affected.Add(rb);
            }
        }

        // 范围伤害
        private void ApplyDamageToTargets(Vector3 explosionCenter)
        {
            Collider[] targetColliders = Physics.OverlapSphere(explosionCenter, ExplosionRadius);
            if (targetColliders == null || targetColliders.Length == 0)
                return;
            System.Collections.Generic.HashSet<IDamageable> damagedTargets = new System.Collections.Generic.HashSet<IDamageable>();
            foreach (var collider in targetColliders)
            {
                if (collider == null) continue;
                IDamageable damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;
                if (damagedTargets.Contains(damageable))
                    continue;
                var damageRequest = new DamageRequest(DamageAmount);
                damageable.RequestDamage(in damageRequest);
                damagedTargets.Add(damageable);
            }
        }
    }
}