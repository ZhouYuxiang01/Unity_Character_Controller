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

        // 动作状态：手动边沿检测，支持 Consume 改写
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

        // Expression
        public bool Expression1Pressed;
        public bool Expression2Pressed;
        public bool Expression3Pressed;
        public bool Expression4Pressed;
    }

    public class PlayerInputReader : MonoBehaviour
    {
        #region 1. 配置参数
        [Header("Mouse Look Settings")]
        public float mouseSensitivity = 1f;
        public bool invertMouseX = false;
        public bool invertMouseY = false;
        [Range(0f, 0.1f)] public float lookSmoothTime = 0.03f; // 视角微平滑，提升3A感

        [Header("Buffer Settings")]
        [SerializeField] private float _inputFlickerBuffer = 0.05f; // 解决闪避清零
        #endregion

        #region 2. 对外接口 (兼容旧版)
        public Vector2 MoveInput => _currentFrame.Move;
        public Vector2 LookInput => _currentFrame.Look;
        public bool IsJumpPressed => _currentFrame.JumpPressed;
        public bool IsSprinting => _currentFrame.SprintHeld;
        public bool IsWalking => _currentFrame.WalkHeld;
        public bool IsAiming => _currentFrame.AimHeld;
        public bool IsDodgePressed => _currentFrame.DodgePressed;
        public bool IsRollPressed => _currentFrame.RollPressed;
        public bool FireInput => _currentFrame.FireHeld;

        public PlayerInputFrame Current => _currentFrame;
        #endregion

        #region 3. 事件回调 (兼容旧版)
        public UnityAction OnWavePressed;
        public UnityAction OnLeftMouseDown;
        public UnityAction OnLeftMouseUp;
        public UnityAction OnJumpPressed; // 注意：这是旧版事件，新逻辑建议直接读 Current.JumpPressed
        public UnityAction OnAimStarted;
        public UnityAction OnAimCanceled;
        public UnityAction OnNumber1Pressed;
        public UnityAction OnNumber2Pressed;
        public UnityAction OnNumber3Pressed;
        public UnityAction OnNumber4Pressed;
        public UnityAction OnNumber5Pressed;

        // Expression events
        public UnityAction OnExpression1Pressed;
        public UnityAction OnExpression2Pressed;
        public UnityAction OnExpression3Pressed;
        public UnityAction OnExpression4Pressed;
        #endregion

        #region 4. Action 引用 (由 Inspector 赋值)
        [Header("Input References")]
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

        [Header("Expression Input References")]
        public InputActionReference expression1Action;
        public InputActionReference expression2Action;
        public InputActionReference expression3Action;
        public InputActionReference expression4Action;
        #endregion

        #region 5. 内部状态
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

            // 触发旧版事件回调 (如果这一帧刚刚按下)
            if (_currentFrame.JumpPressed) OnJumpPressed?.Invoke();

            if (_currentFrame.Expression1Pressed) OnExpression1Pressed?.Invoke();
            if (_currentFrame.Expression2Pressed) OnExpression2Pressed?.Invoke();
            if (_currentFrame.Expression3Pressed) OnExpression3Pressed?.Invoke();
            if (_currentFrame.Expression4Pressed) OnExpression4Pressed?.Invoke();
        }

        private PlayerInputFrame GatherInputFrame()
        {
            PlayerInputFrame frame = new PlayerInputFrame();

            // --- A. 移动采样 (带防抖) ---
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

            // --- B. 视角采样 (带灵敏度、反转、平滑) ---
            Vector2 rawLook = lookAction.action.ReadValue<Vector2>();
            rawLook.x *= mouseSensitivity * (invertMouseX ? -1f : 1f);
            rawLook.y *= mouseSensitivity * (invertMouseY ? -1f : 1f);
            frame.Look = Vector2.SmoothDamp(_lastFrame.Look, rawLook, ref _currentLookVelocity, lookSmoothTime);

            // --- C. 状态采样 (Held) ---
            frame.JumpHeld = jumpAction.action.IsPressed();
            frame.DodgeHeld = dodgeAction.action.IsPressed();
            frame.RollHeld = rollAction.action.IsPressed();
            frame.SprintHeld = sprintAction.action.IsPressed();
            frame.WalkHeld = walkAction.action.IsPressed();
            frame.AimHeld = aimAction.action.IsPressed();
            frame.FireHeld = fireAction.action.IsPressed();

            // --- D. 手动边沿检测 (Pressed) ---
            frame.JumpPressed = frame.JumpHeld && !_lastFrame.JumpHeld;
            frame.DodgePressed = frame.DodgeHeld && !_lastFrame.DodgeHeld;
            frame.RollPressed = frame.RollHeld && !_lastFrame.RollHeld;
            frame.FirePressed = frame.FireHeld && !_lastFrame.FireHeld;

            // Expression edge detection (Pressed)
            bool ex1Held = expression1Action != null && expression1Action.action.IsPressed();
            bool ex2Held = expression2Action != null && expression2Action.action.IsPressed();
            bool ex3Held = expression3Action != null && expression3Action.action.IsPressed();
            bool ex4Held = expression4Action != null && expression4Action.action.IsPressed();

            frame.Expression1Pressed = ex1Held && !_lastFrame.Expression1Pressed;
            frame.Expression2Pressed = ex2Held && !_lastFrame.Expression2Pressed;
            frame.Expression3Pressed = ex3Held && !_lastFrame.Expression3Pressed;
            frame.Expression4Pressed = ex4Held && !_lastFrame.Expression4Pressed;

            return frame;
        }

        #region 消费逻辑 (真正可控)
        public void ConsumeJump() => _currentFrame.JumpPressed = false;
        public void ConsumeDodge() => _currentFrame.DodgePressed = false;
        public void ConsumeRoll() => _currentFrame.RollPressed = false;
        public void ConsumeExpression1() => _currentFrame.Expression1Pressed = false;
        public void ConsumeExpression2() => _currentFrame.Expression2Pressed = false;
        public void ConsumeExpression3() => _currentFrame.Expression3Pressed = false;
        public void ConsumeExpression4() => _currentFrame.Expression4Pressed = false;
        #endregion

        #region 事件绑定 (保持旧版兼容)
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
                    if (ar == waveAction) ar.action.performed += _ => OnWavePressed?.Invoke();
                    if (ar == aimAction) { ar.action.started += _ => OnAimStarted?.Invoke(); ar.action.canceled += _ => OnAimCanceled?.Invoke(); }
                    if (ar == LeftMouseAction) { ar.action.started += _ => OnLeftMouseDown?.Invoke(); ar.action.canceled += _ => OnLeftMouseUp?.Invoke(); }
                    if (ar == number1Action) ar.action.performed += _ => OnNumber1Pressed?.Invoke();
                    if (ar == number2Action) ar.action.performed += _ => OnNumber2Pressed?.Invoke();
                    if (ar == number3Action) ar.action.performed += _ => OnNumber3Pressed?.Invoke();
                    if (ar == number4Action) ar.action.performed += _ => OnNumber4Pressed?.Invoke();
                    if (ar == number5Action) ar.action.performed += _ => OnNumber5Pressed?.Invoke();

                    if (ar == expression1Action) ar.action.performed += _ => OnExpression1Pressed?.Invoke();
                    if (ar == expression2Action) ar.action.performed += _ => OnExpression2Pressed?.Invoke();
                    if (ar == expression3Action) ar.action.performed += _ => OnExpression3Pressed?.Invoke();
                    if (ar == expression4Action) ar.action.performed += _ => OnExpression4Pressed?.Invoke();
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
