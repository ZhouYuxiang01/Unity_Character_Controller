using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Input;

namespace Characters.Player.Processing.Input
{
    public class InputPipeline
    {
        private readonly IInputSource _inputSource;
        private InputData _inputData;

        private RawInputData _rawData;
        private Vector2 _bufferedMove;
        private float _lastNonZeroMoveTime;

        // 配置参数
        private readonly float _inputFlickerBuffer;
        private readonly float _actionBufferTime;
        private ulong _frameIndex;

        // 对外公开当前输入数据 (PlayerController 通过这个属性来拿数据)
        public InputData Current => _inputData;

        // 【彻底解耦】：绝对不传入 PlayerController，只认 IInputSource！
        public InputPipeline(IInputSource inputSource, float inputFlickerBuffer = 0.05f, float lookSmoothTime = 0.03f, float actionBufferTime = 0.2f)
        {
            _inputSource = inputSource;
            _inputFlickerBuffer = inputFlickerBuffer;
            _actionBufferTime = actionBufferTime;

            // 管线自己作为数据的源头，直接 new 出黑板容器
            _inputData = new InputData();
            _inputData.currentFrameData = new FrameInputData { FrameIndex = 0 };
            _inputData.lastFrameData = new FrameInputData { FrameIndex = 0 };

            _rawData = default;
            _bufferedMove = Vector2.zero;
            _lastNonZeroMoveTime = Time.time;
            _frameIndex = 0;
        }

        public void Update()
        {
            _inputData.lastFrameData = _inputData.currentFrameData;
            _inputSource.FetchRawInput(ref _rawData);
            ProcessRawInput();
            _frameIndex++;
        }

