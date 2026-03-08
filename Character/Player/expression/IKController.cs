using UnityEngine;
using Characters.Player.Core;
using Characters.Player.Data;

namespace Characters.Player.Expression
{
    // IK 结算控制器
    // 负责处理左手/右手的 IK 权重 平滑追踪 Aim IK 与 Warp IK 的拦截逻辑
    // 它从运行时黑板读取意图并将目标与权重下发到 IPlayerIKSource
    public class IKController
    {
        private PlayerController _player;
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        private Vector3 _currentLookAtPosition;
        private Vector3 _lookAtPositionVelocity;
        private float _lookAtPositionSmoothTime;

        private IPlayerIKSource _ikSource => _player.IKSource;
        private Transform _lastAimReference = null;

        // --- 运行时平滑状态 ---
        private float _leftHandWeight;
        private float _leftHandVelocity;

        private float _rightHandWeight;
        private float _rightHandVelocity;

        private float _lookAtWeight;
        private float _lookAtVelocity;

        public IKController(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _config = player.Config;
            _lookAtPositionSmoothTime = _config.Aiming.AimIkChaseSmoothTime;
        }

        // 每帧调用一次，由 PlayerController 在主循环中更新
        // 优先级从高到低：Warp IK 拦截 -> Aim 基准点更新 -> 左手 IK -> 右手 IK -> 头部注视
        public void Update()
        {
            if (_ikSource == null) return;

            // =================================================================================
            // 0. 翻越/攀爬 Warp IK 拦截 (最高优先级)
            // =================================================================================
            if (_data.IsWarping)
            {
                float warpHandWeight = _data.ActiveWarpData.HandIKWeightCurve.Evaluate(_data.NormalizedWarpTime);
                if (warpHandWeight > 0.01f)
                {
                    _ikSource.SetIKTarget(IKTarget.LeftHand, _data.WarpIKTarget_LeftHand, _data.WarpIKRotation_Hand, warpHandWeight);
                    _ikSource.SetIKTarget(IKTarget.RightHand, _data.WarpIKTarget_RightHand, _data.WarpIKRotation_Hand, warpHandWeight);
                    return; // 阻断普通 IK
                }
                return;
            }

            // =================================================================================
            // 1. AimIK 基准点 (Muzzle) 更新
            // =================================================================================
            if (_data.IsAiming)
            {
                if (_data.CurrentAimReference != _lastAimReference)
                {
                    _ikSource.SetIKTarget(IKTarget.AimReference, _data.CurrentAimReference, 1f);
                    _lastAimReference = _data.CurrentAimReference;
                }
            }

            // =================================================================================
            // 2. 左手 IK 处理
            // =================================================================================
            float targetLeftW = _data.WantsLeftHandIK ? 1f : 0f;
            _leftHandWeight = Mathf.SmoothDamp(_leftHandWeight, targetLeftW, ref _leftHandVelocity, 0.15f);

            if (_leftHandWeight > 0.01f && _data.LeftHandGoal != null && _data.WantsLeftHandIK)
                _ikSource.SetIKTarget(IKTarget.LeftHand, _data.LeftHandGoal, _leftHandWeight);
            else
            {
                _ikSource.UpdateIKWeight(IKTarget.LeftHand, 0f);
                if (_leftHandWeight < 0.01f) _leftHandVelocity = 0f;
            }

            // =================================================================================
            // 3. 右手 IK 处理
            // =================================================================================
            float targetRightW = _data.WantsRightHandIK ? 1f : 0f;
            _rightHandWeight = Mathf.SmoothDamp(_rightHandWeight, targetRightW, ref _rightHandVelocity, 0.15f);

            if (_rightHandWeight > 0.01f && _data.RightHandGoal != null)
                _ikSource.SetIKTarget(IKTarget.RightHand, _data.RightHandGoal, _rightHandWeight);
            else
            {
                _ikSource.UpdateIKWeight(IKTarget.RightHand, 0f);
                if (_rightHandWeight < 0.01f) _rightHandVelocity = 0f;
            }

            // =================================================================================
            // 4. 头部注视 / 武器瞄准 (LookAt / Aim) 统一处理
            // =================================================================================
            float targetLookW = _data.WantsLookAtIK ? 1f : 0f;
            // 将淡出速度从0.2f提升到0.08f，加快AimIK权重淡出
            _lookAtWeight = Mathf.SmoothDamp(_lookAtWeight, targetLookW, ref _lookAtVelocity, 0.2f);

            if (_lookAtWeight > 0.01f)
            {
                Vector3 desiredTarget = _data.TargetAimPoint;

                _currentLookAtPosition = Vector3.SmoothDamp(
                    _currentLookAtPosition,
                    desiredTarget,
                    ref _lookAtPositionVelocity,
                    _lookAtPositionSmoothTime
                );

                _ikSource.SetIKTarget(
                    IKTarget.HeadLook,
                    _currentLookAtPosition,
                    Quaternion.identity,
                    _lookAtWeight
                );
            }
            else
            {
                _ikSource.UpdateIKWeight(IKTarget.HeadLook, 0f);
                if (_lookAtWeight < 0.01f)
                {
                    _lookAtVelocity = 0f;
                    _currentLookAtPosition = _player.transform.position + _player.transform.forward * 5f;
                }
            }
        }
    }
}