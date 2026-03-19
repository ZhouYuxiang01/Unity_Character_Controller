using UnityEngine;
using Animancer;

namespace BBBNexus
{
    // 剑的装备攻击连击与状态逻辑
    public class SwordBehaviour : MonoBehaviour, IHoldableItem
    {
        // 剑的状态机
        private enum SwordPhase
        {
            None, // 未装备
            Equipping, // 播放装备动画
            Idle, // 可攻击
            Unequipping, // 播放收起动画
            Attacking // 播放攻击动画
        }

        private PlayerController _player; // 玩家引用
        private ItemInstance _instance; // 剑实例数据
        private SwordSO _config; // 剑配置

        private SwordPhase _phase = SwordPhase.None; // 当前状态
        private float _equipEndTime; // 装备动画结束时间
        private float _unequipEndTime; // 收起动画结束时间
        private int _comboIndex; // 连击段数
        private bool _lastFireInput; // 上一帧攻击输入
        private int _attackToken; // 攻击令牌
        private int _attackTokenSnapshot; // 攻击令牌快照
        private System.Action _onAttackAnimEndCached; // 缓存回调

        private const int UpperBodyLayer = 1; // 动画层索引
        private const float DefaultFadeOut = 0.15f; // 淡出时长

        // 初始化实例
        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _config = _instance.BaseData as SwordSO;
        }

        // 装备并播放动画
        public void OnEquipEnter(PlayerController player)
        {
            _player = player;
            _phase = SwordPhase.Equipping;
            _equipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0f);
            _comboIndex = 0;
            _lastFireInput = false;
            _attackToken = 0;
            _onAttackAnimEndCached ??= OnAttackAnimationEnd;
            if (_player != null && _player.AnimFacade != null)
                _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);
            if (_player != null && _player.AnimFacade != null && _config != null && _config.EquipAnim != null)
            {
                var opt = _config.EquipAnimPlayOptions;
                _player.AnimFacade.PlayTransition(_config.EquipAnim, opt);
            }
            if (_player != null && _player.AnimFacade != null)
                _player.AnimFacade.ClearOnEndCallback(UpperBodyLayer);
        }

        // 每帧更新逻辑
        public void OnUpdateLogic()
        {
            if (_player == null || _player.AnimFacade == null || _config == null)
                return;
            bool fire = _player.RuntimeData != null && _player.RuntimeData.WantsToFire;
            bool fireDown = fire && !_lastFireInput;
            _lastFireInput = fire;
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
                    if (Time.time >= _equipEndTime)
                    {
                        _phase = SwordPhase.Idle;
                        _player.AnimFacade.ClearOnEndCallback(UpperBodyLayer);
                    }
                    return;
                case SwordPhase.Unequipping:
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
                    if (fireDown)
                        StartOrChainAttack();
                    return;
                default:
                    return;
            }
        }

        // 强制卸载并播放动画
        public void OnForceUnequip()
        {
            if (_player == null || _player.AnimFacade == null)
                return;
            CancelAttack();
            _phase = SwordPhase.Unequipping;
            _unequipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0f);
            if (_player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_config != null && _config.UnEquipAnim != null)
                {
                    _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);
                    _player.AnimFacade.PlayTransition(_config.UnEquipAnim, _config.UnEquipAnimPlayOptions);
                }
            }
            _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 0f, DefaultFadeOut);
        }

        // 启动或连击
        private void StartOrChainAttack()
        {
            if (_config == null || _player == null)
                return;
            if (_phase == SwordPhase.Equipping || _phase == SwordPhase.Unequipping)
                return;
            var request = _config.GetAttackRequest(_comboIndex);
            if (request.Clip == null)
                return;
            _attackToken++;
            _attackTokenSnapshot = _attackToken;
            _phase = SwordPhase.Attacking;
            if (_config.SwingSound != null)
                AudioSource.PlayClipAtPoint(_config.SwingSound, transform.position);
            if (_player.AnimFacade != null)
                _player.AnimFacade.SetOverrideOnEndCallback(_onAttackAnimEndCached);
            if (_player.RuntimeData != null)
            {
                _player.RequestOverride(request, flushImmediately: true);
            }
            _comboIndex = (_comboIndex + 1) % 3;
        }

        // 攻击动画结束回调
        private void OnAttackAnimationEnd()
        {
            if (_player == null || _player.AnimFacade == null)
                return;
            if (_attackTokenSnapshot != _attackToken) return;
            _phase = SwordPhase.Idle;
        }

        // 取消当前攻击
        private void CancelAttack()
        {
            if (_player == null)
                return;
            _attackToken++;
            if (_player.AnimFacade != null)
                _player.AnimFacade.ClearOverrideOnEndCallback();
            if (_phase == SwordPhase.Attacking)
            {
                _phase = SwordPhase.Idle;
            }
        }
    }
}
