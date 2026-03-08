using Characters.Player;
using Items.Core;
using Items.Data; // 确保这里引用了你的配置层命名空间
using UnityEngine;

namespace Items.Logic.Weapons
{
    /// <summary>
    /// AK46 实体行为控制脚本
    /// 挂载在 AK46 的 Prefab 根节点。接管自身的输入判断、IK管控与开火表现。
    /// </summary>
    public class AK46Behaviour : MonoBehaviour, IHoldableItem
    {
        [Header("--- 表现与挂点 (Visual & IK) ---")]
        [Tooltip("左手应该握在哪里？(将枪管上的空物体拖入)")]
        [SerializeField] private Transform _leftHandGoal;

        [Tooltip("枪口火焰特效")]
        [SerializeField] private ParticleSystem _muzzleFlash;

        [Tooltip("枪口 / 瞄准参考点 (用作 AimIK 的目标)")]
        [SerializeField] private Transform _muzzle;

        // --- 运行时的宿主与灵魂 ---
        private PlayerController _player;
        private ItemInstance _instance;     // 纯逻辑实例 (记录真实的弹药、耐久等)
        private AKSO _akconfig;
        private float _fireRate = 0.1f;   // 默认射速 (最好从 _mySoul.BaseData 强转配置后读取)

        // --- 内部微型状态机 ---
        private bool _isEquipping;
        private float _equipEndTime;
        private float _lastFireTime;

        // 记录上帧瞄准状态以便检测切换
        private bool _wasAiming;

        // IK 调度
        private bool _ikEnableScheduled;
        private float _ikEnableTimePoint;
        private bool _ikDisableScheduled;
        private float _ikDisableTimePoint;
        private bool _ikActive;

        // ==========================================
        // 1. 灵魂注入 (Driver 生成模型后，同一帧立刻被调用)
        // ==========================================
        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _akconfig = _instance.BaseData as AKSO;

            if (_akconfig != null)
            {
                // Prefer explicit ShootInterval if set (> 0), otherwise fallback to base FireRate
                float interval = _akconfig.ShootInterval > 0f ? _akconfig.ShootInterval : _akconfig.FireRate;
                _fireRate = Mathf.Max(0.001f, interval);
            }

            Debug.Log($"<color=#00FF00>[AK46]</color> 灵魂注入成功！当前物品名: {_instance.BaseData.DisplayName}");
        }

        // ==========================================
        // 2. 状态机赋权：拔枪出场！
        // ==========================================
        public void OnEquipEnter(PlayerController player)
        {
            _player = player;
            _isEquipping = true;

            // 设置 LeftHandGoal 但延迟启用 IK 到配置时间点
            if (_leftHandGoal != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.LeftHandGoal = _leftHandGoal;
                _player.RuntimeData.WantsLeftHandIK = false; // 先不立即打开，由调度控制

                if (_akconfig != null)
                {
                    _ikEnableScheduled = true;
                    _ikEnableTimePoint = Time.time + _akconfig.EnableIKTime;
                }
                else
                {
                    // 如果没有配置，立即启用以保持兼容
                    _player.RuntimeData.WantsLeftHandIK = true;
                    _ikActive = true;
                }

                Debug.Log($"<color=#00FF00>[AK46]</color> 左手 IK 目标已设置，计划在 {_ikEnableTimePoint - Time.time:0.00}s 后开启（若配置）。");
            }

            // 处理拔枪硬直
            float equipAnimDuration = _akconfig.EquipEndTime;
            _equipEndTime = Time.time + equipAnimDuration;

            // 播放拔枪动画（若存在）
            if (_akconfig != null && _akconfig.EquipAnim != null && _player != null)
            {
                _player.AnimFacade.PlayTransition(_akconfig.EquipAnim, _akconfig.EquipAnimPlayOptions);
            }

            Debug.Log($"<color=#FFFF00>[AK46]</color> 正在拔枪... {equipAnimDuration} 秒内禁止开火。");
        }

        // ==========================================
        // 3. 武器的主舞台：状态机每帧无脑转发
        // ==========================================
        public void OnUpdateLogic()
        {
            // --- IK 调度 ---
            if (_ikEnableScheduled && Time.time >= _ikEnableTimePoint)
            {
                // Avoid enabling left-hand IK while still in equip hardening.
                if (_isEquipping)
                {
                    // Postpone enable until equip hardening ends
                    _ikEnableTimePoint = _equipEndTime + 0.001f;
                }
                else
                {
                    _ikEnableScheduled = false;
                    // 仅在该武器仍为当前装备时启用 IK
                    if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == _instance)
                    {
                        _player.RuntimeData.WantsLeftHandIK = true;
                        _ikActive = true;
                        Debug.Log("<color=#00FF00>[AK46]</color> 延时开启左手 IK。");
                    }
                }
            }

