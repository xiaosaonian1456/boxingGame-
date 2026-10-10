using UnityEngine;
using TMPro;

/// <summary>
/// 僵尸击杀数统计（单机僵尸模式）：统计玩家击杀的僵尸数量并刷新UI文本。
/// 计数入口：ZombieStateManager.HandleDeath（僵尸血量归零死亡时）调用AddKill。
/// 只有真正被打死才计数——倒计时收尾的爆炸清场直接回收入池、不走死亡流程，不会计入。
/// 对象池复用不影响计数：僵尸每次死亡都会触发一次HandleDeath，击杀数只增不减。
/// 挂法：挂在场景任意物体上，把显示击杀数的TMP_Text拖入killText即可。
/// </summary>
public class ZombieKillCounter : MonoBehaviour
{
    /// 全局访问点：ZombieStateManager死亡时通过它上报击杀
    public static ZombieKillCounter Instance { get; private set; }

    [Header("UI引用")]
    public TMP_Text killText;             // 显示击杀数的文本（TMP_Text）

    [Header("显示格式")]
    public string prefix = "击杀数：";     // 数字前的前缀文本

    /// 当前击杀数，外部脚本可查询
    public int KillCount { get; private set; }

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
        KillCount = 0;
        RefreshText();
    }

    /// <summary>
    /// 击杀数+1并刷新显示（由ZombieStateManager.HandleDeath调用）
    /// </summary>
    public void AddKill()
    {
        KillCount++;
        RefreshText();
    }

    private void RefreshText()
    {
        if (killText != null)
        {
            killText.text = prefix + KillCount;
        }
    }
}
