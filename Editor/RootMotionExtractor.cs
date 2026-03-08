using UnityEngine;
using UnityEditor;
using Characters.Player.Data;
using System.Reflection;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System;

// RootMotionExtractorWindow：这是一个 Unity 编辑器，用于把动画中的根运动信息烘焙到 MotionClipData 中。
public class RootMotionExtractorWindow : EditorWindow
{
    // 选择要扫描的 PlayerSO
    private PlayerSO _targetSO;
    // 支持任意序列化文件（Asset），作为扫描的根对象
    private UnityEngine.Object _targetAsset; // 支持任意序列化文件
    // 用来在编辑器中实例化并采样动画的角色 Prefab
    private GameObject _characterPrefab;

    // 采样率模式：从动画剪辑读取或固定 60/120 FPS
    public enum SampleRateMode { FromClip, Fps60, Fps120 }
    private SampleRateMode _sampleRateMode = SampleRateMode.FromClip;
    // 用于检测脚的 Transform（哪两个骨骼被视为左右脚）
    private HumanBodyBones _leftFootBone = HumanBodyBones.LeftFoot;
    private HumanBodyBones _rightFootBone = HumanBodyBones.RightFoot;

    // 批量应用时使用的 MotionType
    private MotionType _batchMotionType = MotionType.CurveDriven;
    // 批量设置时的目标时长（用于 PlaybackSpeed 计算）
    private float _batchTargetDuration = 0f;

    // 方向过滤角度阈值（度），用于判断是否把移动方向保存为本地方向
    private float _localDirFilterAngleDeg = 12f;
    // 最小位移阈值，如果位移太小则认为没有方向
    private float _localDirMinDistance = 0.02f;

    // --- Dashboard & Logging ---
    // 是否输出详细日志
    private bool _verboseLogging = true;
    // 每多少帧记录一次日志
    private int _logEveryNFrames = 15;
    // 仪表盘中最多存放多少事件
    private int _maxDashboardEvents = 30;

    // 标记是否正在烘焙
    private bool _isBaking;
    // 当前烘焙索引（用于进度显示）
    private int _bakeIndex;
    // 总共需要烘焙多少个 MotionClipData
    private int _bakeTotal;
    // 进度（0 到 1）
    private float _bakeProgress01;
    // 当前正在处理的剪辑名字
    private string _currentClipName;
    // 当前阶段（例如 Init、Bake、Curves 等）
    private string _currentStage;
    // 当前阶段的详细信息
    private string _currentDetail;
    // 当前剪辑结束时哪个脚着地（Left/Right）
    private FootPhase _currentEndPhase;
    // 旋转完成的时间（秒）
    private float _currentRotationFinishedTime;
    // 当前速度曲线的关键帧数
    private int _currentSpeedKeys;
    // 当前旋转曲线的关键帧数
    private int _currentRotKeys;
    // 当前剪辑烘焙耗时（毫秒）
    private long _currentClipMs;

    // 仪表盘事件列表（用于在窗口中显示日志）
    private readonly List<DashboardEvent> _events = new List<DashboardEvent>();
    // 滚动条位置
    private Vector2 _eventScroll;
    // 总计耗时计时器
    private readonly Stopwatch _swAll = new Stopwatch();
    // 单剪辑耗时计时器
    private readonly Stopwatch _swClip = new Stopwatch();

    // 仪表盘事件结构
    private struct DashboardEvent
    {
        public double Time;
        public string Msg;
        public Color Color;
    }

    // 简单的姿态信息，记录左右脚在根变换下的位置（局部坐标）
    private struct PoseInfo { public Vector3 LeftLocal; public Vector3 RightLocal; }

    // 在 Unity 菜单中注册窗口入口
    [MenuItem("Tools/BBB-Nexus/Root Motion Extractor (Ultimate)")]
    public static void ShowWindow()
    {
        // 打开一个新的编辑器窗口，标题为 "RM Baker"
        GetWindow<RootMotionExtractorWindow>("RM Baker");
    }

