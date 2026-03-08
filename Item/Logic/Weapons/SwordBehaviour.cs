using UnityEngine;
using Characters.Player;
using Items.Core;
using Items.Data;
using Characters.Player.Animation;
using Animancer;
using Characters.Player.Data;

namespace Items.Logic.Weapons
{
    // 剑的行为脚本 完整的装备生命周期与连击系统 
    // 负责装备动画 攻击连段 中断处理 IK 权重管理等全流程 
    // 逻辑说明：
    //  1) 拿出与收回都播放动画 动画期间禁止攻击 
    //  2) 装备完成后上半身不播放任何待机动画 仅当攻击时才激活 
    //  3) 攻击依次循环 Attack1 -> Attack2 -> Attack3 
    //  4) 攻击中再次按下按键可立即连段 无冷却时间 
    public class SwordBehaviour : MonoBehaviour, IHoldableItem
    {
        // 内部微型状态机 管理剑的装备 待机 攻击 卸载四个阶段 
        private enum SwordPhase
        {
            // 未装备状态 
            None,
            // 正在播放拿出动画 禁止攻击 
            Equipping,
            // 装备完成 可以攻击 
            Idle,
            // 正在播放收起动画 禁止攻击 
            Unequipping,
            // 正在播放攻击动画 允许连段 
            Attacking,
        }

        // 宿主控制器引用 获取动画系统 输入 运行时状态等 
        private PlayerController _player;
        // 该剑的实例数据 包含堆叠数量等运行时属性 
        private ItemInstance _instance;
        // 离线配置 包含所有动画 参数 时长等 
        private SwordSO _config;

        // 当前所处的生命周期阶段 
        private SwordPhase _phase = SwordPhase.None;

        // 装备与收起的时长控制 
        private float _equipEndTime;
        private float _unequipEndTime;

        // 连击系统 记录当前在第几段 0-2 对应 Attack1/2/3 
        private int _comboIndex;

        // 上一帧的射击输入状态 用于检测本帧是否为按键上升沿 
        private bool _lastFireInput;

        // 攻击令牌 每次开始新攻击都递增 旧回调如果令牌不匹配则不执行 
        // 这防止了连段时旧动画的回调覆盖新动画的状态 
        private int _attackToken;

        // 可调参数 上半身层级索引 与 PlayerController 配置一致 
        private const int UpperBodyLayer = 1;
        // 动画淡出的默认时长 在 0.15 秒内平滑淡出上半身层 
        private const float DefaultFadeOut = 0.15f;

        // 灵魂注入 获得运行时实例与配置 
        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            // 强转为剑的配置 如果失败说明有严重的装配错误 
            _config = _instance.BaseData as SwordSO;
        }

        // 装备入场 播放拿出动画 初始化状态机 
        public void OnEquipEnter(PlayerController player)
        {
            _player = player;

            // 转入装备阶段 设置超时时间 
            _phase = SwordPhase.Equipping;
            _equipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0f);

            // 初始化连击系统 从第一段开始 
            _comboIndex = 0;
            // 清空上一帧的输入记录 避免装备瞬间误触发攻击 
            _lastFireInput = false;
            _attackToken = 0;

