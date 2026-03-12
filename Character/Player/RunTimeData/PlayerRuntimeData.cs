using Items.Core;
using UnityEngine;
using Characters.Player.Animation;

namespace Characters.Player.Data
{
    /// <summary>
    /// 玩家运行时数据容器 - 整个控制流程的信息交汇点
    /// 负责在意图管线 物理驱动 动画系统 IK系统之间传递数据
    /// 每一帧都会被多个系统读写 务必保证数据的一致性和及时性
    /// 这就像是航空管制室 所有信息都汇聚在这里 再分发给各个子系统执行
    /// </summary>
    public class PlayerRuntimeData
    {
        #region INPUT - 原始输入数据 - 从InputReader获取

        [Header("输入数据 (Input Data) - 直接来自玩家的输入")]

        [Tooltip("相机视角的旋转输入 X=水平转向 Y=竖直俯仰 范围通常-1~1 单位：度/帧")]
        public Vector2 LookInput;

        [Tooltip("移动方向的摇杆输入 X=前后 Y=左右 范围-1~1 这是动画混合树的输入源")]
        public Vector2 MoveInput;

        #endregion

        #region VIEW & ROTATION - 视角与朝向状态 - 由相机系统持续更新

        [Header("视角与朝向 (View & Orientation) - 由相机与旋转系统驱动")]

        [Tooltip("玩家当前视角的水平转向角 度 -180~180 对应相机的yaw")]
        public float ViewYaw;

        [Tooltip("玩家当前视角的竖直俯仰角 度 由PitchLimits限制 对应相机的pitch")]
        public float ViewPitch;

        [Tooltip("权威旋转的水平转向 权威旋转是相机告诉角色应该面向哪里 用于IK目标点计算")]
        public float AuthorityYaw;

        [Tooltip("权威旋转的竖直俯仰 一般在瞄准IK中使用")]
        public float AuthorityPitch;

        [Tooltip("权威旋转四元数 包含了当前相机要求角色的完整方向姿态")]
        public Quaternion AuthorityRotation;

        [Tooltip("角色当前实际朝向的水平角 由意图管线平滑计算 永远追不上ViewYaw但会逐渐靠近")]
        public float CurrentYaw;

        [Tooltip("角色旋转速度 用于平滑转向 值越大转得越快")]
        public float RotationVelocity;

        #endregion

        #region PHYSICS & MOVEMENT - 物理与移动状态 - 由CharacterController与重力系统驱动

        [Header("物理与移动 (Physics & Movement) - 物理引擎的实时反馈")]

        [Tooltip("角色是否接地 true=脚踩地面 false=在空中 这是跳跃 下落 翻越等动作的触发条件")]
        public bool IsGrounded;

        [Tooltip("角色是否正在闪避 用于防止闪避期间触发其他高优先级动作")]
        public bool IsDodgeing;

        [Tooltip("角色竖直方向的速度 正数向上 负数向下 单位m/s 由重力持续修改")]
        public float VerticalVelocity;

        [Tooltip("本帧是否刚刚着陆 true=触地的那一刻 下一帧就变false 用于触发着陆动画")]
        public bool JustLanded;

        [Tooltip("本帧是否刚刚离地 true=跳起的那一刻 下一帧就变false 用于清理接地状态")]
        public bool JustLeftGround;

        [Tooltip("角色是否处于瞄准状态 由瞄准拦截器控制 true时会切到瞄准动画混合树")]
        public bool IsAiming;

        [Tooltip("上一帧的下半身运动状态 Idle/Walk/Jog/Sprint 用于检测状态切换")]
        public LocomotionState LastLocomotionState = LocomotionState.Idle;

        [Tooltip("本帧的下半身运动状态 由意图管线计算 决定动画混合树的参数")]
        public LocomotionState CurrentLocomotionState = LocomotionState.Idle;

        [Space]
        [Tooltip("玩家期望移动方向的世界坐标向量 单位向量 由摇杆输入与相机朝向合成 动画混合树用这个计算相对朝向")]
        public Vector3 DesiredWorldMoveDir;

        [Tooltip("玩家期望移动方向相对于身体的夹角 度 -180~180 用于判断是前进还是后退")]
        public float DesiredLocalMoveAngle;

        [Header("实时速度 (Runtime Speed) - 动画与根运动驱动的速度")]

        [Tooltip("角色当前的水平移动速度 m/s 由CharacterController反馈或根运动驱动 用于物理模拟与IK调整")]
        public float CurrentSpeed;

        #endregion

        #region ITEM - 当前物品/装备意图 - 物品系统的唯一权威源

        [Header("装备物品 (Item - Runtime) - 背包与装备意图的桥梁")]

