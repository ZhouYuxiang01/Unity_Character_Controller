using Characters.Player;
using Items.Core;
using Items.Data; // 确保引用配置层命名空间
using UnityEngine;

namespace Items.Logic.Weapons
{
    /// <summary>
    /// Cannon 实体行为控制脚本
    /// 挂载在 Cannon 的 Prefab 根节点。接管自身的输入判断、IK管控与开火表现。
    /// </summary>
    public class CannonBehaviour : MonoBehaviour, IHoldableItem
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
        private CannonSO _cannonConfig;
        private float _fireRate = 0.1f;   // 默认射速

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
            _cannonConfig = _instance.BaseData as CannonSO;

            if (_cannonConfig != null)
            {
                float interval = _cannonConfig.ShootInterval > 0f ? _cannonConfig.ShootInterval : _cannonConfig.FireRate;
                _fireRate = Mathf.Max(0.001f, interval);
            }

            Debug.Log($"<color=#00FF00>[CANNON]</color> 灵魂注入成功！当前物品名: {_instance.BaseData.DisplayName}");
        }

        // ==========================================
        // 2. 状态机赋权：拔枪出场！
        // ==========================================
        public void OnEquipEnter(PlayerController player)
        {
            _player = player;
            _isEquipping = true;

            if (_leftHandGoal != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.LeftHandGoal = _leftHandGoal;
                _player.RuntimeData.WantsLeftHandIK = false;

                if (_cannonConfig != null)
                {
                    _ikEnableScheduled = true;
                    _ikEnableTimePoint = Time.time + _cannonConfig.EnableIKTime;
                }
                else
                {
                    _player.RuntimeData.WantsLeftHandIK = true;
                    _ikActive = true;
                }

                Debug.Log($"<color=#00FF00>[CANNON]</color> 左手 IK 目标已设置，计划在 {_ikEnableTimePoint - Time.time:0.00}s 后开启（若配置）。");
            }

            float equipAnimDuration = _cannonConfig != null ? _cannonConfig.EquipEndTime : 0.5f;
            _equipEndTime = Time.time + equipAnimDuration;

            if (_cannonConfig != null && _cannonConfig.EquipAnim != null && _player != null)
            {
                _player.AnimFacade.PlayTransition(_cannonConfig.EquipAnim, _cannonConfig.EquipAnimPlayOptions);
            }

            Debug.Log($"<color=#FFFF00>[CANNON]</color> 正在拔枪... {equipAnimDuration} 秒内禁止开火。");
        }

        // ==========================================
        // 3. 武器的主舞台：状态机每帧无脑转发
        // ==========================================
        public void OnUpdateLogic()
        {
            // --- IK 调度 ---
            if (_ikEnableScheduled && Time.time >= _ikEnableTimePoint)
            {
                if (_isEquipping)
                {
                    _ikEnableTimePoint = _equipEndTime + 0.001f;
                }
                else
                {
                    _ikEnableScheduled = false;
                    if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == _instance)
                    {
                        _player.RuntimeData.WantsLeftHandIK = true;
                        _ikActive = true;
                        Debug.Log("<color=#00FF00>[CANNON]</color> 延时开启左手 IK。");
                    }
                }
            }

            if (_ikDisableScheduled && Time.time >= _ikDisableTimePoint)
            {
                _ikDisableScheduled = false;
                if (_player != null && _player.RuntimeData != null)
                {
                    var current = _player.RuntimeData.CurrentItem;
                    if (current == null || current.InstanceID == _instance.InstanceID)
                    {
                        _player.RuntimeData.WantsLeftHandIK = false;
                        _player.RuntimeData.LeftHandGoal = null;
                        _ikActive = false;
                        Debug.Log("<color=#FF0000>[CANNON]</color> 延时关闭左手 IK。");
                    }
                    else
                    {
                        Debug.Log("<color=#FFFF00>[CANNON]</color> 跳过延时关闭 IK，因为当前装备已更换。");
                    }
                }
            }

            // --- 硬直拦截 ---
            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    _isEquipping = false;
                    Debug.Log("<color=#00FF00>[CANNON]</color> 拔枪完毕！进入战备状态。");

                    if (_cannonConfig != null && _cannonConfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_cannonConfig.EquipIdleAnim, _cannonConfig.EquipIdleAnimOptions);
                    }
                }
                else
                {
                    return;
                }
            }

            // --- 业务逻辑 ---
            bool isAiming = _player != null && _player.RuntimeData != null && _player.RuntimeData.IsAiming;

            if (!_isEquipping && _wasAiming != isAiming)
            {
                if (isAiming)
                {
                    if (_cannonConfig != null && _cannonConfig.AimAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_cannonConfig.AimAnim, _cannonConfig.AnimPlayOptions);
                    }

                    if (_player != null && _player.RuntimeData != null && _muzzle != null)
                    {
                        _player.RuntimeData.CurrentAimReference = _muzzle;
                        _player.RuntimeData.WantsLookAtIK = true;
                    }
                }
                else
                {
                    if (_cannonConfig != null && _cannonConfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_cannonConfig.EquipIdleAnim, _cannonConfig.EquipIdleAnimOptions);
                    }

                    if (_player != null && _player.RuntimeData != null)
                    {
                        if (_player.RuntimeData.CurrentAimReference == _muzzle)
                            _player.RuntimeData.CurrentAimReference = null;

                        _player.RuntimeData.WantsLookAtIK = false;
                    }
                }

                _wasAiming = isAiming;
            }

            bool isFiring = _player != null && _player.InputReader != null && _player.InputReader.FireInput;

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
            _isEquipping = false;

            if (_muzzleFlash != null) _muzzleFlash.Stop();

            if (_cannonConfig != null)
            {
                _ikDisableScheduled = true;
                _ikDisableTimePoint = Time.time + _cannonConfig.DisableIKTime;

                Debug.Log($"<color=#FF8800>[CANNON]</color> 计划在 {_cannonConfig.DisableIKTime:0.00}s 后关闭左手 IK（相对于收起动画开始）。");
            }
            else
            {
                if (_player != null && _player.RuntimeData != null)
                {
                    _player.RuntimeData.WantsLeftHandIK = false;
                    _player.RuntimeData.LeftHandGoal = null;
                    _ikActive = false;
                }
            }

            if (_player != null && _player.RuntimeData != null)
            {
                if (_player.RuntimeData.CurrentAimReference == _muzzle)
                    _player.RuntimeData.CurrentAimReference = null;

                _player.RuntimeData.WantsLookAtIK = false;
            }

            if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_cannonConfig != null && _cannonConfig.UnEquipAnim != null)
                {
                    _player.AnimFacade.PlayTransition(_cannonConfig.UnEquipAnim, _cannonConfig.UnEquipAnimPlayOptions);
                }
            }

            Debug.Log("<color=#FF0000>[CANNON]</color> 已发起收枪流程，等待延时关闭 IK（若配置）。");
        }

        // ==========================================
        // 私有方法：开火判定
        // ==========================================
        private void TryFire()
        {
            if (Time.time - _lastFireTime < _fireRate) return;

            _lastFireTime = Time.time;

            if (_muzzleFlash != null) _muzzleFlash.Play();

            if (_cannonConfig != null && _cannonConfig.ShootSound != null && _muzzle != null)
            {
                AudioSource.PlayClipAtPoint(_cannonConfig.ShootSound, _muzzle.position);
            }

            if (_cannonConfig != null && _cannonConfig.MuzzleVFXPrefab != null && _muzzle != null)
            {
                var muzzleVFX = Object.Instantiate(_cannonConfig.MuzzleVFXPrefab, _muzzle.position, _muzzle.rotation);
                muzzleVFX.transform.parent = _muzzle;
            }

            ApplyRecoil();

            if (_cannonConfig != null && _cannonConfig.ProjectilePrefab != null && _muzzle != null)
            {
                var proj = Object.Instantiate(_cannonConfig.ProjectilePrefab, _muzzle.position, _muzzle.rotation);
                proj.transform.parent = null;

                var rb = proj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = _muzzle.forward * _cannonConfig.ProjectileSpeed;
                }

                var simple = proj.GetComponent<SimpleProjectile>();
                if (simple != null)
                {
                    simple.hitSound = _cannonConfig.ProjectileHitSound;
                }
            }

            Debug.Log("<color=#FF8800>[CANNON]</color> 砰！检测到瞄准状态，成功开火！");
        }

        private void ApplyRecoil()
        {
            if (_player == null || _player.RuntimeData == null || _cannonConfig == null) return;

            float pitchNoise = Random.Range(-_cannonConfig.RecoilPitchRandomRange, _cannonConfig.RecoilPitchRandomRange);
            float yawNoise = Random.Range(-_cannonConfig.RecoilYawRandomRange, _cannonConfig.RecoilYawRandomRange);

            float finalPitch = _cannonConfig.RecoilPitchAngle + pitchNoise;
            float finalYaw = _cannonConfig.RecoilYawAngle + yawNoise;

            float yawSign = Random.value > 0.5f ? 1f : -1f;

            _player.RuntimeData.ViewPitch -= finalPitch;
            _player.RuntimeData.ViewYaw += yawSign * finalYaw;

            _player.RuntimeData.ViewPitch = Mathf.Clamp(
                _player.RuntimeData.ViewPitch,
                _player.Config.Core.PitchLimits.x,
                _player.Config.Core.PitchLimits.y
            );

            Debug.Log($"<color=#FF8800>[CANNON]</color> 一次性后坐力已应用！俯仰: {finalPitch}°, 偏航: {finalYaw}° (yawSign: {yawSign})");
        }
    }
}
