using UnityEngine;
using Characters.Player.AI.Data;

namespace Characters.Player.AI.Brain
{
    public interface IAITacticalBrain
    {
        void Initialize(Transform selfTransform);

        ref readonly TacticalIntent EvaluateTactics(in NavigationContext context);
    }
}