        [Tooltip("快捷栏装备意图：-1表示没有意图，0~4表示想要装备对应槽位的物品")]
        public int WantsToEquipHotbarIndex = -1;
        [Tooltip("当前装备的物品实例 包含定义与堆叠数量 为null表示空手 这是装备驱动唯一的数据源")]
        public ItemInstance CurrentItem;
        /// <summary>
        /// 当前用于执行"指向"动作的基准Transform
        /// 例如：持枪时这是枪口 手电筒时这是灯泡 空手时通常是头部
        /// IK系统会让这个Transform精确对准TargetAimPoint
        /// 这样保证了不同武器都能准确指向玩家的准星
        /// </summary>
        [Tooltip("当前武器的指向基准点 transform 通常是枪口或手电筒灯泡 为null时默认使用头部")]
        public Transform CurrentAimReference;

        #endregion

        #region INTENT - 单帧动作意图 - 由意图管线计算 每帧清理一次

        [Header("动作意图 (Action Intents) - 一帧生命周期的临时标志")]

        [Tooltip("瞄准的目标点 世界坐标 由相机射线计算得出 IK系统会让枪口/手电筒指向这里")]
        public Vector3 TargetAimPoint;

        [Tooltip("相机当前的观察方向 用于计算身体与相机的夹角 驱动动画混合树的上半身参数")]
        public Vector3 CameraLookDirection;

        [Tooltip("本帧是否想要跑步 false=走步 true=跑步 由意图管线根据体力与输入判断")]
        public bool WantToRun;

        [Tooltip("本帧是否想要闪避 拦截器会优先处理此意图 触发闪避状态")]
        public bool WantsToDodge;

        [Tooltip("本帧是否想要翻滚 与闪避冲突 通常通过不同的输入按钮触发")]
        public bool WantsToRoll;

        [Tooltip("本帧是否想要跳跃 检测到地面且体力充足时触发")]
        public bool WantsToJump;

        [Tooltip("本帧是否想要二段跳 仅在空中且未使用过二段跳时有效")]
        public bool WantsDoubleJump;

        [Tooltip("二段跳的方向 Up=竖直向上 Left/Right=向左右跳")]
        public DoubleJumpDirection DoubleJumpDirection = DoubleJumpDirection.Up;

        [Space]
        [Tooltip("本帧是否想要翻越 检测到合适的障碍物时设为true")]
        public bool WantsToVault;

        [Tooltip("本帧是否想要低翻越 障碍物高度在范围内时为true")]
        public bool WantsLowVault;

        [Tooltip("本帧是否想要高翻越 高个子障碍物时为true")]
        public bool WantsHighVault;

        [Tooltip("当前有效的翻越障碍物信息 包含IK目标与着陆点等")]
        public VaultObstacleInfo CurrentVaultInfo;

        [Tooltip("摇杆输入量化后的8方向结果 用于选择对应的启动动画")]
        public DesiredDirection QuantizedDirection;

        [Space]
        [Header("下落意图 (Fall Intent) - 空中坠落的触发标志")]

        [Tooltip("本帧是否进入下落状态 由MovementParameterProcessor根据空中时间计算 触发下落动画与表现")]
        public bool WantsToFall;

        [Space]
        [Header("开火意图 (Fire Intent) - 射击行为的触发标志")]

        [Tooltip("本帧是否想要开火 由瞄准与开火意图处理器根据左鼠标/开火按钮按下时设置 一帧周期")]
        public bool WantsToFire;

        [Space]
        [Header("表情意图 (Expression Intent) - 脸部表情的单帧触发")]

        [Tooltip("本帧是否播放表情1 通常用于受伤反应")]
        public bool WantsExpression1;

        [Tooltip("本帧是否播放表情2")]
        public bool WantsExpression2;

        [Tooltip("本帧是否播放表情3")]
        public bool WantsExpression3;

        [Tooltip("本帧是否播放表情4")]
        public bool WantsExpression4;

        #endregion

        #region WARPING & VAULTING - 空间扭曲与翻越逻辑 - 高级动画驱动

        [Header("动画变形 (Motion Warping) - 动态修改根运动轨迹")]

        [Tooltip("角色是否处于根运动变形状态 true时动画的位移会被MotionDriver实时重新计算")]
        public bool IsWarping;

        [Tooltip("角色是否处于翻越状态 翻越是特殊的变形动作 持续到着陆")]
        public bool IsVaulting;

        [Tooltip("当前激活的根运动变形数据 包含烘焙的速度曲线与IK权重曲线")]
        public WarpedMotionData ActiveWarpData;

        [Tooltip("根运动变形的归一化时间 0~1 由动画播放进度驱动 IK系统用这个插值计算目标点")]
        public float NormalizedWarpTime;

        [Header("变形目标 (Warp IK Targets) - 动画变形期间的IK目标")]

