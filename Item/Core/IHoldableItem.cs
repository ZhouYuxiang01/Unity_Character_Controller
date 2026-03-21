namespace BBBNexus
{
    /// <summary>
    /// 可持有物品接口 - 定义所有装备物品必须实现的生命周期与逻辑接口
    /// 武器、道具、任何能拿在手上的东西都必须实现这个接口
    /// 
    /// === IK 职责划分 ===
    /// 物品系统采用三层 IK 清理机制：
    /// 1. 物品自主清理（OnForceUnequip）- 第一层
    /// 2. EquipmentDriver 统一清理（UnequipCurrentItem）- 第二层（主防线）
    /// 3. IKController 运行时检测（SanitizeAimReference）- 第三层（最后防线）
    /// 
    /// 职责分工：
    /// - 物品在 OnEquipEnter 中写入：LeftHandGoal、WantsLeftHandIK、CurrentAimReference
    /// - 物品在 OnUpdateLogic 中可动态更新：CurrentAimReference、WantsLookAtIK
    /// - 物品在 OnForceUnequip 中应清理：自己写入的所有 IK 数据
    /// - EquipmentDriver 在 UnequipCurrentItem 中兜底清理：所有 IK 数据（防止对象池失活遗留）
    /// - IKController 每帧检测：CurrentAimReference 的有效性（防止追踪失活对象）
    /// </summary>
    public interface IHoldableItem
    {
        /// <summary>
        /// 灵魂注入：当模型实例生成后，EquipmentDriver 立刻调用此方法注入实例数据
        /// 这一刻物品的逻辑系统获得了黑板中的真实数据，包括堆叠数量、属性修改等
        /// </summary>
        void Initialize(ItemInstance instanceData);

        /// <summary>
        /// 装备入场：状态机将权限转交给物品时被触发
        /// 这是拔枪、拿出等装备的启动时刻，物品应在此执行初始化表现与动画
        /// 
        /// === IK 操作 ===
        /// 物品可在此方法中设置 IK 目标：
        /// - 设置 RuntimeData.LeftHandGoal（左手握点）
        /// - 设置 RuntimeData.WantsLeftHandIK = true/false
        /// - 设置 RuntimeData.CurrentAimReference（瞄准参考点，远程武器在进入瞄准时）
        /// </summary>
        void OnEquipEnter(PlayerController player);

        /// <summary>
        /// 逻辑更新：每帧都被调用，物品应在此查询输入执行攻击、使用等逻辑
        /// 这是物品的核心行为驱动点，如果不实现此方法物品将无法响应输入
        /// 
        /// === IK 操作 ===
        /// 物品可在此方法中动态更新 IK 状态：
        /// - 根据瞄准状态修改 CurrentAimReference（瞄准时设置，非瞄准时清空）
        /// - 根据动画阶段修改 WantsLeftHandIK、WantsLookAtIK 等标志
        /// </summary>
        void OnUpdateLogic();

        /// <summary>
        /// 强制卸载：状态机切换、角色死亡等事件时被强制调用
        /// 
        /// 物品必须立即停止所有自身协程、清理 IK 调度、音效等
        /// 不能依赖 InputReader 的正常流程，务必完全清理以避免残留 Bug
        /// 
        /// === IK 清理 ===
        /// 物品应在此方法中清理自己写入的 IK 数据：
        /// - 清理 LeftHandGoal、WantsLeftHandIK
        /// - 清理 CurrentAimReference、WantsLookAtIK（如果设置过）
        /// 
        /// 注意：即使此方法未能彻底清理，EquipmentDriver.UnequipCurrentItem() 仍会进行
        /// 统一清理，所以不必过分担心遗漏的情况。EquipmentDriver 是主防线。
        /// </summary>
        void OnForceUnequip();
    }
}