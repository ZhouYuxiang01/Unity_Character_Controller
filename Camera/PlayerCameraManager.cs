using UnityEngine;
using Cinemachine;
using Characters.Player; // 引用 PlayerController

namespace Core.CameraSystem
{
    public class PlayerCameraManager : MonoBehaviour
    {
        [Header("监听对象")]
        [SerializeField] private PlayerController _player;

        [Header("虚拟相机")]
        [SerializeField] private CinemachineVirtualCamera _freeLookCam; // 探索
        [SerializeField] private CinemachineVirtualCamera _aimCam; // 瞄准

        [Header("鼠标控制 (运行时)")]
        [Tooltip("进入运行模式时是否隐藏并锁定鼠标，退出时会自动恢复。发布时建议关闭。")]
        public bool HideCursorOnPlay = true;
        [Tooltip("锁定模式：Locked 会锁在窗口中心，Confined 限制在窗口内，None 不锁定")] 
        public CursorLockMode CursorLock = CursorLockMode.Locked;

        [Header("准星 (Screen HUD)")]
        [Tooltip("在屏幕中央显示的准星贴图，仅在运行时绘制。可为空以隐藏。")]
        public Texture2D CrosshairTexture;
        [Tooltip("是否显示准星（仅影响运行时显示）")]
        public bool ShowCrosshair = true;
        [Tooltip("准星在屏幕上的像素大小（正方形）")]
        public float CrosshairSize = 32f;

        private void Start()
        {
            // 运行时进入时根据配置隐藏并锁定鼠标
            if (HideCursorOnPlay)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLock;
            }
        }

        private void Update()
        {
            if (_player == null) return;

            // 优先级切换放在 Update：确保 CinemachineBrain 在 LateUpdate 选择机位前就已确定优先级。
            if (_player.RuntimeData.IsAiming)
            {
                _aimCam.Priority = 20;
                _freeLookCam.Priority = 10;
            }
            else
            {
                _aimCam.Priority = 10;
                _freeLookCam.Priority = 20;
            }
        }

        // 在 Game 窗口绘制简单的准星 HUD
        // 使用 OnGUI 简单快速且在打包后仍然可用。性能影响极小（仅绘制一个贴图）。
        private void OnGUI()
        {
            if (!ShowCrosshair) return;
            if (CrosshairTexture == null) return;
            if (!Application.isPlaying) return; // 仅在运行时绘制（编辑器场景视图也会触发 OnGUI）

            // 屏幕中心坐标
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            float size = Mathf.Max(1f, CrosshairSize);
            Rect rect = new Rect(x - size * 0.5f, y - size * 0.5f, size, size);

            // 保存颜色与矩阵（防止其他 OnGUI 影响）
            Color oldColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, CrosshairTexture);
            GUI.color = oldColor;
        }

        // 确保在脚本停用或应用退出时恢复鼠标状态，避免编辑器或系统丢失光标
        private void OnDisable()
        {
            if (HideCursorOnPlay)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void OnApplicationQuit()
        {
            if (HideCursorOnPlay)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
}
