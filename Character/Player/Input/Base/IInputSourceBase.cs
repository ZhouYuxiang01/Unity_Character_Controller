using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Input
{
    /// <summary>
    /// 输入源基类 - 所有输入源的抽象基类
    /// 提供统一的序列化接口 支持在 Unity 编辑器中拖拽赋值
    /// 所有具体输入源(如PlayerInputReader、AI输入源等)都应继承此类
    /// </summary>
    public abstract class IInputSourceBase : MonoBehaviour, IInputSource
    {
        /// <summary>
        /// 由具体实现类重写 负责获取原始输入数据
        /// </summary>
        /// <param name="rawData">用于存储原始输入的结构体引用</param>
        public abstract void FetchRawInput(ref RawInputData rawData);
    }
}
