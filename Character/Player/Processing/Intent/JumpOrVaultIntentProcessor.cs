using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Processing
{
    // 跳跃与翻越意图处理器 它是高段移动的决策中枢 
    // 负责检测翻越障碍物 仲裁跳跃 翻越 二段跳的优先级 
    public class JumpOrVaultIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly Transform _playerTransform;
        private readonly LayerMask _obstacleMask;

        // 缓存有效的翻越信息 使得按键时能快速获取最新的障碍物数据 
        private VaultObstacleInfo _lastValidLowVaultInfo;
        private float _lastValidLowVaultTime;
        private VaultObstacleInfo _lastValidHighVaultInfo;
        private float _lastValidHighVaultTime;

        // 构造函数：只注入纯粹的数据和物理锚点 (Transform)
        public JumpOrVaultIntentProcessor(PlayerRuntimeData data, PlayerSO config, Transform playerTransform)
        {
            _data = data;
            _config = config;
            _playerTransform = playerTransform;
            _obstacleMask = _config.Vaulting.ObstacleLayers;
        }

        // 返回值 bool：告诉总管家“我是否成功处理(消耗)了跳跃意图”
        public bool Update(in ProcessedInputData input)
        {
            // 实时扫描环境并更新缓存 但只在地面上进行以节省性能
            if (_data.IsGrounded)
            {
                if (DetectObstacle(out VaultObstacleInfo lowInfo, _config.Vaulting.LowVaultMinHeight, _config.Vaulting.LowVaultMaxHeight, false))
                {
                    _lastValidLowVaultInfo = lowInfo;
                    _lastValidLowVaultTime = Time.time;
                }

                if (DetectObstacle(out VaultObstacleInfo highInfo, _config.Vaulting.HighVaultMinHeight, _config.Vaulting.HighVaultMaxHeight, false))
                {
                    _lastValidHighVaultInfo = highInfo;
                    _lastValidHighVaultTime = Time.time;
                }
            }

            // 0拷贝读取快照的属性
            if (input.JumpPressed)
            {
                // 如果成功处理了跳跃相关意图，返回true要求消耗输入
                if (HandleJumpIntent(_data))
                {
                    return true;
                }
            }

            return false;
        }

        // 跳跃意图处理 仲裁是否跳跃 翻越或二段跳 
        // 优先级依次为 低翻越 高翻越 地面跳跃 空中二段跳 
        private bool HandleJumpIntent(PlayerRuntimeData data)
        {
            if (TryGetVaultIntent(data, out VaultObstacleInfo info, _config.Vaulting.LowVaultMinHeight, _config.Vaulting.LowVaultMaxHeight))
            {
                data.WantsToVault = true;
                data.WantsLowVault = true;
                data.CurrentVaultInfo = info;
                return true;
            }

            if (TryGetVaultIntent(data, out info, _config.Vaulting.HighVaultMinHeight, _config.Vaulting.HighVaultMaxHeight))
            {
                data.WantsToVault = true;
                data.WantsHighVault = true;
                data.CurrentVaultInfo = info;
                return true;
            }

            if (data.IsGrounded)
            {
                data.WantsToJump = true;
                return true;
            }

            if (!data.IsGrounded && !data.HasPerformedDoubleJumpInAir)
            {
                data.DoubleJumpDirection = DoubleJumpDirection.Up;
                data.WantsDoubleJump = true;
                return true;
            }

            return false;
        }

        public bool TryGetVaultIntent(PlayerRuntimeData data, out VaultObstacleInfo info, float minHeight, float maxHeight)
        {
            info = new VaultObstacleInfo { IsValid = false };
            if (!data.IsGrounded && !data.HasPerformedDoubleJumpInAir) return false;

            // 按下按键时 调用静默模式的检测 获取最新数据
            return DetectObstacle(out info, minHeight, maxHeight, true);
        }

        // 纯粹的数学和物理检测逻辑 无状态副作用 
        private bool DetectObstacle(out VaultObstacleInfo info, float minHeight, float maxHeight, bool isSilent)
        {
            info = new VaultObstacleInfo { IsValid = false };

            Transform root = _playerTransform;
            Vector3 rayStart = root.position + Vector3.up * _config.Vaulting.VaultForwardRayHeight;
            Vector3 forward = root.forward;

            if (Physics.Raycast(rayStart, forward, out RaycastHit wallHit, _config.Vaulting.VaultForwardRayLength, _obstacleMask))
            {
                if (Vector3.Dot(wallHit.normal, Vector3.up) > 0.1f) return false;

                Vector3 downRayStart = wallHit.point + Vector3.up * _config.Vaulting.VaultDownwardRayLength + forward * _config.Vaulting.VaultDownwardRayOffset;

                if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit ledgeHit, _config.Vaulting.VaultDownwardRayLength, _obstacleMask))
                {
                    if (Vector3.Dot(ledgeHit.normal, Vector3.up) < 0.9f) return false;

                    float height = ledgeHit.point.y - root.position.y;
                    if (height < minHeight || height > maxHeight) return false;

                    Vector3 vaultForwardDir = -wallHit.normal;
                    Vector3 landRayStart = ledgeHit.point + vaultForwardDir * _config.Vaulting.VaultLandDistance + Vector3.up * 0.5f;
                    Vector3 finalLandPoint = Vector3.zero;
                    bool foundGround = false;

                    if (Physics.Raycast(landRayStart, Vector3.down, out RaycastHit landHit, _config.Vaulting.VaultLandRayLength, _obstacleMask))
                    {
                        if (Vector3.Dot(landHit.normal, Vector3.up) >= 0.7f)
                        {
                            finalLandPoint = landHit.point;
                            foundGround = true;
                        }
                    }

                    if (_config.Vaulting.RequireGroundBehindWall && !foundGround) return false;

                    if (!foundGround)
                    {
                        finalLandPoint = landRayStart + Vector3.down * 0.5f;
                    }

                    info.IsValid = true;
                    info.WallPoint = wallHit.point;
                    info.WallNormal = wallHit.normal;
                    info.Height = height;
                    info.ExpectedLandPoint = finalLandPoint;

                    Vector3 ledgeEdge = new Vector3(wallHit.point.x, ledgeHit.point.y, wallHit.point.z);
                    info.LedgePoint = ledgeEdge;

                    // 利用纯数学矩阵解耦的手部IK推算
                    Vector3 wallNormalFlat = new Vector3(wallHit.normal.x, 0f, wallHit.normal.z);
                    if (wallNormalFlat.sqrMagnitude < 0.0001f) return false;

                    Vector3 rightDir = Vector3.Cross(Vector3.up, wallNormalFlat).normalized;
                    Vector3 characterRight = new Vector3(root.right.x, 0f, root.right.z).normalized;

                    if (characterRight.sqrMagnitude > 0.0001f)
                    {
                        if (Vector3.Dot(rightDir, characterRight) < 0f)
                            rightDir = -rightDir;
                    }

                    float halfSpread = _config.Vaulting.VaultHandSpread * 0.5f;
                    info.LeftHandPos = ledgeEdge - rightDir * halfSpread;
                    info.RightHandPos = ledgeEdge + rightDir * halfSpread;
                    info.HandRot = Quaternion.LookRotation(-wallNormalFlat.normalized, Vector3.up);

                    return true;
                }
            }
            return false;
        }
    }
}