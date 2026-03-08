using UnityEngine;
using Characters.Player;
using Items.Core;
using Items.Data;
using Characters.Player.Animation;
using Animancer;
using Characters.Player.Data;

namespace Items.Logic.Weapons
{
    /// <summary>
    /// SwordBehaviour (重构版)
    /// 
    /// 需求：
    /// 1) sword 拿出/收回都会播放动画，且在动画期间不能攻击。
    /// 2) 拿出结束后，上半身不播放任何动画（Layer1 权重可由 UpperBodyHoldItemState 管控）。
    /// 3) 攻击依次播放 Attack1 -> Attack2 -> Attack3（第一次 1，第二次 2）。
    /// 4) 攻击过程中允许再次按下攻击，立即播放下一段攻击（连段）。
    /// </summary>
    public class SwordBehaviour : MonoBehaviour, IHoldableItem
    {
        private enum SwordPhase
        {
            None,
            Equipping,
            Idle,
            Unequipping,
            Attacking,
        }

        private PlayerController _player;
        private ItemInstance _instance;
        private SwordSO _config;

        private SwordPhase _phase = SwordPhase.None;

        // Equip / Unequip gating
        private float _equipEndTime;
        private float _unequipEndTime;

        // Combo
        private int _comboIndex; // 0..2

        // Input edge
        private bool _lastFireInput;

        // Attack state token (prevents old callbacks from affecting new attack).
        private int _attackToken;

        // Tunables
        private const int UpperBodyLayer = 1;
        private const float DefaultFadeOut = 0.15f;

        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _config = _instance.BaseData as SwordSO;
        }

        public void OnEquipEnter(PlayerController player)
        {
            _player = player;

            _phase = SwordPhase.Equipping;
            _equipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0f);

            _comboIndex = 0;
            _lastFireInput = false;
            _attackToken = 0;

            // Equip 时确保上半身层可见，以便装备动画正常显示（一般 EquipAnim 在 Layer1 或默认 layer 配置里）
            if (_player != null && _player.AnimFacade != null)
                _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);

            // 播放拿出动画（阻止攻击）
            if (_player != null && _player.AnimFacade != null && _config != null && _config.EquipAnim != null)
            {
                var opt = _config.EquipAnimPlayOptions;
                _player.AnimFacade.PlayTransition(_config.EquipAnim, opt);
            }

            // 清掉旧回调，避免串线
            if (_player != null && _player.AnimFacade != null)
                _player.AnimFacade.ClearOnEndCallback(UpperBodyLayer);
        }

        public void OnUpdateLogic()
        {
            if (_player == null || _player.AnimFacade == null || _config == null)
                return;

            // Rising edge fire
            bool fire = _player.InputReader != null && _player.InputReader.FireInput;
            bool fireDown = fire && !_lastFireInput;
            _lastFireInput = fire;

            // Interrupt policy: locomotion intents cancel sword attacks.
            if (_phase == SwordPhase.Attacking && _player.RuntimeData != null)
            {
                var rd = _player.RuntimeData;
                if (rd.WantsToRoll || rd.WantsToDodge || rd.WantsToVault || rd.WantsToJump)
                {
                    CancelAttack();
                    return;
                }
            }

            switch (_phase)
            {
                case SwordPhase.Equipping:
                    // Equip 时不能攻击
                    if (Time.time >= _equipEndTime)
                    {
                        _phase = SwordPhase.Idle;

                        // 需求：拿出之后上半身不播放动画 => 清空 Layer1 当前 state
                        _player.AnimFacade.ClearOnEndCallback(UpperBodyLayer);

                        // 这里不强制播放 EquipIdleAnim（即使配置了也不播）
                        // 同时不主动把 layer1 权重拉到 0，交由上半身状态/其他系统处理。
                    }
                    return;

                case SwordPhase.Unequipping:
                    // Unequip 时不能攻击
                    if (Time.time >= _unequipEndTime)
                    {
                        _phase = SwordPhase.None;
                    }
                    return;

                case SwordPhase.Idle:
                    if (fireDown)
                        StartOrChainAttack();
                    return;

                case SwordPhase.Attacking:
                    // 攻击中允许再次按下攻击，立即接下一段。
                    if (fireDown)
                        StartOrChainAttack();
                    return;

                default:
                    return;
            }
        }

        public void OnForceUnequip()
        {
            if (_player == null || _player.AnimFacade == null)
                return;

            // 强制切走：必须停止攻击、清空回调
            CancelAttack();

            _phase = SwordPhase.Unequipping;
            _unequipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0f);

            // 播放收起动画（期间不能攻击）
            if (_player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_config != null && _config.UnEquipAnim != null)
                {
                    _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);
                    _player.AnimFacade.PlayTransition(_config.UnEquipAnim, _config.UnEquipAnimPlayOptions);
                }
            }

            // 收起后淡出上半身层（如果上半身系统仍在 HoldItem State，会自行再拉回；这里尽量温和）
            _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 0f, DefaultFadeOut);
        }

        private void StartOrChainAttack()
        {
            if (_config == null || _player == null || _player.AnimFacade == null)
                return;

            // Equip / Unequip 期间完全禁止攻击
            if (_phase == SwordPhase.Equipping || _phase == SwordPhase.Unequipping)
                return;

            if (!TryGetAttackClip(_comboIndex, out var clip, out var opt))
                return;

            // 播放时确保层可见
            _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);

            // 每次攻击都 bump token，使旧回调失效
            _attackToken++;
            int token = _attackToken;

            // 播放攻击动画到 Layer1
            AnimPlayOptions options = opt;
            options.Layer = UpperBodyLayer;
            _player.AnimFacade.PlayTransition(clip, options);

            _phase = SwordPhase.Attacking;

            // 播放声音
            if (_config.SwingSound != null)
                AudioSource.PlayClipAtPoint(_config.SwingSound, transform.position);

            // 绑定结束回调：只有 token 仍然匹配时才执行，避免“连段覆盖后旧回调把状态打回 Idle 导致卡住/乱序”
            _player.AnimFacade.SetOnEndCallback(() =>
            {
                if (token != _attackToken) return;

                // 攻击自然结束：进入 Idle，并淡出 Layer1
                _phase = SwordPhase.Idle;
                _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 0f, DefaultFadeOut);
            }, UpperBodyLayer);

            // 准备下一段连击索引（立即递增，保证攻击中再按一次会去下一段）
            _comboIndex = (_comboIndex + 1) % 3;
        }

        private void CancelAttack()
        {
            if (_player == null || _player.AnimFacade == null)
                return;

            // 使所有旧回调失效
            _attackToken++;

            _player.AnimFacade.ClearOnEndCallback(UpperBodyLayer);

            if (_phase == SwordPhase.Attacking)
            {
                _phase = SwordPhase.Idle;
                _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 0f, DefaultFadeOut);
            }
        }

        private bool TryGetAttackClip(int index, out ClipTransition clip, out AnimPlayOptions options)
        {
            clip = null;
            options = AnimPlayOptions.Default;

            switch (index)
            {
                case 0:
                    clip = _config.AttackAnim1;
                    options = _config.AttackAnimOptions1;
                    return clip != null && clip.Clip != null;
                case 1:
                    clip = _config.AttackAnim2;
                    options = _config.AttackAnimOptions2;
                    return clip != null && clip.Clip != null;
                case 2:
                    clip = _config.AttackAnim3;
                    options = _config.AttackAnimOptions3;
                    return clip != null && clip.Clip != null;
                default:
                    return false;
            }
        }
    }
}