            if (_ikDisableScheduled && Time.time >= _ikDisableTimePoint)
            {
                _ikDisableScheduled = false;
                // 只有在该武器仍应当关闭 IK 时才关闭（避免影响新武器）
                if (_player != null && _player.RuntimeData != null)
                {
                    var current = _player.RuntimeData.CurrentItem;
                    if (current == null || current.InstanceID == _instance.InstanceID)
                    {
                        _player.RuntimeData.WantsLeftHandIK = false;
                        _player.RuntimeData.LeftHandGoal = null;
                        _ikActive = false;
                        Debug.Log("<color=#FF0000>[AK46]</color> 延时关闭左手 IK。");
                    }
                    else
                    {
                        Debug.Log("<color=#FFFF00>[AK46]</color> 跳过延时关闭 IK，因为当前装备已更换。");
                    }
                }
            }

            // --- 阶段 A：硬直拦截 ---
            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    _isEquipping = false; // 硬直结束！
                    Debug.Log("<color=#00FF00>[AK46]</color> 拔枪完毕！进入战备状态。");

                    // 拔枪完毕后进入 EquipIdleAnim（如果配置了的话）
                    if (_akconfig != null && _akconfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_akconfig.EquipIdleAnim, _akconfig.EquipIdleAnimOptions);
                    }
                }
                else
                {
                    return; // 依然在拔枪中，直接 return，不响应任何玩家输入！
                }
            }

            // --- 阶段 B：业务逻辑执行 ---

            // 获取瞄准状态
            bool isAiming = _player != null && _player.RuntimeData != null && _player.RuntimeData.IsAiming;

            // 如果瞄准状态发生切换，则切换动画
            if (!_isEquipping && _wasAiming != isAiming)
            {
                if (isAiming)
                {
                    // 进入瞄准，播放瞄准动画，并把 muzzle 写入 CurrentAimReference 作为 AimIK 的基准点
                    if (_akconfig != null && _akconfig.AimAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_akconfig.AimAnim, _akconfig.AnimPlayOptions);
                    }

                    if (_player != null && _player.RuntimeData != null && _muzzle != null)
                    {
                        // 将枪口设置为 AimIK 的基准点（让 AimIK 以枪口为中心指向准星）
                        _player.RuntimeData.CurrentAimReference = _muzzle;
                        // 打开头部指向 IK（让头部看向摄像机计算的准星点）
                        _player.RuntimeData.WantsLookAtIK = true;
                    }
                }
                else
                {
                    // 退出瞄准，回到 EquipIdleAnim（如果存在），并清除 AimReference
                    if (_akconfig != null && _akconfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_akconfig.EquipIdleAnim, _akconfig.EquipIdleAnimOptions);
                    }

                    if (_player != null && _player.RuntimeData != null)
                    {
                        // 清除 AimIK 基准点
                        if (_player.RuntimeData.CurrentAimReference == _muzzle)
                            _player.RuntimeData.CurrentAimReference = null;

                        // 关闭头部指向 IK
                        _player.RuntimeData.WantsLookAtIK = false;
                    }
                }

                _wasAiming = isAiming;
            }

            // 开火输入
            bool isFiring = _player != null && _player.InputReader != null && _player.InputReader.FireInput;

            // 只有在瞄准时可以开火
            if (isAiming && isFiring)
            {
                TryFire();
            }
        }

        // ==========================================
        // 4. 被迫下线：切枪、受击或翻滚被打断
        // ==========================================
        public void OnForceUnequip()
        {
            _isEquipping = false; // 强行重置状态

            if (_muzzleFlash != null) _muzzleFlash.Stop();

            // 不要立即关闭 IK，而是根据配置延迟关闭，以配合收枪动画
            if (_akconfig != null)
            {
                _ikDisableScheduled = true;
                _ikDisableTimePoint = Time.time + _akconfig.DisableIKTime;

                Debug.Log($"<color=#FF8800>[AK46]</color> 计划在 {_akconfig.DisableIKTime:0.00}s 后关闭左手 IK（相对于收起动画开始）。");
            }
            else
            {
                // 未配置则立即关闭
                if (_player != null && _player.RuntimeData != null)
                {
                    _player.RuntimeData.WantsLeftHandIK = false;
                    _player.RuntimeData.LeftHandGoal = null;
                    _ikActive = false;
                }
            }

            // 清理瞄准相关的 IK
            if (_player != null && _player.RuntimeData != null)
            {
                if (_player.RuntimeData.CurrentAimReference == _muzzle)
                    _player.RuntimeData.CurrentAimReference = null;

                _player.RuntimeData.WantsLookAtIK = false;
            }

            // 播放收起动画（仅当当前物品已经为 null 时）
            if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_akconfig != null && _akconfig.UnEquipAnim != null)
                {
                    _player.AnimFacade.PlayTransition(_akconfig.UnEquipAnim, _akconfig.UnEquipAnimPlayOptions);
                }
            }

            Debug.Log($"<color=#FF0000>[AK46]</color> 已发起收枪流程，等待延时关闭 IK（若配置）。");
        }

        // ==========================================
        // 私有方法：开火判定
        // ==========================================
        private void TryFire()
        {
            // 1. 射速校验
            if (Time.time - _lastFireTime < _fireRate) return;

            // 2. 弹药校验 (假设你在 _mySoul 里存了弹药数量，比如用字典或者扩展字段)
            // 如果你扩展了 ItemInstance，这里应该是： if (_mySoul.CurrentAmmo <= 0) return;
            // 为了演示框架跑通，我们这里暂时只打日志

            // 3. 真正开火
            _lastFireTime = Time.time;

            if (_muzzleFlash != null) _muzzleFlash.Play();

            // Play shooting sound if configured
            if (_akconfig != null && _akconfig.ShootSound != null && _muzzle != null)
            {
                AudioSource.PlayClipAtPoint(_akconfig.ShootSound, _muzzle.position);
            }

            // Spawn muzzle VFX if configured
            if (_akconfig != null && _akconfig.MuzzleVFXPrefab != null && _muzzle != null)
            {
                var muzzleVFX = Object.Instantiate(_akconfig.MuzzleVFXPrefab, _muzzle.position, _muzzle.rotation);
                // 保持特效在 muzzle 位置作为子物体
                muzzleVFX.transform.parent = _muzzle;
            }

            // 应用后坐力效果
            ApplyRecoil();

            // Spawn projectile if configured
            if (_akconfig != null && _akconfig.ProjectilePrefab != null && _muzzle != null)
            {
                var proj = Object.Instantiate(_akconfig.ProjectilePrefab, _muzzle.position, _muzzle.rotation);
                // Ensure it exists in world root (not parented to the weapon)
                proj.transform.parent = null;

                var rb = proj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Set initial velocity directly for consistent behavior
                    rb.velocity = _muzzle.forward * _akconfig.ProjectileSpeed;
                }

                // If the projectile has a SimpleProjectile component, assign hit sound from AKSO
                var simple = proj.GetComponent<SimpleProjectile>();
                if (simple != null)
                {
                    simple.hitSound = _akconfig.ProjectileHitSound;
                }
            }

            // 如果有后坐力动画、屏幕震动等，全在这里调用

            Debug.Log($"<color=#FF8800>[AK46]</color> 砰！检测到瞄准状态，成功开火！");
        }

        /// <summary>
        /// 应用后坐力效果：修改视角参数（ViewYaw/ViewPitch）而不是权威旋转
        /// 这样后坐力会被 ViewRotationProcessor 纳入最终的权威旋转计算，不会被下一帧重置
        /// </summary>
        private void ApplyRecoil()
        {
            if (_player == null || _player.RuntimeData == null || _akconfig == null) return;

            // 计算随机化的俯仰与偏航
            float pitchNoise = Random.Range(-_akconfig.RecoilPitchRandomRange, _akconfig.RecoilPitchRandomRange);
            float yawNoise = Random.Range(-_akconfig.RecoilYawRandomRange, _akconfig.RecoilYawRandomRange);

            float finalPitch = _akconfig.RecoilPitchAngle + pitchNoise;
            float finalYaw = _akconfig.RecoilYawAngle + yawNoise;

            // 直接修改 ViewPitch 和 ViewYaw（而不是 AuthorityRotation）
            // 这样后坐力会被纳入权威参考系，下一帧 ViewRotationProcessor 会基于这些值计算权威旋转
            // 后坐力的偏航方向随机
            float yawSign = Random.value > 0.5f ? 1f : -1f;
            
            _player.RuntimeData.ViewPitch -= finalPitch;  // 负值使视角向上
            _player.RuntimeData.ViewYaw += yawSign * finalYaw;  // 左右偏航

            // 应用俯仰限制（保持与 ViewRotationProcessor 的逻辑一致）
            _player.RuntimeData.ViewPitch = Mathf.Clamp(
                _player.RuntimeData.ViewPitch,
                _player.Config.Core.PitchLimits.x,
                _player.Config.Core.PitchLimits.y
            );

            Debug.Log($"<color=#FF8800>[AK46]</color> 一次性后坐力已应用！俯仰: {finalPitch}°, 偏航: {finalYaw}° (yawSign: {yawSign})");
        }
    }
}