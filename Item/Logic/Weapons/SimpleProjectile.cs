using UnityEngine;

// 简单投射物脚本 处理子弹 炮弹等投射物的碰撞反应与销毁 
// 支持碰撞与触发器两种检测方式 生成击中特效与音效 
public class SimpleProjectile : MonoBehaviour
{
    [Header("Hit Effects")]
    // 击中时播放的特效预制体 会在击中点瞄准击中面生成 
    public GameObject hitVFXPrefab;
    // 击中时播放的音效 由开火脚本注入 
    public AudioClip hitSound;

    [Header("Settings")]
    // 投射物的生存时长 防止无限漂流在地图上导致内存泄漏 
    public float lifeTime = 5f;

    // 防止重复触发碰撞 确保只处理一次击中 避免多次播放特效与音效 
    private bool _hasImpacted = false;

    // 初始化 设置生存时间 
    private void Start()
    {
        // 使用生命周期销毁 当投射物没有击中任何东西时 经过指定时长后自动销毁 
        // 这防止了无限飘浮的僵尸投射物占用内存 
        Destroy(gameObject, lifeTime);
    }

    // 碰撞检测 用于有刚体的碰撞体 
    private void OnCollisionEnter(Collision collision)
    {
        // 已经击中过则直接返回 
        if (_hasImpacted) return;

        // 获取第一个碰撞接触点 包含击中位置与法线方向 
        ContactPoint contact = collision.GetContact(0);

        // 统一处理击中事件 
        HandleImpact(contact.point, contact.normal, collision.collider);
    }

    // 触发器检测 用于没有刚体但设置了 IsTrigger 的碰撞体 
    private void OnTriggerEnter(Collider other)
    {
        // 已经击中过则直接返回 
        if (_hasImpacted) return;

        // 触发器没有 ContactPoint 所以需要手动计算击中位置与法线 
        // ClosestPoint 返回碰撞体上最接近投射物的点 
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        // 法线是从接触点指向投射物的方向 代表投射物撞向碰撞体的方向 
        Vector3 contactNormal = (transform.position - contactPoint).normalized;

        // 如果法线计算失败 使用投射物的飞行方向作为备选法线 
        if (contactNormal.sqrMagnitude < 1e-6f)
        {
            // 投射物的反向就是击中面的法向 这样特效会正确指向 
            contactNormal = -transform.forward;
        }

        // 统一处理击中事件 
        HandleImpact(contactPoint, contactNormal, other);
    }

    // 统一处理击中逻辑 生成特效与音效 标记已击中 销毁自身 
    private void HandleImpact(Vector3 point, Vector3 normal, Collider other)
    {
        // 双重防护 确保只处理一次 
        if (_hasImpacted) return;
        _hasImpacted = true;

        // 生成击中视觉特效 
        // 特效会在击中点生成 并且面向击中面法线 确保特效朝向正确 
        if (hitVFXPrefab != null)
        {
            Instantiate(hitVFXPrefab, point, Quaternion.LookRotation(normal));
        }

        // 生成击中音效 
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, point);
        }

        // 这里可以添加伤害计算 标签检测等游戏逻辑 
        // 比如 if (other != null && other.CompareTag("Enemy")) { ... } 

        // 销毁投射物实例 
        Destroy(gameObject);
    }
}