            // 确保上半身层可见 装备动画可能需要在这一层播放 
            if (_player != null && _player.AnimFacade != null)
                _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);

            // 播放拿出动画 此阶段禁止攻击 
            if (_player != null && _player.AnimFacade != null && _config != null && _config.EquipAnim != null)
            {
                var opt = _config.EquipAnimPlayOptions;
                _player.AnimFacade.PlayTransition(_config.EquipAnim, opt);
            }

            // 清空旧的动画结束回调 避免多把剑的回调串线 
            if (_player != null && _player.AnimFacade != null)
                _player.AnimFacade.ClearOnEndCallback(UpperBodyLayer);
        }

        // 逻辑更新 每帧检测输入与阶段转移 
        public void OnUpdateLogic()
        {
            // 完整性检查 任何必要的引用缺失都直接返回 
            if (_player == null || _player.AnimFacade == null || _config == null)
                return;

            // 检测射击按键的上升沿 只在本帧按下时触发 不是按住 
            bool fire = _player.InputReader != null && _player.InputReader.Current.FireHeld;
            bool fireDown = fire && !_lastFireInput;
            _lastFireInput = fire;

            // 中断策略 高优先级的移动意图会中断攻击 确保玩家能快速反应 
            if (_phase == SwordPhase.Attacking && _player.RuntimeData != null)
            {
                var rd = _player.RuntimeData;
                if (rd.WantsToRoll || rd.WantsToDodge || rd.WantsToVault || rd.WantsToJump)
                {
                    CancelAttack();
                    return;
                }
            }

            // 状态机逻辑 每个阶段有不同的行为 
            switch (_phase)
            {
                case SwordPhase.Equipping:
                    // 装备动画播完后转入待机 
                    if (Time.time >= _equipEndTime)
                    {
                        _phase = SwordPhase.Idle;

                        // 需求 装备完成后上半身不播放任何待机动画 
                        // 所以清空层的回调与状态 但保持权重 等待攻击时再激活 
                        _player.AnimFacade.ClearOnEndCallback(UpperBodyLayer);

                        // 不主动播放待机动画 也不调整权重 让上半身系统自己管控 
                    }
                    return;

                case SwordPhase.Unequipping:
                    // 收起动画播完后彻底卸载 
                    if (Time.time >= _unequipEndTime)
                    {
                        _phase = SwordPhase.None;
                    }
                    return;

                case SwordPhase.Idle:
                    // 待机时检测攻击按键 
                    if (fireDown)
                        StartOrChainAttack();
                    return;

                case SwordPhase.Attacking:
                    // 攻击中允许再次按下攻击 立即启动下一段连击 
                    if (fireDown)
                        StartOrChainAttack();
                    return;

                default:
                    return;
            }
        }

        // 强制卸载 通常由上半身状态机或装备管理器触发 
        public void OnForceUnequip()
        {
            if (_player == null || _player.AnimFacade == null)
                return;

            // 强制停止当前攻击 清空所有回调与计时器 
            CancelAttack();

            // 转入卸载阶段 播放收起动画 
            _phase = SwordPhase.Unequipping;
            _unequipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0f);

            // 播放收起动画 此阶段禁止新攻击 
            if (_player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_config != null && _config.UnEquipAnim != null)
                {
                    _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);
                    _player.AnimFacade.PlayTransition(_config.UnEquipAnim, _config.UnEquipAnimPlayOptions);
                }
            }

            // 收起后逐渐淡出上半身层 其他系统可能会重新拉高权重 这里只负责温和淡出 
            _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 0f, DefaultFadeOut);
        }

        // 启动新攻击或接上一个连击 
        private void StartOrChainAttack()
        {
            // 验证必要的对象 
            if (_config == null || _player == null || _player.AnimFacade == null)
                return;

            // 装备与卸载期间绝对禁止攻击 
            if (_phase == SwordPhase.Equipping || _phase == SwordPhase.Unequipping)
                return;

            // 尝试获取当前连击段的动画 失败则终止 
            if (!TryGetAttackClip(_comboIndex, out var clip, out var opt))
                return;

            // 确保上半身层权重足够 攻击动画才能显示 
            _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);

            // 每次新攻击递增令牌 使旧的结束回调失效 
            // 这防止连段时旧动画的回调把新攻击的状态打回 Idle 导致连击链断裂 
            _attackToken++;
            int token = _attackToken;

            // 播放攻击动画到上半身层 
            AnimPlayOptions options = opt;
            options.Layer = UpperBodyLayer;
            _player.AnimFacade.PlayTransition(clip, options);

            // 转入攻击阶段 
            _phase = SwordPhase.Attacking;

            // 播放挥动音效 
            if (_config.SwingSound != null)
                AudioSource.PlayClipAtPoint(_config.SwingSound, transform.position);

            // 绑定动画结束回调 仅当令牌匹配时才执行 
            // 这确保连段时新动画的回调会覆盖旧动画 避免状态混乱 
            _player.AnimFacade.SetOnEndCallback(() =>
            {
                // 令牌不匹配说明这是旧攻击的回调 直接忽略 
                if (token != _attackToken) return;

                // 攻击自然结束 转入待机并淡出上半身层 
                _phase = SwordPhase.Idle;
                _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 0f, DefaultFadeOut);
            }, UpperBodyLayer);

            // 立即递增连击索引 使得攻击中再次按下会执行下一段 
            _comboIndex = (_comboIndex + 1) % 3;
        }

        // 取消当前攻击 用于闪避 翻滚等高优先级动作 
        private void CancelAttack()
        {
            if (_player == null || _player.AnimFacade == null)
                return;

            // 递增令牌 使所有旧回调失效 
            _attackToken++;

            // 清空所有回调 
            _player.AnimFacade.ClearOnEndCallback(UpperBodyLayer);

            // 如果正在攻击则转入待机 并淡出上半身层 
            if (_phase == SwordPhase.Attacking)
            {
                _phase = SwordPhase.Idle;
                _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 0f, DefaultFadeOut);
            }
        }

        // 根据连击索引查询对应的攻击动画与参数 
        private bool TryGetAttackClip(int index, out ClipTransition clip, out AnimPlayOptions options)
        {
            clip = null;
            options = AnimPlayOptions.Default;

            // 根据索引返回不同的动画 
            switch (index)
            {
                case 0:
                    // 第一段攻击 
                    clip = _config.AttackAnim1;
                    options = _config.AttackAnimOptions1;
                    return clip != null && clip.Clip != null;
                case 1:
                    // 第二段攻击 
                    clip = _config.AttackAnim2;
                    options = _config.AttackAnimOptions2;
                    return clip != null && clip.Clip != null;
                case 2:
                    // 第三段攻击 
                    clip = _config.AttackAnim3;
                    options = _config.AttackAnimOptions3;
                    return clip != null && clip.Clip != null;
                default:
                    return false;
            }
        }
    }
}
