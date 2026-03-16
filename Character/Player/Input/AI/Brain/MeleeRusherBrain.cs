using System;
using UnityEngine;
using Characters.Player.AI.Data;

namespace Characters.Player.AI.Brain
{
    [Serializable]
    public class MeleeRusherBrain : AITacticalBrainBase
    {
        [SerializeField] private float _engagementRange = 15f;
        [SerializeField] private float _attackRange = 2f;

        [Header("Jump Settings")]
        [SerializeField] private float _jumpCooldown = 1.5f;
        [SerializeField] private float _doubleJumpDelay = 0.35f;

        private float _strafeTimer;
        private float _strafeDirection = 1f;

        // 【跳跃状态机】：超低开销的延时触发机制
        private float _jumpCooldownTimer;
        private float _doubleJumpDelayTimer;
        private int _jumpPhase; // 0=空闲, 1=一段跳已触发, 2=二段跳已触发

        protected override void ProcessTactics(in NavigationContext context)
        {
            float dist = context.DistanceToTarget;
            Vector3 worldDir = context.DesiredWorldDirection;
            Vector2 lookInput = CalculateLookInput(context.TargetWorldDirection);

            // --- 【核心跳跃逻辑】：极简状态机 ---
            bool wantsToJump = false;

            // 冷却计时器递减
            if (_jumpCooldownTimer > 0) _jumpCooldownTimer -= Time.deltaTime;
            if (_doubleJumpDelayTimer > 0) _doubleJumpDelayTimer -= Time.deltaTime;

            // 只有当 Sensor 信号且冷却已过时，才触发第一段跳跃
            if (context.NeedsJump && _jumpCooldownTimer <= 0)
            {
                wantsToJump = true;
                _jumpPhase = 1;  // 标记第一段跳已触发
                _jumpCooldownTimer = _jumpCooldown;  // 启动防连跳冷却
                _doubleJumpDelayTimer = _doubleJumpDelay;  // 启动二段跳延迟
            }
            // 如果一段跳已触发且延迟时间已过，且 Sensor 仍在说"需要跳"（悬崖/卡住），就触发二段跳
            else if (_jumpPhase == 1 && context.NeedsJump && _doubleJumpDelayTimer <= 0)
            {
                wantsToJump = true;
                _jumpPhase = 2;  // 标记二段跳已触发
                _doubleJumpDelayTimer = 1.0f;  // 防止二段跳被多次触发
            }

            // 重置状态：如果 Sensor 不再报告"需要跳"，清理标记
            if (!context.NeedsJump && _jumpCooldownTimer <= 0)
            {
                _jumpPhase = 0;
            }
            // --- 跳跃逻辑结束 ---

            if (dist > _engagementRange)
            {
                // 远距离：猛冲向目标
                _currentIntent = new TacticalIntent(
                    ConvertWorldDirToJoystick(worldDir), 
                    lookInput, 
                    false, 
                    false, 
                    wantsToJump);
            }
            else if (dist > _attackRange)
            {
                // 中距离：迂回战术
                _strafeTimer -= Time.deltaTime;
                if (_strafeTimer <= 0)
                {
                    _strafeDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                    _strafeTimer = UnityEngine.Random.Range(1.5f, 3.5f);
                }

                Vector3 rightDir = Vector3.Cross(Vector3.up, worldDir).normalized;
                Vector3 tacticalDir = (worldDir * 0.4f) + (rightDir * _strafeDirection * 0.8f);

                _currentIntent = new TacticalIntent(
                    ConvertWorldDirToJoystick(tacticalDir.normalized), 
                    lookInput, 
                    false, 
                    true, 
                    wantsToJump);
            }
            else
            {
                // 近距离：贴脸输出（即使贴脸也保留跳跃能力以躲避）
                _currentIntent = new TacticalIntent(
                    Vector2.zero, 
                    lookInput, 
                    true, 
                    false, 
                    wantsToJump);
            }
        }
    }
}