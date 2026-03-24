using UnityEngine;
#if BBBNEXUS_HAS_UAR
using UnityEngine.Animations.Rigging;
#endif

namespace BBBNexus
{
    //注意 我没有支持UAR的脚部ik 因为如果要这么适配 就得打一堆特化逻辑 还会破坏上级的架构设计
    //原生的UAR如果需要适配 就要写一堆数学类 也是原因之一(绝对不是因为我懒得适配)
    //另外 瞄准的ik逻辑也是空置的 原因相同
    public class UnityAnimationRiggingSource : PlayerIKSourceBase
    {
#if BBBNEXUS_HAS_UAR
        [Header("Hand IK Components")]
        [SerializeField] private TwoBoneIKConstraint _leftHandIK;
        [SerializeField] private TwoBoneIKConstraint _rightHandIK;

        [Header("Hand IK Targets (Proxies)")]
        // 这是 Prefab 内部固定的空物体 (Target_Hand_L / R)
        // IK Constraint 的 data.target 永远指向它们，不要改引用！
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField] private Transform _rightHandTarget;

        [Header("Head LookAt IK")]
        [SerializeField] private MultiAimConstraint _headLookAtIK;
        [SerializeField] private Transform _lookAtTarget; // Prefab 内部固定的 LookAt_Target

        // [可选] 用于将来控制 Layer 权重
        [SerializeField] private RigBuilder _rigBuilder;

        // --- 接口实现 ---

        public override void SetIKTarget(IKTarget target, Transform targetTransform, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_leftHandIK != null && _leftHandTarget != null)
                    {
                        // 1. 更新代理 Target 的位置 (核心逻辑)
                        if (targetTransform != null)
                        {
                            _leftHandTarget.position = targetTransform.position;
                            _leftHandTarget.rotation = targetTransform.rotation;
                        }

                        // 2. 更新权重
                        _leftHandIK.weight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_rightHandIK != null && _rightHandTarget != null)
                    {
                        // 1. 更新代理 Target 的位置
                        if (targetTransform != null)
                        {
                            _rightHandTarget.position = targetTransform.position;
                            _rightHandTarget.rotation = targetTransform.rotation;
                        }

                        // 2. 更新权重
                        _rightHandIK.weight = weight;
                    }
                    break;
            }
        }

        public override void SetIKTarget(IKTarget target, Vector3 position, Quaternion rotation, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_leftHandIK != null && _leftHandTarget != null)
                    {
                        _leftHandTarget.position = position;
                        _leftHandTarget.rotation = rotation;
                        _leftHandIK.weight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_rightHandIK != null && _rightHandTarget != null)
                    {
                        _rightHandTarget.position = position;
                        _rightHandTarget.rotation = rotation;
                        _rightHandIK.weight = weight;
                    }
                    break;

                case IKTarget.HeadLook:
                    if (_lookAtTarget != null)
                    {
                        _lookAtTarget.position = position;
                        if (_headLookAtIK != null)
                        {
                            _headLookAtIK.weight = weight;
                        }
                    }
                    break;
            }
        }

        public override void UpdateIKWeight(IKTarget target, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_leftHandIK != null) _leftHandIK.weight = weight;
                    break;

                case IKTarget.RightHand:
                    if (_rightHandIK != null) _rightHandIK.weight = weight;
                    break;

                case IKTarget.HeadLook:
                    if (_headLookAtIK != null) _headLookAtIK.weight = weight;
                    break;
            }
        }
        public override void EnableAllIK()
        {
            if (_rigBuilder != null)
            {
                _rigBuilder.enabled = true;
            }
            else
            {
                if (_leftHandIK != null) _leftHandIK.enabled = true;
                if (_rightHandIK != null) _rightHandIK.enabled = true;
                if (_headLookAtIK != null) _headLookAtIK.enabled = true;
            }
        }

        public override void DisableAllIK()
        {
            if (_rigBuilder != null)
            {
                _rigBuilder.enabled = false;
            }
            else
            {
                if (_leftHandIK != null) _leftHandIK.enabled = false;
                if (_rightHandIK != null) _rightHandIK.enabled = false;
                if (_headLookAtIK != null) _headLookAtIK.enabled = false;
            }
        }
#else
        // 【为何这里保留了空方法？这不是没用的废代码！】
        // 工作原理：当项目里没有导入 UAR 插件时，宏 BBBNEXUS_HAS_UAR 就会失效。
        // 上面所有的实际逻辑都会被编译器忽略。但由于本类继承自 PlayerIKSourceBase，
        // 必须要实现接口里的所有抽象方法（SetIKTarget 等函数）。如果让它们也被清理掉，
        // Unity 编译这部分代码时就会因为“派生类未完全实现抽象接口”而直接红字报错！
        // 因此，写下这些空函数不仅极有必要，还能保证原本挂在角色身上的预制体脚本引用不丢失。
        public override void SetIKTarget(IKTarget target, Transform targetTransform, float weight) { }
        public override void SetIKTarget(IKTarget target, Vector3 position, Quaternion rotation, float weight) { }
        public override void UpdateIKWeight(IKTarget target, float weight) { }
        public override void EnableAllIK() { }
        public override void DisableAllIK() { }
#endif
    }
}