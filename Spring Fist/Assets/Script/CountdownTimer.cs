using UnityEngine;
using TMPro;

/// <summary>
/// 单机倒计时UI（僵尸模式）：从totalTime秒开始倒数，格式显示为"分:秒"（如8:00）。
/// 倒计时归零时判定游戏结束：弹出游戏结束图片，并按设置冻结游戏（僵尸、玩家全部停住）。
/// 纯本地逻辑，不依赖Photon。挂法：挂在场景任意物体上，拖入UI文本和游戏结束图片即可。
/// </summary>
public class CountdownTimer : MonoBehaviour
{
    /// 全局访问点：ZombieSpawner等需要读剩余时间的外部脚本通过它访问
    public static CountdownTimer Instance { get; private set; }

    [Header("倒计时设置")]
    public float totalTime = 480f;        // 总时长（秒）：480秒 = 8分钟

    [Header("UI引用")]
    public TMP_Text timeText;             // 显示倒计时的文本（TMP_Text）
    public GameObject gameOverImage;      // 游戏结束图片（归零时SetActive(true)，默认应保持隐藏）

    [Header("游戏结束设置")]
    public bool freezeOnGameOver = true;  // 归零时是否冻结游戏（Time.timeScale=0，僵尸和攻击全部停住）

    /// 游戏是否已结束（倒计时归零），外部脚本可查询
    public bool IsGameOver { get; private set; }

    /// 剩余时间（秒），外部UI需要读秒数时用
    public float RemainingTime { get; private set; }

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

    private void Start()
    {
        RemainingTime = totalTime;
        IsGameOver = false;
        UpdateText();
        // 保险：开局时确保游戏结束图片是隐藏的
        if (gameOverImage != null)
        {
            gameOverImage.SetActive(false);
        }
    }

    private void Update()
    {
        if (IsGameOver || timeText == null)
        {
            return;
        }

        RemainingTime -= Time.deltaTime;
        if (RemainingTime <= 0f)
        {
            RemainingTime = 0f;
            GameOver();
        }
        UpdateText();
    }

    /// <summary>
    /// 倒计时归零：弹游戏结束图片，冻结游戏
    /// </summary>
    private void GameOver()
    {
        IsGameOver = true;
        if (gameOverImage != null)
        {
            gameOverImage.SetActive(true);
        }
        if (freezeOnGameOver)
        {
            Time.timeScale = 0f;
        }
        Debug.Log("倒计时结束，游戏结束");
    }

    /// <summary>
    /// 刷新文本：格式"分:秒"，秒数向上取整（7:01下一秒直接跳7:00而不是先显示7:00再跳）
    /// </summary>
    private void UpdateText()
    {
        if (timeText == null)
        {
            return;
        }
        int totalSeconds = Mathf.CeilToInt(RemainingTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timeText.text = minutes + ":" + seconds.ToString("00");
    }
}