    // OnGUI：绘制编辑器窗口的 UI
    private void OnGUI()
    {
        GUILayout.Label("Root Motion 烘焙器 (v2.0 - 四元数内核)", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("核心配置", EditorStyles.boldLabel);
            // 选择 PlayerSO（项目中的配置文件）
            _targetSO = (PlayerSO)EditorGUILayout.ObjectField("配置文件 (PlayerSO)", _targetSO, typeof(PlayerSO), false);
            // 选择任意序列化 Asset 作为扫描根（ ScriptableObject ）
            _targetAsset = EditorGUILayout.ObjectField(new GUIContent("Target Asset (任意序列化文件)", "可选择任意序列化的 Unity 对象（ScriptableObject、Asset 等）作为扫描根"), _targetAsset, typeof(UnityEngine.Object), false);
            // 拖入一个用于模拟采样动画的角色预制体
            _characterPrefab = (GameObject)EditorGUILayout.ObjectField("模拟模型 (Prefab)", _characterPrefab, typeof(GameObject), false);
            // 采样率选择器（从剪辑读取或固定帧率）
            _sampleRateMode = (SampleRateMode)EditorGUILayout.EnumPopup("烘焙采样率", _sampleRateMode);
            // 说明文字提示
            EditorGUILayout.HelpBox("更高的采样率可以捕捉更精细的运动细节，但会增加烘焙时间和数据量。", MessageType.Info);
        }

        // --- Logging & Dashboard ---
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("日志与仪表盘", EditorStyles.boldLabel);
            // 是否输出更详细的日志
            _verboseLogging = EditorGUILayout.ToggleLeft("详细日志（控制台 + 仪表盘）", _verboseLogging);
            // 按钮：清空仪表盘事件
            if (GUILayout.Button("清空仪表盘事件"))
            {
                _events.Clear();
                Repaint(); // 刷新窗口显示
            }
        }