        [Tooltip("左手IK目标点 世界坐标 在翻越时指向墙面的左手握点")]
        public Vector3 WarpIKTarget_LeftHand;

        [Tooltip("右手IK目标点 世界坐标")]
        public Vector3 WarpIKTarget_RightHand;

        [Tooltip("手部IK的朝向四元数 确保两只手的握姿一致")]
        public Quaternion WarpIKRotation_Hand;

        #endregion

        #region ANIMATION PARAMETERS - 动画驱动参数 - 动画混合树的输入

        [Header("动画参数 (Animator Parameters) - 动画混合树的实时控制")]

        [Tooltip("前后方向的动画混合参数 -1=向后 0=原地 1=向前 由意图管线平滑计算")]
        public float CurrentAnimBlendX;

        [Tooltip("左右方向的动画混合参数 -1=向左 0=正前 1=向右 与X组成2D混合空间")]
        public float CurrentAnimBlendY;

        [Tooltip("跑步循环的周期时间 用于检测脚步周期 与脚相判定搭配使用")]
        public float CurrentRunCycleTime;

        [Tooltip("预期脚相 LeftFootDown或RightFootDown 用于选择停止动画与启动动画")]
        public FootPhase ExpectedFootPhase;

        [Header("着陆与过渡 (Landing & Transition) - 特殊动作的参数")]

        [Tooltip("下落高度等级 0~4级 用于选择对应的着陆缓冲动画 级数越高下落伤害越大")]
        public int FallHeightLevel;
        #endregion

        #region
        /// <summary>
        /// 新的播放选项覆写 包含淡入时间 播放速率 起始位置 IK同步等参数
        /// 如果设置此项 下次播放动画时会用这里的参数而不是默认配置
        /// 常在状态转移时用于快速调整动画表现
        /// </summary>
        [Tooltip("下半身状态播放选项覆写 为null时使用默认配置 设置后会自动清空")]
        public AnimPlayOptions? NextStatePlayOptions = null;

        [Tooltip("上半身状态播放选项覆写 独立于下半身")]
        public AnimPlayOptions? NextUpperBodyStatePlayOptions = null;
        #endregion

        #region
        [Header("IK驱动 (IK Goals) - 肢体对齐的目标")]

        [Tooltip("是否启用左手IK true时左手会向LeftHandGoal靠近")]
        public bool WantsLeftHandIK;

        [Tooltip("是否启用右手IK true时右手会向RightHandGoal靠近")]
        public bool WantsRightHandIK;

        [Tooltip("是否启用头部指向IK true时头部会转向LookAtPosition")]
        public bool WantsLookAtIK;

        [Tooltip("左手IK的目标transform 通常是武器的握点或某个交互物体")]
        public Transform LeftHandGoal;

        [Tooltip("右手IK的目标transform")]
        public Transform RightHandGoal;

        [Tooltip("头部指向的目标点 世界坐标 IK系统会让头部看向这里")]
        public Vector3 LookAtPosition;

        #endregion

        #region ATTRIBUTES & TRACKING - 数值与周期追踪 - 游戏状态的记录

        [Header("数值与追踪 (Attributes & Tracking) - 玩家角色的各项状态")]

        [Tooltip("角色当前体力值 0~MaxStamina 冲刺 闪避 翻滚等动作会消耗体力")]
        public float CurrentStamina;

        [Tooltip("体力是否枯竭 true=没气了 无法执行高耗能动作 直到回复到阈值以上")]
        public bool IsStaminaDepleted;

        [Header("周期追踪 (Cycle Tracking) - 单次动作的状态记录")]

        [Tooltip("本次空中活动中是否已使用过二段跳 着陆后自动重置 防止无限二段跳")]
        public bool HasPerformedDoubleJumpInAir;

        [Header("全局引用 (Global References) - 系统级别的对象引用")]

        [Tooltip("相机transform 用于计算相对方向与射线检测等")]
        public Transform CameraTransform;

        #endregion

        #region API 方法

        public PlayerRuntimeData()
        {
            CurrentLocomotionState = LocomotionState.Idle;
        }

        /// <summary>
        /// 每帧清理一次性意图标志位
        /// 这些标志只应持续一帧 过期后必须清空 否则动作会重复触发
        /// </summary>
        public void ResetIntetnt()
        {
            WantsToVault = false;
            WantToRun = false;
            WantsToJump = false;
            WantsDoubleJump = false;
            WantsToDodge = false;
            WantsToRoll = false;
            WantsLowVault = false;
            WantsHighVault = false;
            WantsToFire = false; // 清理开火意图

            // Expression intents (one-frame)
            WantsExpression1 = false;
            WantsExpression2 = false;
            WantsExpression3 = false;
            WantsExpression4 = false;
            // 注意：WantsToFall 由 MovementParameterProcessor 每帧计算，不在这里清理
        }

        #endregion
    }
}
