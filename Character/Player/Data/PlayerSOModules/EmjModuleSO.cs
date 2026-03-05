using UnityEngine;
using Animancer;
using Characters.Player.Animation;

namespace Characters.Player.Data
{
    /// <summary>
    /// 表情模块配置：包含一个基础表情动画（循环）和四个瞬时/特殊表情动画。
    /// </summary>
    [CreateAssetMenu(fileName = "EmjModule", menuName = "Player/Modules/Emj Module")]
    public class EmjModuleSO : ScriptableObject
    {
        [Header("Base Expression")]
        [Tooltip("基础表情")]
        public ClipTransition BaseExpression;

        [Header("Special Expressions")]
        [Tooltip("特殊表情 1")]
        public ClipTransition SpecialExpression1;

        [Tooltip("特殊表情 2")]
        public ClipTransition SpecialExpression2;

        [Tooltip("特殊表情 3")]
        public ClipTransition SpecialExpression3;

        [Tooltip("特殊表情 4")]
        public ClipTransition SpecialExpression4;
    }
}