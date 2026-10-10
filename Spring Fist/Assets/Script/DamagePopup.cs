using UnityEngine;
using TMPro;

/// <summary>
/// 伤害飘字：生成后向上升起并逐渐淡出，到时间自动销毁，始终面向摄像机。
/// 挂在飘字预制体上（预制体 = 一个3D TextMeshPro对象 + 本脚本）。
/// 由DamagePopupSpawner统一生成，不要手动放进场景。
/// </summary>
public class DamagePopup : MonoBehaviour
{
    public float floatSpeed = 1f;   // 上升速度（米/秒）
    public float lifetime = 0.8f;   // 存在时间（秒），到时间自动销毁
    [Tooltip("飘字颜色池，生成时随机挑一个（在Inspector里增删颜色）")]
    public Color[] randomColors = { Color.red, new Color(1f, 0.5f, 0f), Color.yellow };

    private TMP_Text _text;
    private float _timer;
    private Color _initialColor;

    private void Awake()
    {
        _text = GetComponentInChildren<TMP_Text>();
        if (_text != null)
        {
            // 生成时随机挑一个颜色，淡出基于这个颜色计算
            if (randomColors != null && randomColors.Length > 0)
            {
                _text.color = randomColors[Random.Range(0, randomColors.Length)];
            }
            _initialColor = _text.color;
        }
    }

    /// <summary>
    /// 设置显示的伤害数值（生成时由DamagePopupSpawner调用）
    /// </summary>
    public void Setup(float damage)
    {
        if (_text != null)
        {
            _text.text = "-" + Mathf.CeilToInt(damage);
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        // 向上升起
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // 面向摄像机（与UIFaceCamera同款逻辑）
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }

        // 逐渐淡出
        if (_text != null)
        {
            float alpha = Mathf.Clamp01(1f - _timer / lifetime);
            _text.color = new Color(_initialColor.r, _initialColor.g, _initialColor.b, _initialColor.a * alpha);
        }

        // 到时间销毁
        if (_timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
