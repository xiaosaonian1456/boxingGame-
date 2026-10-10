using UnityEngine;
using TMPro;

/// <summary>
/// 单机模式复活倒计时UI（僵尸模式）：玩家死亡进入复活倒计时期间，
/// 在TextMeshPro上显示"复活倒计时：X"（秒数向上取整），复活后自动隐藏。
/// 挂法：挂在倒计时文本对象上，health拖入玩家的RobotHealth
/// （不填则自动找Tag为Player的玩家身上的RobotHealth），
/// countdownText不填则自动取本物体上的TMP_Text。
/// 通过开关TMP_Text.enabled显隐（而不是SetActive），脚本挂在文本同物体上也能持续工作。
/// 纯本地逻辑，不依赖Photon。
/// </summary>
public class RespawnCountdownText : MonoBehaviour
{
    public RobotHealth health;        // 玩家的RobotHealth（不填则自动按Tag为Player查找）
    public TMP_Text countdownText;    // 显示倒计时的文本（不填则自动取本物体上的）

    private void Awake()
    {
        if (countdownText == null)
        {
            countdownText = GetComponent<TMP_Text>();
        }
        // 开局隐藏（玩家未死亡不显示倒计时）
        if (countdownText != null)
        {
            countdownText.enabled = false;
        }
    }

    private void Update()
    {
        if (health == null)
        {
            FindPlayerHealth(); // 玩家可能延迟生成，持续寻找
            return;
        }
        if (countdownText == null)
        {
            return;
        }

        if (health.IsRespawning)
        {
            countdownText.enabled = true;
            countdownText.text = "复活倒计时：" + Mathf.CeilToInt(health.RespawnRemaining);
        }
        else if (countdownText.enabled)
        {
            countdownText.enabled = false;
        }
    }

    /// 自动查找Tag为Player的玩家身上的RobotHealth
    private void FindPlayerHealth()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            health = player.GetComponentInChildren<RobotHealth>();
        }
    }
}
