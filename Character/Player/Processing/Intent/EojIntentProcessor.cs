using Characters.Player.Data;

namespace Characters.Player.Processing
{
    /// <summary>
    /// Expression intent processor.
    /// Reads expression input events and writes one-frame intents into RuntimeData (blackboard).
    /// </summary>
    public class EojIntentProcessor
    {
        private readonly PlayerController _player;
        private readonly PlayerRuntimeData _data;

        public EojIntentProcessor(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;

            if (_player != null && _player.InputReader != null)
            {
                _player.InputReader.OnExpression1Pressed += HandleExpression1;
                _player.InputReader.OnExpression2Pressed += HandleExpression2;
                _player.InputReader.OnExpression3Pressed += HandleExpression3;
                _player.InputReader.OnExpression4Pressed += HandleExpression4;
            }
        }

        ~EojIntentProcessor()
        {
            if (_player != null && _player.InputReader != null)
            {
                _player.InputReader.OnExpression1Pressed -= HandleExpression1;
                _player.InputReader.OnExpression2Pressed -= HandleExpression2;
                _player.InputReader.OnExpression3Pressed -= HandleExpression3;
                _player.InputReader.OnExpression4Pressed -= HandleExpression4;
            }
        }

        public void Update()
        {
            // No per-frame scanning needed. Intents are set by events and cleared in RuntimeData.ResetIntetnt().
        }

        private void HandleExpression1() => _data.WantsExpression1 = true;
        private void HandleExpression2() => _data.WantsExpression2 = true;
        private void HandleExpression3() => _data.WantsExpression3 = true;
        private void HandleExpression4() => _data.WantsExpression4 = true;
    }
}