        private void ProcessRawInput()
        {
            var currentFrame = new FrameInputData
            {
                FrameIndex = _frameIndex,
                Raw = _rawData,
                Processed = default
            };

            // ============== 轴向输入处理 ==============
            if (_rawData.MoveAxis.sqrMagnitude > 0.01f)
            {
                _bufferedMove = _rawData.MoveAxis;
                _lastNonZeroMoveTime = Time.time;
                currentFrame.Processed.Move = _rawData.MoveAxis;
            }
            else if (Time.time - _lastNonZeroMoveTime < _inputFlickerBuffer)
            {
                currentFrame.Processed.Move = _bufferedMove;
            }
            else
            {
                currentFrame.Processed.Move = Vector2.zero;
            }

            // 【完美保留】：视角绝对不平滑，原汁原味透传
            currentFrame.Processed.Look = _rawData.LookAxis;

            // ============== 持续状态直传 ==============
            currentFrame.Processed.JumpHeld = _rawData.JumpHeld;
            currentFrame.Processed.DodgeHeld = _rawData.DodgeHeld;
            currentFrame.Processed.RollHeld = _rawData.RollHeld;
            currentFrame.Processed.SprintHeld = _rawData.SprintHeld;
            currentFrame.Processed.WalkHeld = _rawData.WalkHeld;
            currentFrame.Processed.AimHeld = _rawData.AimHeld;
            currentFrame.Processed.InteractHeld = _rawData.InteractHeld;
            // FireHeld 已合并到 LeftMouseHeld，这里只赋值 LeftMouseHeld
            currentFrame.Processed.LeftMouseHeld = _rawData.LeftMouseHeld;
            // 保持兼容性：FireHeld 就是 LeftMouseHeld
            currentFrame.Processed.FireHeld = _rawData.LeftMouseHeld;

            currentFrame.Processed.Expression1Held = _rawData.Expression1Held;
            currentFrame.Processed.Expression2Held = _rawData.Expression2Held;
            currentFrame.Processed.Expression3Held = _rawData.Expression3Held;
            currentFrame.Processed.Expression4Held = _rawData.Expression4Held;

            currentFrame.Processed.Number1Held = _rawData.Number1Held;
            currentFrame.Processed.Number2Held = _rawData.Number2Held;
            currentFrame.Processed.Number3Held = _rawData.Number3Held;
            currentFrame.Processed.Number4Held = _rawData.Number4Held;
            currentFrame.Processed.Number5Held = _rawData.Number5Held;

            currentFrame.Processed.WaveHeld = _rawData.WaveHeld;

            // ============== 核心魔法：动作缓存池管理 ==============
            float dt = Time.deltaTime;
            var lastProc = _inputData.lastFrameData.Processed;

            float UpdateBuffer(float lastTimer, bool justPressed)
            {
                float newTimer = Mathf.Max(0f, lastTimer - dt);
                if (justPressed) newTimer = _actionBufferTime;
                return newTimer;
            }

            currentFrame.Processed.JumpBufferTimer = UpdateBuffer(lastProc.JumpBufferTimer, _rawData.JumpJustPressed);
            currentFrame.Processed.DodgeBufferTimer = UpdateBuffer(lastProc.DodgeBufferTimer, _rawData.DodgeJustPressed);
            currentFrame.Processed.RollBufferTimer = UpdateBuffer(lastProc.RollBufferTimer, _rawData.RollJustPressed);
            // FireBufferTimer 合并到 LeftMouseBufferTimer
            currentFrame.Processed.LeftMouseBufferTimer = UpdateBuffer(lastProc.LeftMouseBufferTimer, _rawData.LeftMouseJustPressed);
            // 保持兼容性：FireBufferTimer 就是 LeftMouseBufferTimer
            currentFrame.Processed.FireBufferTimer = currentFrame.Processed.LeftMouseBufferTimer;

            currentFrame.Processed.Expression1BufferTimer = UpdateBuffer(lastProc.Expression1BufferTimer, _rawData.Expression1JustPressed);
            currentFrame.Processed.Expression2BufferTimer = UpdateBuffer(lastProc.Expression2BufferTimer, _rawData.Expression2JustPressed);
            currentFrame.Processed.Expression3BufferTimer = UpdateBuffer(lastProc.Expression3BufferTimer, _rawData.Expression3JustPressed);
            currentFrame.Processed.Expression4BufferTimer = UpdateBuffer(lastProc.Expression4BufferTimer, _rawData.Expression4JustPressed);

            currentFrame.Processed.Number1BufferTimer = UpdateBuffer(lastProc.Number1BufferTimer, _rawData.Number1JustPressed);
            currentFrame.Processed.Number2BufferTimer = UpdateBuffer(lastProc.Number2BufferTimer, _rawData.Number2JustPressed);
            currentFrame.Processed.Number3BufferTimer = UpdateBuffer(lastProc.Number3BufferTimer, _rawData.Number3JustPressed);
            currentFrame.Processed.Number4BufferTimer = UpdateBuffer(lastProc.Number4BufferTimer, _rawData.Number4JustPressed);
            currentFrame.Processed.Number5BufferTimer = UpdateBuffer(lastProc.Number5BufferTimer, _rawData.Number5JustPressed);

            currentFrame.Processed.WaveBufferTimer = UpdateBuffer(lastProc.WaveBufferTimer, _rawData.WaveJustPressed);

            // 【注意】：这里把给 JumpPressed 赋值的代码全删了！
            // 因为在 InputData.cs 里，JumpPressed 应该写成：public bool JumpPressed => JumpBufferTimer > 0f;
            // 这样只要 Timer 归零，Pressed 瞬间就变成 false，绝对不会产生连跳 Bug！

            _inputData.currentFrameData = currentFrame;
        }

        // ============== 消费接口 ==============
        public void ConsumeJumpPressed() { var f = _inputData.currentFrameData; f.Processed.JumpBufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeDodgePressed() { var f = _inputData.currentFrameData; f.Processed.DodgeBufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeRollPressed() { var f = _inputData.currentFrameData; f.Processed.RollBufferTimer = 0f; _inputData.currentFrameData = f; }
        // ConsumeFirePressed 保持向后兼容，内部调用 ConsumeLeftMousePressed
        public void ConsumeFirePressed() => ConsumeLeftMousePressed();

        public void ConsumeExpression1Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression1BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression2Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression2BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression3Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression3BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression4Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression4BufferTimer = 0f; _inputData.currentFrameData = f; }

        public void ConsumeNumber1Pressed() { var f = _inputData.currentFrameData; f.Processed.Number1BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber2Pressed() { var f = _inputData.currentFrameData; f.Processed.Number2BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber3Pressed() { var f = _inputData.currentFrameData; f.Processed.Number3BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber4Pressed() { var f = _inputData.currentFrameData; f.Processed.Number4BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber5Pressed() { var f = _inputData.currentFrameData; f.Processed.Number5BufferTimer = 0f; _inputData.currentFrameData = f; }

        public void ConsumeWavePressed() { var f = _inputData.currentFrameData; f.Processed.WaveBufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeLeftMousePressed() { var f = _inputData.currentFrameData; f.Processed.LeftMouseBufferTimer = 0f; _inputData.currentFrameData = f; }
    }
}