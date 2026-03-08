using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

namespace Characters.Player.Input
{
    [System.Serializable]
    public struct PlayerInputFrame
    {
        public Vector2 Move;
        public Vector2 Look;

        public bool JumpPressed;
        public bool JumpHeld;

        public bool DodgePressed;
        public bool DodgeHeld;

        public bool RollPressed;
        public bool RollHeld;

        public bool SprintHeld;
        public bool WalkHeld;
        public bool AimHeld;

        public bool InteractPressed;
        public bool InteractHeld;

        public bool FirePressed;
        public bool FireHeld;

        public bool Expression1Pressed;
        public bool Expression2Pressed;
        public bool Expression3Pressed;
        public bool Expression4Pressed;

        public bool Number1Pressed;
        public bool Number2Pressed;
        public bool Number3Pressed;
        public bool Number4Pressed;
        public bool Number5Pressed;

        public bool WavePressed;
        public bool InteractHeldState;
        public bool LeftMousePressed;
        public bool LeftMouseHeld;

        public bool Expression1Held;
        public bool Expression2Held;
        public bool Expression3Held;
        public bool Expression4Held;

        public bool Number1Held;
        public bool Number2Held;
        public bool Number3Held;
        public bool Number4Held;
        public bool Number5Held;

        public bool WaveHeld;
    }

    public class PlayerInputReader : MonoBehaviour
    {
        #region 1. 配置参数
        [Header("视角设置")]
        public float mouseSensitivity = 1f;
        public bool invertMouseX = false;
        public bool invertMouseY = false;
        [Range(0f, 0.1f)] public float lookSmoothTime = 0.03f; // 视角微平滑，提升3A感

        [Header("输入缓冲设置")]
        [SerializeField] private float _inputFlickerBuffer = 0.05f; // 解决闪避清零
        #endregion

        #region 2. 对外接口 (直接提供当前帧数据)
        /// <summary>
        /// 获取当前帧的输入快照。外部系统应直接读取此属性，而不是旧版的单个属性。
        /// </summary>
        public PlayerInputFrame Current => _currentFrame;
        #endregion

        #region 3. Action 引用 (由 Inspector 赋值)
        [Header("输入动作引用")]
        public InputActionReference moveAction;
        public InputActionReference lookAction;
        public InputActionReference jumpAction;
        public InputActionReference sprintAction;
        public InputActionReference walkAction;
        public InputActionReference aimAction;
        public InputActionReference dodgeAction;
        public InputActionReference rollAction;
        public InputActionReference waveAction;
        public InputActionReference LeftMouseAction;
        public InputActionReference number1Action;
        public InputActionReference number2Action;
        public InputActionReference number3Action;
        public InputActionReference number4Action;
        public InputActionReference number5Action;
        public InputActionReference fireAction;

        [Header("表情输入引用")]
        public InputActionReference expression1Action;
        public InputActionReference expression2Action;
        public InputActionReference expression3Action;
        public InputActionReference expression4Action;
        #endregion

        #region 4. 内部状态
        private PlayerInputFrame _currentFrame;
        private PlayerInputFrame _lastFrame;

        private Vector2 _bufferedMove;
        private float _lastNonZeroMoveTime;
        private Vector2 _currentLookVelocity; // 用于视角平滑
        #endregion

        private void OnEnable() => ToggleActions(true);
        private void OnDisable() => ToggleActions(false);

        private void Update()
        {
            _lastFrame = _currentFrame;
            _currentFrame = GatherInputFrame();
        }

