using UnityEngine;
using Characters.Player.Input;
using Characters.Player.Data;
using Characters.Player.AI.Sensor;
using Characters.Player.AI.Brain;
using Characters.Player.Core.Attributes; // 引入 UI 黑魔法

namespace Characters.Player.AI.Adapter
{
    [DisallowMultipleComponent]
    public class AICombatInputAdapter : InputSourceBase
    {
        [Header("AI Modules")]
        public NavigatorSensorBase _navigatorSensor;

        // 【核心黑魔法】：多态序列化纯 C# 接口！
        [SubclassSelector]
        [SerializeReference]
        public IAITacticalBrain _brain;

        private bool _lastAttackIntent;
        private bool _lastAimIntent;
        private bool _lastJumpIntent;

        public NavigatorSensorBase NavigatorSensor => _navigatorSensor;
        public IAITacticalBrain Brain => _brain;

        private void Awake()
        {
            if (_navigatorSensor == null)
                _navigatorSensor = GetComponent<NavigatorSensorBase>();

            if (_brain == null)
            {
                Debug.LogError($"[AI 灾难] {_brain} 为空！请在 Inspector 中使用下拉菜单选择战术大脑！", this);
                enabled = false;
                return;
            }

            // 【依赖注入】：纯 C# 类不知道自己长在哪，必须由挂载点把 Transform 喂给它！
            _brain.Initialize(this.transform);
        }

        public override void FetchRawInput(ref RawInputData rawData)
        {
            if (_navigatorSensor == null || _brain == null)
            {
                ClearIntent(ref rawData);
                return;
            }

            ref readonly var context = ref _navigatorSensor.GetCurrentContext();
            ref readonly var intent = ref _brain.EvaluateTactics(in context);

            rawData.MoveAxis = intent.MovementInput;
            rawData.LookAxis = intent.LookInput;

            bool currentAimIntent = intent.WantsToAim;
            rawData.AimHeld = currentAimIntent;

            bool currentAttackIntent = intent.WantsToAttack;
            rawData.Expression1Held = currentAttackIntent;
            rawData.Expression1JustPressed = currentAttackIntent && !_lastAttackIntent;

            // --- 新增跳跃信号映射 ---
            bool currentJumpIntent = intent.WantsToJump;
            rawData.JumpHeld = currentJumpIntent;
            rawData.JumpJustPressed = currentJumpIntent && !_lastJumpIntent; // 精准生成按下瞬间的判定
                                                                             // ------------------------

            _lastAttackIntent = currentAttackIntent;
            _lastAimIntent = currentAimIntent;
            _lastJumpIntent = currentJumpIntent; // 保存当前跳跃状态

            // 清理多余的动作，只留我们要的
            rawData.RollHeld = false;
            rawData.RollJustPressed = false;
            rawData.DodgeHeld = false;
            rawData.DodgeJustPressed = false;
        }

        private void ClearIntent(ref RawInputData rawData)
        {
            rawData.MoveAxis = Vector2.zero;
            rawData.LookAxis = Vector2.zero;
            rawData.AimHeld = false;
            rawData.Expression1Held = false;
            rawData.Expression1JustPressed = false;
            _lastAttackIntent = false;
            _lastAimIntent = false;
        }
    }
}