using UnityEngine;

public class SimpleProjectile : MonoBehaviour
{
    [Header("Hit Effects")]
    public GameObject hitVFXPrefab; // 击中时的粒子特效预制体
    public AudioClip hitSound;      // 击中时的音效

    [Header("Settings")]
    public float lifeTime = 5f;     // 子弹最大存活时间（防止射向天空导致内存泄漏）

    // 防止多次触发（例如同时触发碰撞和触发器）
    private bool _hasImpacted = false;

    private void Start()
    {
        // 飞行保险：如果子弹飞出地图没打中任何东西，到了时间自动销毁
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasImpacted) return;

        // 1. 抓取第一接触点（极其重要！）
        ContactPoint contact = collision.GetContact(0);

        HandleImpact(contact.point, contact.normal, collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasImpacted) return;

        // Trigger 情况下没有 ContactPoint，需要近似计算碰撞点和法线
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        Vector3 contactNormal = (transform.position - contactPoint).normalized;

        // 如果 ClosestPoint 返回的点和子弹位置一致（例如完全重合），fallback 一个朝向
        if (contactNormal.sqrMagnitude < 1e-6f)
        {
            contactNormal = -transform.forward; // 假定子弹迎面撞上，法线朝向子弹反方向
        }

        HandleImpact(contactPoint, contactNormal, other);
    }

    // 统一处理命中效果与销毁
    private void HandleImpact(Vector3 point, Vector3 normal, Collider other)
    {
        if (_hasImpacted) return;
        _hasImpacted = true;

        // 生成粒子特效
        if (hitVFXPrefab != null)
        {
            Instantiate(hitVFXPrefab, point, Quaternion.LookRotation(normal));
        }

        // 播放音效
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, point);
        }

        // 在这里可以添加伤害判定逻辑
        // if (other != null && other.CompareTag("Enemy")) { ... }

        // 撞击后销毁子弹实体
        Destroy(gameObject);
    }
}