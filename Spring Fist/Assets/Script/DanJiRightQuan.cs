using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// 单机模式右手出拳：检测Pico右手柄向前挥动动作（电脑端按F键模拟），
/// 从FistSkinManager的特效列表中随机挑一种拳头子弹发射。
/// 子弹由ProjectileFist对象池复用，不反复Instantiate/Destroy。
/// 玩家复活倒计时期间（RobotHealth.IsRespawning）无法攻击，但移动不受影响。
/// </summary>
public class DanJiRightQuan : MonoBehaviour
{
    [Header("出拳设置")]
    public KeyCode fireKey = KeyCode.F;
    public float punchCooldown = 0.5f;   // 两次出拳的最小间隔
    [Header("发射拳头设置")]
    public GameObject projectileFistPrefab; // 兜底子弹预制体（FistSkinManager未配置时使用）
    [Tooltip("子弹飞行距离倍率（相对ProjectileFist.maxDistance）")]
    public float projectileDistanceMultiplier = 1f;
    [Header("动作出拳设置")]
    public float punchVelocityThreshold = 2.2f;   // 前向挥动速度阈值（米/秒），低于此速度不出拳
    public float punchDirectionStrictness = 1.2f; // 前向分量必须超过横向/纵向分量的倍数，防止回收或横移误触发
    public float velocitySmoothing = 10f;         // 速度平滑系数，过滤手柄抖动
    [Header("手柄设置")]
    private Transform RightController;
    private Transform handle; // 手柄的Transform引用
    public Transform fist;    // 手的Transform引用

    private Vector3 _prevHandlePos;
    private Vector3 _smoothedVelocity;
    private bool _hasPrevHandlePos = false;

    // 出拳冷却中
    public bool _isPunching = false;

    // 玩家血条（复活倒计时期间禁止出拳），懒加载缓存
    private RobotHealth _playerHealth;

    private void Awake()
    {
        XROrigin xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin != null)
        {
            RightController = xrOrigin.transform.Find("[Right InteractionAttachController] Attach");
            if (RightController != null)
            {
                handle = RightController.GetChild(0).transform;
            }
        }
    }

    void Update()
    {
        // 复活期间仍持续更新手柄速度跟踪（DetectPunchMotion内部拦截出拳），避免复活瞬间速度突变误触发出拳
        DetectPunchMotion();
        if (Input.GetKeyDown(fireKey) && !_isPunching && !IsPlayerRespawning())
        {
            FirePunch();
            Debug.Log("出拳");
        }
    }

    /// <summary>
    /// 玩家是否处于复活倒计时中（RobotHealth挂在Tag为Player的玩家根节点上）
    /// </summary>
    private bool IsPlayerRespawning()
    {
        if (_playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerHealth = player.GetComponentInChildren<RobotHealth>();
            }
        }
        return _playerHealth != null && _playerHealth.IsRespawning;
    }

    /// <summary>
    /// 检测手柄向前挥动的出拳动作：手柄速度在本地方向上的前向分量(z)超过阈值，
    /// 且明显大于横向(x)/纵向(y)分量时才判定为出拳；回收(z为负)或左右移动不触发
    /// </summary>
    void DetectPunchMotion()
    {
        if (handle == null || Time.deltaTime <= 0f)
        {
            return;
        }

        Vector3 currentPos = handle.position;
        if (!_hasPrevHandlePos)
        {
            _prevHandlePos = currentPos;
            _hasPrevHandlePos = true;
            return;
        }

        Vector3 frameVelocity = (currentPos - _prevHandlePos) / Time.deltaTime;
        _prevHandlePos = currentPos;
        _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, frameVelocity, Mathf.Clamp01(velocitySmoothing * Time.deltaTime));

        Vector3 localVelocity = handle.InverseTransformDirection(_smoothedVelocity);
        if (!_isPunching
            && !IsPlayerRespawning() // 复活倒计时期间无法攻击
            && localVelocity.z > punchVelocityThreshold
            && localVelocity.z > Mathf.Abs(localVelocity.x) * punchDirectionStrictness
            && localVelocity.z > Mathf.Abs(localVelocity.y) * punchDirectionStrictness)
        {
            FirePunch();
            Debug.Log("出拳");
        }
    }

    void FirePunch()
    {
        // 防御性检查
        if (fist == null)
        {
            Debug.LogError("[DanJiRightQuan] fist 未赋值！请在 Inspector 中设置 fist 引用。");
            return;
        }

        // 单机模式每次出拳从FistSkinManager特效列表中随机挑一种
        GameObject prefab = GetRandomPrefab();
        if (prefab == null)
        {
            Debug.LogError("[DanJiRightQuan] 未配置子弹预制体！请配置 FistSkinManager 的特效列表或本组件的 projectileFistPrefab 兜底。");
            return;
        }

        _isPunching = true;

        // 从对象池取一个拳头子弹发射（携带特效），手上的拳头保持不动。
        // 飞行方向以手柄的+Z（出拳检测也是按手柄本地z判定的），不能用拳头模型的rotation（它自带X=90°旋转）
        Vector3 punchDir = handle != null ? handle.forward : fist.forward;
        ProjectileFist.Spawn(prefab, fist.position, punchDir, true, -1, projectileDistanceMultiplier, transform.root);

        // 冷却结束后才能再次出拳
        Invoke(nameof(ResetState), punchCooldown);
    }

    /// <summary>
    /// 从FistSkinManager的特效列表中随机挑一个预制体，未配置时用兜底预制体
    /// </summary>
    private GameObject GetRandomPrefab()
    {
        if (FistSkinManager.Instance != null
            && FistSkinManager.Instance.projectileVariants != null
            && FistSkinManager.Instance.projectileVariants.Length > 0)
        {
            int index = Random.Range(0, FistSkinManager.Instance.projectileVariants.Length);
            return FistSkinManager.Instance.GetPrefab(index);
        }
        return projectileFistPrefab;
    }

    void ResetState()
    {
        _isPunching = false;
    }
}