        private PlayerInputFrame GatherInputFrame()
        {
            PlayerInputFrame frame = new PlayerInputFrame();

            // 移动采样 (带防抖
            Vector2 rawMove = moveAction.action.ReadValue<Vector2>();
            if (rawMove.sqrMagnitude > 0.01f)
            {
                _bufferedMove = rawMove;
                _lastNonZeroMoveTime = Time.time;
            }
            else if (Time.time - _lastNonZeroMoveTime < _inputFlickerBuffer)
            {
                rawMove = _bufferedMove;
            }
            frame.Move = rawMove;

            // 视角采样
            Vector2 rawLook = lookAction.action.ReadValue<Vector2>();
            rawLook.x *= mouseSensitivity * (invertMouseX ? -1f : 1f);
            rawLook.y *= mouseSensitivity * (invertMouseY ? -1f : 1f);
            frame.Look = Vector2.SmoothDamp(_lastFrame.Look, rawLook, ref _currentLookVelocity, lookSmoothTime);

            // 状态采样
            frame.JumpHeld = jumpAction.action.IsPressed();
            frame.DodgeHeld = dodgeAction.action.IsPressed();
            frame.RollHeld = rollAction.action.IsPressed();
            frame.SprintHeld = sprintAction.action.IsPressed();
            frame.WalkHeld = walkAction.action.IsPressed();
            frame.AimHeld = aimAction.action.IsPressed();
            frame.FireHeld = fireAction.action.IsPressed();

            // 手动边沿检测
            frame.JumpPressed = frame.JumpHeld && !_lastFrame.JumpHeld;
            frame.DodgePressed = frame.DodgeHeld && !_lastFrame.DodgeHeld;
            frame.RollPressed = frame.RollHeld && !_lastFrame.RollHeld;
            frame.FirePressed = frame.FireHeld && !_lastFrame.FireHeld;

            // 表情采样 + 边沿检测
            frame.Expression1Held = expression1Action != null && expression1Action.action.IsPressed();
            frame.Expression2Held = expression2Action != null && expression2Action.action.IsPressed();
            frame.Expression3Held = expression3Action != null && expression3Action.action.IsPressed();
            frame.Expression4Held = expression4Action != null && expression4Action.action.IsPressed();

            frame.Expression1Pressed = frame.Expression1Held && !_lastFrame.Expression1Held;
            frame.Expression2Pressed = frame.Expression2Held && !_lastFrame.Expression2Held;
            frame.Expression3Pressed = frame.Expression3Held && !_lastFrame.Expression3Held;
            frame.Expression4Pressed = frame.Expression4Held && !_lastFrame.Expression4Held;

            // 数字快捷键采样 + 边沿检测（修复：不能用 lastFrame.NumberXPressed 来做边沿）
            frame.Number1Held = number1Action != null && number1Action.action.IsPressed();
            frame.Number2Held = number2Action != null && number2Action.action.IsPressed();
            frame.Number3Held = number3Action != null && number3Action.action.IsPressed();
            frame.Number4Held = number4Action != null && number4Action.action.IsPressed();
            frame.Number5Held = number5Action != null && number5Action.action.IsPressed();

            frame.Number1Pressed = frame.Number1Held && !_lastFrame.Number1Held;
            frame.Number2Pressed = frame.Number2Held && !_lastFrame.Number2Held;
            frame.Number3Pressed = frame.Number3Held && !_lastFrame.Number3Held;
            frame.Number4Pressed = frame.Number4Held && !_lastFrame.Number4Held;
            frame.Number5Pressed = frame.Number5Held && !_lastFrame.Number5Held;

            // 其他功能键采样 + 边沿检测 (Pressed)
            frame.WaveHeld = waveAction != null && waveAction.action.IsPressed();
            bool leftMouseHeld = LeftMouseAction != null && LeftMouseAction.action.IsPressed();

            frame.WavePressed = frame.WaveHeld && !_lastFrame.WaveHeld;
            frame.LeftMousePressed = leftMouseHeld && !_lastFrame.LeftMousePressed;
            frame.LeftMouseHeld = leftMouseHeld;

            return frame;
        }

        #region 消费逻辑 
        public void ConsumeJump() => _currentFrame.JumpPressed = false;
        public void ConsumeDodge() => _currentFrame.DodgePressed = false;
        public void ConsumeRoll() => _currentFrame.RollPressed = false;
        public void ConsumeExpression1() => _currentFrame.Expression1Pressed = false;
        public void ConsumeExpression2() => _currentFrame.Expression2Pressed = false;
        public void ConsumeExpression3() => _currentFrame.Expression3Pressed = false;
        public void ConsumeExpression4() => _currentFrame.Expression4Pressed = false;
        public void ConsumeNumber1() => _currentFrame.Number1Pressed = false;
        public void ConsumeNumber2() => _currentFrame.Number2Pressed = false;
        public void ConsumeNumber3() => _currentFrame.Number3Pressed = false;
        public void ConsumeNumber4() => _currentFrame.Number4Pressed = false;
        public void ConsumeNumber5() => _currentFrame.Number5Pressed = false;
        public void ConsumeWave() => _currentFrame.WavePressed = false;
        public void ConsumeLeftMouse() => _currentFrame.LeftMousePressed = false;
        #endregion

        #region 事件绑定 
        private void ToggleActions(bool enable)
        {
            InputActionReference[] all = {
                moveAction, lookAction, jumpAction, sprintAction, walkAction,
                aimAction, dodgeAction, rollAction, waveAction, LeftMouseAction,
                number1Action, number2Action, number3Action, number4Action, number5Action, fireAction,
                expression1Action, expression2Action, expression3Action, expression4Action
            };

            foreach (var ar in all)
            {
                if (ar == null) continue;
                if (enable)
                {
                    ar.action.Enable();
                }
                else
                {
                    ar.action.Disable();
                }
            }
        }
        #endregion
    }
}