        // --- Advanced Settings ---
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("高级设置", EditorStyles.boldLabel);
            // 左右脚骨骼选择（用于判断脚相）
            _leftFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("左脚骨骼", _leftFootBone);
            _rightFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("右脚骨骼", _rightFootBone);
            // 本地方向过滤角度阈值（限制 0-90）
            _localDirFilterAngleDeg = Mathf.Clamp(EditorGUILayout.FloatField("方向过滤角度阈值(°)", _localDirFilterAngleDeg), 0f, 90f);
            // 本地方向最小位移阈值（不能为负）
            _localDirMinDistance = Mathf.Max(0f, EditorGUILayout.FloatField("方向最小位移阈值(m)", _localDirMinDistance));
        }

        EditorGUILayout.Space(10);

        // --- Bake Button ---
        // 如果正在烘焙，则禁用按钮
        using (new EditorGUI.DisabledScope(_isBaking))
        {
            if (GUILayout.Button("烘焙全部 MotionClipData", GUILayout.Height(40)))
            {
                // 检查必要的输入：Target Asset/PlayerSO 和 Character Prefab
                if ((_targetAsset == null && _targetSO == null) || _characterPrefab == null)
                {
                    EditorUtility.DisplayDialog("错误", "缺少目标 Asset/PlayerSO 或 Character Prefab！", "OK");
                    return;
                }
                // 开始批量烘焙
                BakeAll();
            }
        }

        // --- Batch Settings ---
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Label("批量设置（精简）Batch Settings", EditorStyles.boldLabel);
        // 批量设置中的驱动模式
        _batchMotionType = (MotionType)EditorGUILayout.EnumPopup("驱动模式 (Type)", _batchMotionType);
        // 批量设置中的目标时长
        _batchTargetDuration = EditorGUILayout.FloatField("目标时长 (Target Duration)", _batchTargetDuration);
        if (GUILayout.Button("应用到所有 MotionClipData", GUILayout.Height(30)))
        {
            if (_targetSO == null)
            {
                EditorUtility.DisplayDialog("错误", "请先拖入 PlayerSO！", "OK");
                return;
            }
            ApplyBatchSettings();
        }

        // 绘制仪表盘区域
        DrawDashboard();
    }

    // --- Main Baking Logic (Entry Point) ---
    // BakeAll：遍历目标对象下所有 MotionClipData，并把烘焙后的新实例替换回去。
    private void BakeAll()
    {
        // root 为优先使用的目标：先用选中的任意 Asset，否则用 PlayerSO
        var root = _targetAsset != null ? _targetAsset : (UnityEngine.Object)_targetSO;
        if (root == null)
        {
            Debug.LogError("Root asset is null");
            return;
        }

        // 支持撤销：记录对 root 的修改
        Undo.RecordObject(root, "Bake All Motion Clip Data");

        // 初始化状态
        _isBaking = true;
        _bakeIndex = 0;
        _bakeProgress01 = 0f;
        _currentClipName = string.Empty;
        _currentStage = "Init";
        _currentDetail = string.Empty;
        _swAll.Restart();

        AddEvent($"开始烘焙：{root.name}", new Color(0.4f, 1f, 0.6f));
        LogVerbose($"=== BakeAll START => {root.name} ===", "#44ff88");

        // 在编辑器中实例化一个角色，用来 SampleAnimation（不要保存到场景）
        GameObject agent = Instantiate(_characterPrefab);
        agent.hideFlags = HideFlags.HideAndDontSave;
        Animator animator = agent.GetComponent<Animator>();

        // 检查 Animator 和 Avatar 是否有效且为 Humanoid，否则无法使用 GetBoneTransform
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            UnityEngine.Debug.LogError("模型必须带有 Animator 且为 Humanoid Avatar！");
            DestroyImmediate(agent);
            _isBaking = false;
            return;
        }

        // 确保动画的根运动会被应用，并且永远更新（编辑器中强制动画采样）
        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 收集我们会修改的所有拥有者对象，以便最后强制标记为已修改（SetDirty）
        var touchedOwners = new HashSet<UnityEngine.Object>();

        try
        {
            // 找到所有 MotionClipData 同时带上字段信息和拥有者对象（参考 WarpedMotionExtractor 的做法）
            var allClips = new List<(MotionClipData data, FieldInfo field, object owner)>();
            ScanMotionClipDataWithFieldInfo(root, (data, field, owner) => {
                // 只收集那些有实际 AnimationClip 的数据
                if (data != null && data.Clip != null && data.Clip.Clip != null)
                    allClips.Add((data, field, owner));
            });

            _bakeTotal = allClips.Count;
            int successCount = 0;

            // 遍历并烘焙每一个 MotionClipData
            for (int current = 0; current < allClips.Count; current++)
            {
                var (originalData, field, owner) = allClips[current];
                _bakeIndex = current;
                _currentClipName = originalData.Clip.Clip.name; // 当前剪辑名
                _bakeProgress01 = (float)current / Mathf.Max(1, _bakeTotal);
                _currentStage = "Bake";
                _currentDetail = "Preparing...";

                LogVerbose($"--> Clip START: {_currentClipName}", "#ffd54a");
                AddEvent($"开始：{_currentClipName}", new Color(1f, 0.92f, 0.35f));

                // 在编辑器顶部显示进度条
                EditorUtility.DisplayProgressBar("Baking Root Motion", $"Processing {originalData.Clip.Clip.name}...", _bakeProgress01);

                _swClip.Restart(); // 单个剪辑计时开始

                // 创建一个新的 MotionClipData 实例，把读取到的设置复制过去，然后烘焙曲线到这个新实例上
                MotionClipData bakedData = new MotionClipData();
                bakedData.Clip = originalData.Clip;
                bakedData.Type = originalData.Type;
                bakedData.TargetDuration = originalData.TargetDuration;
                bakedData.EndTime = originalData.EndTime;
                bakedData.AllowBakeTargetLocalDirection = originalData.AllowBakeTargetLocalDirection;

                // 主要工作：对单个剪辑进行采样并生成 SpeedCurve、RotationCurve 等
                BakeSingleClip(animator, bakedData);

                _swClip.Stop(); // 单个剪辑计时结束

                // 使用反射将字段的值替换为新的 MotionClipData 实例，从而让 ScriptableObject 中引用更新
                if (field != null && owner != null)
                {
                    field.SetValue(owner, bakedData);
                    successCount++;

                    // 如果 owner 是 UnityEngine.Object（例如 ScriptableObject），记录下来用于 SetDirty
                    if (owner is UnityEngine.Object uo)
                        touchedOwners.Add(uo);
                }

                _currentClipMs = _swClip.ElapsedMilliseconds; // 记录耗时
                LogVerbose($"<-- Clip DONE: {_currentClipName} ({_currentClipMs}ms)", "#66ff88");
                AddEvent($"完成：{_currentClipName}  ({_currentClipMs}ms)", new Color(0.5f, 1f, 0.65f));

                Repaint(); // 刷新窗口显示
            }

            // --- 保存修改到磁盘（尽可能只对相关文件做重序列化） ---
            touchedOwners.Add(root); // 确保 root 本身也会被保存
            foreach (var obj in touchedOwners)
            {
                if (obj != null)
                    EditorUtility.SetDirty(obj); // 标记已改变
            }

            AssetDatabase.SaveAssets();

            // 只重新序列化与我们接触到的资源，避免对整个项目做慢速操作
            var touchedAssetPaths = new HashSet<string>();
            foreach (var obj in touchedOwners)
            {
                if (obj == null) continue;
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path))
                    touchedAssetPaths.Add(path);
            }

            if (touchedAssetPaths.Count > 0)
                AssetDatabase.ForceReserializeAssets(new List<string>(touchedAssetPaths));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _swAll.Stop(); // 总体耗时结束

            AddEvent($"全部完成：{_swAll.ElapsedMilliseconds}ms (成功更新 {successCount} 个)", new Color(0.35f, 1f, 0.9f));
            LogVerbose($"=== BakeAll DONE => {_swAll.ElapsedMilliseconds}ms (Success: {successCount}/{_bakeTotal}) ===", "#44ffee");
        }
        finally
        {
            // 清理：移除进度条、销毁临时实例并重置状态
            EditorUtility.ClearProgressBar();
            DestroyImmediate(agent);
            _isBaking = false;
            _currentStage = "Idle";
            _bakeProgress01 = 1f;
            Repaint();
        }
    }

    /// <summary>
    /// 递归扫描 MotionClipData 并同时收集 FieldInfo 和 owner 对象，用于强制替换。
    /// Recursively scan MotionClipData and collect FieldInfo and owner for forced replacement (WarpedMotionExtractor pattern).
    /// </summary>
    private void ScanMotionClipDataWithFieldInfo(object target, Action<MotionClipData, FieldInfo, object> onFound)
    {
        // 如果目标为空则直接返回
        if (target == null) return;
        var type = target.GetType();
        // 只处理 UnityEngine.Object（ScriptableObject/MonoBehaviour 等）或引用类型
        if (!typeof(UnityEngine.Object).IsAssignableFrom(type) && !type.IsClass) return;

        // 获取所有实例字段（包括私有和公共字段）
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var value = field.GetValue(target); // 读取字段的值
            if (value == null) continue;

            // 如果字段本身就是 MotionClipData，或者实际值是 MotionClipData，则调用回调
            if (field.FieldType == typeof(MotionClipData) || value is MotionClipData mcd)
            {
                onFound((MotionClipData)value, field, target);
            }
            else if (value is ScriptableObject so)
            {
                // 如果字段是 ScriptableObject，则递归扫描它的字段
                ScanMotionClipDataWithFieldInfo(so, onFound);
            }
            else if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                // 如果字段是集合（数组、List 等），遍历集合中的每个元素
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    if (item is MotionClipData itemMcd)
                    {
                        // 集合内的 MotionClipData 项
                        onFound(itemMcd, field, target);
                    }
                    else if (item is ScriptableObject itemSo)
                    {
                        // 集合内的 ScriptableObject，递归扫描
                        ScanMotionClipDataWithFieldInfo(itemSo, onFound);
                    }
                }
            }
        }
    }

    // --- [UPGRADED] Core Clip Baking Method ---
    // BakeSingleClip：对单个 MotionClipData 的 AnimationClip 做采样，生成速度曲线与旋转曲线等信息。
    private void BakeSingleClip(Animator animator, MotionClipData data)
    {
        // 取得真实的 AnimationClip
        AnimationClip clip = data.Clip.Clip;

        // 1. Determine Sample Rate based on UI selection
        float frameRate;
        switch (_sampleRateMode)
        {
            case SampleRateMode.Fps60:
                frameRate = 60f; // 固定 60 FPS
                break;
            case SampleRateMode.Fps120:
                frameRate = 120f; // 固定 120 FPS
                break;
            case SampleRateMode.FromClip:
            default:
                // 如果剪辑有设置帧率就用它，否则默认 30
                frameRate = clip.frameRate > 0 ? clip.frameRate : 30;
                break;
        }

        // 采样间隔 = 1 / 帧率，确保采样覆盖整个剪辑
        float interval = 1f / frameRate;
        float totalTime = Mathf.Max(clip.length, 0.001f); // 防止长度为 0
        int frameCount = Mathf.CeilToInt(totalTime * frameRate); // 采样帧数

        // 更新 UI 显示：当前阶段为旋转扫描
        _currentStage = "RotateScan";
        _currentDetail = $"frames={frameCount}, fps={frameRate}";
        Repaint();

        // 2. First Pass: Collect rotation curve for RotationFinishedTime using Quaternion math
        // 使用临时曲线来记录累计的 Y 轴旋转角度（绕竖直轴的角度）
        AnimationCurve tempRotCurve = new AnimationCurve();
        // 把角色放到原点，重置旋转，确保每次采样位置一致
        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Quaternion lastRot = Quaternion.identity;
        float accRotY = 0f; // 累计的偏航角度（度）

        for (int i = 0; i <= frameCount; i++)
        {
            // 计算采样时间，最后一帧确保不越界
            float time = Mathf.Min(i * interval, totalTime);
            // 采样动画：这会把 animator.gameObject 的变换设置为动画在该时间点的状态
            clip.SampleAnimation(animator.gameObject, time);
            Quaternion currentRot = animator.transform.rotation;

            if (i > 0)
            {
                // 用四元数计算两个旋转之间的差，然后求出绕 Y 轴的偏航变化量
                Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRot);
                Vector3 rotatedForward = deltaRot * Vector3.forward;
                rotatedForward.y = 0; // 只关心水平面上的方向
                float deltaYaw = Vector3.SignedAngle(Vector3.forward, rotatedForward.normalized, Vector3.up);

                accRotY += deltaYaw; // 累积偏航角
            }

            // 把累计角度存入临时曲线（时间 -> 累计偏航）
            tempRotCurve.AddKey(time, accRotY);
            lastRot = currentRot;
        }

        // 根据临时旋转曲线推断旋转结束的时间点（例如角色转身完成的时间）
        data.RotationFinishedTime = CalculateRotationFinishedTime(tempRotCurve, totalTime);
        _currentRotationFinishedTime = data.RotationFinishedTime;

        // 3. Sample last pose for foot phase（采样最后一帧，判断哪个脚在下）
        _currentStage = "EndPhase";
        Repaint();
        PoseInfo endPose = SampleClipPose(animator, clip, totalTime);
        // 比较左右脚高度，来判断结束时哪个脚在地面（简单启发式方法）
        data.EndPhase = (endPose.LeftLocal.y < endPose.RightLocal.y) ? FootPhase.LeftFootDown : FootPhase.RightFootDown;
        _currentEndPhase = data.EndPhase;

        // 4. Calculate playback speed（如果有目标时长则按比例缩放）
        _currentStage = "Speed";
        Repaint();
        data.PlaybackSpeed = (data.TargetDuration > 0.01f) ? (totalTime / data.TargetDuration) : 1f;

        // 5. Second Pass: Bake final speed and rotation curves（根据 PlaybackSpeed 生成最终曲线）
        _currentStage = "Curves";
        Repaint();
        // 新建曲线用于保存速度和旋转信息
        data.SpeedCurve = new AnimationCurve();
        data.RotationCurve = new AnimationCurve();
        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // 重置位置

        Vector3 lastPos = Vector3.zero;
        lastRot = Quaternion.identity; // Reset for second pass
        accRotY = 0f;

        Vector3 startPos = Vector3.zero; // 记录起始位置用于推断目标方向
        Quaternion startRot = Quaternion.identity; // 记录起始根朝向

        for (int i = 0; i <= frameCount; i++)
        {
            float originalTime = Mathf.Min(i * interval, totalTime);
            // 这个 scaledTime 是把时间按播放速度缩放到目标时长下的时间轴
            float scaledTime = originalTime / data.PlaybackSpeed;

            // 采样动画到原始时间点（注意：我们使用 originalTime 而不是 scaledTime 来采样动画，因为动画本身的采样在原始时间轴上）
            clip.SampleAnimation(animator.gameObject, originalTime);

            Vector3 currentPos = animator.transform.position;
            Quaternion currentRot = animator.transform.rotation;

            if (i == 0)
            {
                // 首帧初始化
                startPos = currentPos;
                startRot = currentRot;
                lastPos = currentPos;
                lastRot = currentRot;

                // 给曲线加起点（速度为 0，旋转为 0）
                data.SpeedCurve.AddKey(0, 0);
                data.RotationCurve.AddKey(0, 0);
                continue;
            }

            // --- Speed Calculation ---
            // 只在 XZ 平面计算位移距离，忽略 Y
            float dist = Vector3.Distance(new Vector3(currentPos.x, 0, currentPos.z), new Vector3(lastPos.x, 0, lastPos.z));
            float rawSpeed = dist / interval; // 原始瞬时速度（单位：单位/秒）
            // 把速度按 PlaybackSpeed 放缩（因为我们可能要把动画压缩到目标时长）
            data.SpeedCurve.AddKey(scaledTime, rawSpeed * data.PlaybackSpeed);

            // --- [UPGRADED] Rotation Calculation ---
            // 使用四元数求出两个采样之间的偏航变化，然后累积
            Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRot);
            Vector3 rotatedForward = deltaRot * Vector3.forward;
            rotatedForward.y = 0;
            float deltaYaw = Vector3.SignedAngle(Vector3.forward, rotatedForward.normalized, Vector3.up);
            accRotY += deltaYaw;
            // 把累计偏航存入旋转曲线（时间轴使用 scaledTime）
            data.RotationCurve.AddKey(scaledTime, accRotY);

            // 更新上一次的位移与旋转，用于下一帧计算
            lastPos = currentPos;
            lastRot = currentRot;
        }

        // 6. Infer TargetLocalDirection（如果允许则计算角色移动的本地方向）
        if (data.AllowBakeTargetLocalDirection)
        {
            Vector3 endPos = animator.transform.position; // 最后一帧的位置
            Vector3 startForwardVec = startRot * Vector3.forward; // 起始朝向向量
            startForwardVec.y = 0;
            // 计算起始根朝向的偏航角
            float startRootYaw = Vector3.SignedAngle(Vector3.forward, startForwardVec.normalized, Vector3.up);
            // 根据起始位置与结束位置推断本地移动方向
            BakeTargetLocalDirection(data, startPos, endPos, startRootYaw);
        }
        else
        {
            // 不允许时清空
            data.TargetLocalDirection = Vector3.zero;
        }

        // 记录曲线关键帧数量，供 UI 显示
        _currentSpeedKeys = data.SpeedCurve.length;
        _currentRotKeys = data.RotationCurve.length;

        // 7. Smooth curves (optional but recommended) - 平滑曲线的步骤（默认注释掉）
        _currentStage = "Smooth";
        Repaint();
        // SmoothCurve(data.SpeedCurve, 3);
        // SmoothCurve(data.RotationCurve, 5);

        // 输出完成日志
        LogVerbose($"{clip.name} DONE => EndPhase={data.EndPhase}, RotFinish={data.RotationFinishedTime:0.00}s, PlaybackSpeed={data.PlaybackSpeed:0.000}", "#66ff88");
    }

    // --- Helper & Utility Functions (Unchanged) ---
    // 绘制仪表盘（右侧显示进度、事件等）
    private void DrawDashboard()
    {
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("仪表盘 Dashboard", EditorStyles.boldLabel);
            var r = EditorGUILayout.GetControlRect(false, 18);
            // 进度条显示
            EditorGUI.ProgressBar(r, _bakeProgress01, _isBaking ? $"{_bakeIndex + 1}/{Mathf.Max(1, _bakeTotal)}  {_currentClipName}" : "Idle");
            using (new EditorGUI.DisabledScope(true))
            {
                // 只读显示当前阶段和细节信息
                EditorGUILayout.TextField("阶段 Stage", _currentStage ?? string.Empty);
                EditorGUILayout.TextField("细节 Detail", _currentDetail ?? string.Empty);
            }
            // 显示结束脚是左还是右，并用颜色区分
            var phaseLabel = _currentEndPhase == FootPhase.LeftFootDown ? "L" : "R";
            var phaseColor = _currentEndPhase == FootPhase.LeftFootDown ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.35f, 0.6f, 1f);
            var phaseRect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(phaseRect, new Color(0, 0, 0, 0.15f));
            var leftRect = phaseRect;
            leftRect.width = 60;
            EditorGUI.DrawRect(leftRect, phaseColor);
            GUI.Label(leftRect, $"End {phaseLabel}", EditorStyles.whiteLabel);
            var rightRect = phaseRect;
            rightRect.x += 64;
            rightRect.width -= 64;
            GUI.Label(rightRect, $"RotFinish={_currentRotationFinishedTime:F2}s | SpeedKeys={_currentSpeedKeys} | RotKeys={_currentRotKeys} | Clip={_currentClipMs}ms", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
            GUILayout.Label("事件流 Event Log", EditorStyles.boldLabel);
            _eventScroll = EditorGUILayout.BeginScrollView(_eventScroll, GUILayout.MinHeight(140));
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                var style = new GUIStyle(EditorStyles.label) { richText = true, normal = { textColor = e.Color } };
                GUILayout.Label($"[{e.Time:0.00}s] {e.Msg}", style);
            }
            EditorGUILayout.EndScrollView();
        }
    }
    // 添加一个仪表盘事件并限制最大事件数量
    private void AddEvent(string msg, Color color)
    {
        _events.Insert(0, new DashboardEvent { Time = EditorApplication.timeSinceStartup, Msg = msg, Color = color });
        if (_events.Count > _maxDashboardEvents)
            _events.RemoveRange(_maxDashboardEvents, _events.Count - _maxDashboardEvents);
        Repaint();
    }
    // 根据设置决定是否输出控制台日志（带颜色标签）
    private void LogVerbose(string msg, string colorTag)
    {
        if (!_verboseLogging) return;
        UnityEngine.Debug.Log($"<color={colorTag}>[RM Baker]</color> {msg}");
    }
    // 一个更简单的递归扫描版本，只返回 MotionClipData（用于批量设置）
    private void ScanMotionClipDataRecursive(object target, Action<MotionClipData> onFound)
    {
        if (target == null) return;
        var type = target.GetType();
        if (!typeof(UnityEngine.Object).IsAssignableFrom(type) && !type.IsClass) return;
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var value = field.GetValue(target);
            if (value == null) continue;
            if (field.FieldType == typeof(MotionClipData) || value is MotionClipData mcd) onFound((MotionClipData)value);
            else if (value is ScriptableObject so) ScanMotionClipDataRecursive(so, onFound);
            else if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                foreach (var item in enumerable)
                {
                    if (item is MotionClipData itemMcd) onFound(itemMcd);
                    else if (item is ScriptableObject itemSo) ScanMotionClipDataRecursive(itemSo, onFound);
                }
            }
        }
    }
    // 把批量设置应用到所有扫描到的 MotionClipData
    private void ApplyBatchSettings()
    {
        var root = _targetAsset != null ? _targetAsset : (UnityEngine.Object)_targetSO;
        if (root == null)
        {
            EditorUtility.DisplayDialog("错误", "请先拖入 PlayerSO 或 目标 Asset！", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("确认操作", $"你确定要将设置应用到 '{root.name}' 中的所有 MotionClipData 吗？", "确定 OK", "取消 Cancel")) return;
        try
        {
            int count = 0;
            ScanMotionClipDataRecursive(root, data => {
                if (data != null) { data.Type = _batchMotionType; data.TargetDuration = _batchTargetDuration; count++; }
            });
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            AddEvent($"批量设置完成：{count} 个", new Color(0.3f, 0.7f, 1f));
        }
        catch (System.Exception ex)
        {
            AddEvent($"批量设置失败：{ex.Message}", new Color(1f, 0.35f, 0.35f));
            UnityEngine.Debug.LogError($"批量设置失败: {ex.Message}");
        }
    }
    // 根据起始与结束位置以及起始朝向推断本地移动方向
    private void BakeTargetLocalDirection(MotionClipData data, Vector3 startPos, Vector3 endPos, float startRootYaw)
    {
        Vector3 delta = endPos - startPos;
        delta.y = 0f; // 只考虑水平面上的移动
        if (delta.magnitude < _localDirMinDistance) { data.TargetLocalDirection = Vector3.zero; return; }
        Quaternion startYawRot = Quaternion.Euler(0f, startRootYaw, 0f);
        Vector3 localDir = Quaternion.Inverse(startYawRot) * delta.normalized;
        localDir.y = 0f;
        localDir = localDir.sqrMagnitude > 0.0001f ? localDir.normalized : Vector3.zero;
        // 如果方向接近角色前方（小于阈值角度），则认为没有显著方向
        if (Vector3.Angle(Vector3.forward, localDir) <= _localDirFilterAngleDeg) { data.TargetLocalDirection = Vector3.zero; return; }
        data.TargetLocalDirection = localDir;
    }
    // 在指定时间采样动画并返回脚的局部位置
    private PoseInfo SampleClipPose(Animator anim, AnimationClip clip, float time)
    {
        anim.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        clip.SampleAnimation(anim.gameObject, time);
        var leftT = anim.GetBoneTransform(_leftFootBone);
        var rightT = anim.GetBoneTransform(_rightFootBone);
        return new PoseInfo
        {
            LeftLocal = leftT != null ? anim.transform.InverseTransformPoint(leftT.position) : Vector3.zero,
            RightLocal = rightT != null ? anim.transform.InverseTransformPoint(rightT.position) : Vector3.zero
        };
    }
    // 根据旋转曲线判断旋转何时完成（例如累计旋转达到 95% 的时间点）
    private static float CalculateRotationFinishedTime(AnimationCurve rotCurve, float totalTime)
    {
        if (rotCurve == null || rotCurve.length < 2) return 0f;
        float total = Mathf.Abs(rotCurve.keys[rotCurve.length - 1].value);
        if (total < 15f) return 0f; // 如果总旋转角度小于阈值，则不认为有明显旋转
        float threshold = total * 0.95f; // 95% 阈值
        foreach (var key in rotCurve.keys)
        {
            if (Mathf.Abs(key.value) >= threshold) return Mathf.Clamp(key.time, 0f, totalTime);
        }
        return totalTime;
    }
    // 简单滑动窗口平滑曲线的实现（可选）
    private void SmoothCurve(AnimationCurve curve, int windowSize)
    {
        if (curve == null || curve.length < windowSize) return;
        Keyframe[] newKeys = new Keyframe[curve.length];
        int half = windowSize / 2;
        for (int i = 0; i < curve.length; i++)
        {
            float sum = 0; int count = 0;
            for (int j = -half; j <= half; j++)
            {
                int idx = i + j;
                if (idx >= 0 && idx < curve.length) { sum += curve.keys[idx].value; count++; }
            }
            newKeys[i] = new Keyframe(curve.keys[i].time, sum / count, 0, 0);
        }
        curve.keys = newKeys;
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
    }
}
