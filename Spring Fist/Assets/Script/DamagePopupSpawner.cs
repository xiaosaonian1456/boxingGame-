using UnityEngine;

/// <summary>
/// 伤害飘字生成器（单机）：场景中挂一个并配置飘字预制体。
/// 任何地方调用 DamagePopupSpawner.Show(伤害值, 目标位置) 即可在目标上方弹出飘字。
/// </summary>
public class DamagePopupSpawner : MonoBehaviour
{
    public static DamagePopupSpawner Instance { get; private set; }

    public GameObject popupPrefab;    // 飘字预制体（挂DamagePopup + 3D TextMeshPro）
    public float heightOffset = 1.8f; // 飘字高度偏移（米），按目标头顶位置调整

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 在目标上方弹出伤害飘字（未配置生成器或预制体时静默跳过，不影响伤害流程）。
    /// parent不为空：作为其子物体生成并以parent位置为出生点（UI文本必须挂在Canvas下才渲染）；
    /// parent为空：在targetPosition头顶heightOffset处以世界坐标生成（适用3D文本预制体）
    /// </summary>
    public static void Show(float damage, Vector3 targetPosition, Transform parent = null)
    {
        if (Instance == null || Instance.popupPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = parent != null
            ? parent.position
            : targetPosition + Vector3.up * Instance.heightOffset;

        GameObject popup = Instantiate(Instance.popupPrefab, spawnPosition, Quaternion.identity, parent);

        DamagePopup dp = popup.GetComponent<DamagePopup>();
        if (dp != null)
        {
            dp.Setup(damage);
        }
    }
}
