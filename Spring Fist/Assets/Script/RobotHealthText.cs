using UnityEngine;
using TMPro;

/// <summary>
/// 在TextMeshPro上实时显示RobotHealth的血量（单机）。
/// 挂在血条UI的Text对象上，拖入要显示的RobotHealth；
/// 订阅OnHealthChanged事件，血量变化时立即刷新，不做每帧轮询。
/// </summary>
public class RobotHealthText : MonoBehaviour
{
    public RobotHealth health;    // 要显示血量的目标（机器人或单机玩家）
    public TMP_Text healthText;   // 显示血量的文本（不填则自动取本物体上的）

    private void Awake()
    {
        if (healthText == null)
        {
            healthText = GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        if (health == null)
        {
            return;
        }
        health.OnHealthChanged += UpdateText;
        // 订阅时立即刷新一次（RobotHealth在Awake里初始化血量，那时还没订阅，收不到首次事件）
        UpdateText(health.CurrentHealth, health.maxHealth);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= UpdateText;
        }
    }

    private void UpdateText(float current, float max)
    {
        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(current).ToString();
        }
    }
}
