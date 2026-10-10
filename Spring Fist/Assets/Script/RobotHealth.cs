using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 本地血条管理（单机）：挂在需要血量的物体根节点上（机器人、单机模式玩家均可）。
/// 负责血量初始化、扣血、实时刷新Image血条（fillImage.fillAmount）。
/// 机器人血量归零进入死亡状态，RobotStateManager攻击时会检查IsDead停止出拳。
/// 单机模式玩家（Tag为Player）血量归零进入复活倒计时（参考联机HealthBar的四人模式复活逻辑）：
/// 倒计时期间保持死亡状态无法受伤，并暂停ZombieSpawner生成僵尸，
/// DanJiLeftQuan/DanJiRightQuan检查IsRespawning禁止出拳（移动不受影响）；
/// 倒计时结束满血复活，恢复僵尸生成。僵尸/机器人死亡后不复活。
/// 与联机玩家用的HealthBar不同，本脚本不依赖Photon，纯本地逻辑。
/// </summary>
public class RobotHealth : MonoBehaviour
{
    [Header("血量设置")]
    public float maxHealth = 100f;    // 直接在Inspector改总血量
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    [Header("血条UI")]
    public Image fillImage;           // 拖入血条填充图片（Image的Image Type需设为Filled）
    [Tooltip("伤害飘字的挂载点（拖入血条Canvas或头顶空物体）：UI文本必须挂在Canvas下才渲染；不填则在头顶世界坐标生成（仅适用3D文本预制体）")]
    public Transform popupAnchor;

    [Header("单机玩家复活设置")]
    [Tooltip("死亡后是否进入复活倒计时。单机玩家（Tag为Player）会自动开启，无需勾选；僵尸/机器人保持false死亡不复活")]
    public bool respawnOnDeath = false;
    public float respawnDelay = 5f;   // 死亡后复活倒计时（秒）

    /// <summary>
    /// 是否处于复活倒计时中（参考HealthBar四人模式）。
    /// 复活期间保持IsDead无法受伤，ZombieSpawner暂停生成僵尸。
    /// 僵尸/机器人永远为 false。
    /// </summary>
    public bool IsRespawning { get; private set; }

    /// <summary>复活剩余秒数（供RespawnCountdownText等UI显示倒计时）</summary>
    public float RespawnRemaining
    {
        get { return IsRespawning ? Mathf.Max(0f, _respawnEndTime - Time.time) : 0f; }
    }

    /// 血量变化回调（当前血量, 最大血量），需要显示数字血量等外部UI可订阅
    public event System.Action<float, float> OnHealthChanged;

    /// 死亡回调，只触发一次（ResetHealth后重置）
    public event System.Action OnDeath;

    /// 复活完成回调（复活倒计时结束、满血恢复时触发）
    public event System.Action OnRespawn;

    private Coroutine _respawnCoroutine;
    private float _respawnEndTime;

    private void Awake()
    {
        // 单机模式玩家（Tag为Player）自动开启复活；僵尸/机器人死亡后不复活
        if (CompareTag("Player"))
        {
            respawnOnDeath = true;
        }
        ResetHealth();
    }

    /// <summary>
    /// 扣血：实时刷新血条，归零时触发死亡。
    /// 单机玩家归零时进入复活倒计时（期间保持死亡状态无法受伤，并暂停生成僵尸）
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0f, maxHealth);
        UpdateHealthBar();

        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            OnDeath?.Invoke();

            if (respawnOnDeath)
            {
                // 单机玩家：进入复活倒计时，期间暂停生成僵尸，复活后恢复
                IsRespawning = true;
                if (ZombieSpawner.Instance != null)
                {
                    ZombieSpawner.Instance.SpawningPaused = true;
                }
                _respawnCoroutine = StartCoroutine(RespawnCoroutine());
                Debug.Log("血量归零，" + respawnDelay + "秒后复活");
            }
            else
            {
                Debug.Log("机器人血量归零，已死亡");
            }
        }
    }

    /// <summary>
    /// 单机玩家复活流程（参考HealthBar四人模式）：倒计时结束后满血复活，恢复生成僵尸
    /// </summary>
    private IEnumerator RespawnCoroutine()
    {
        _respawnEndTime = Time.time + respawnDelay;
        yield return new WaitForSeconds(respawnDelay);
        _respawnCoroutine = null;

        // 复活：血量恢复满，清除死亡/复活标记，恢复僵尸生成
        CurrentHealth = maxHealth;
        IsDead = false;
        IsRespawning = false;
        UpdateHealthBar();

        if (ZombieSpawner.Instance != null)
        {
            ZombieSpawner.Instance.SpawningPaused = false;
        }
        OnRespawn?.Invoke();
        Debug.Log("复活完成，血量已恢复");
    }

    /// <summary>
    /// 回血（不超过上限），死亡状态下无效，需先ResetHealth
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, maxHealth);
        UpdateHealthBar();
    }

    /// <summary>
    /// 恢复满血并清除死亡/复活状态（重开一局时调用）。
    /// 若正在复活倒计时中则打断倒计时，并恢复僵尸生成
    /// </summary>
    public void ResetHealth()
    {
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }
        if (IsRespawning)
        {
            IsRespawning = false;
            if (ZombieSpawner.Instance != null)
            {
                ZombieSpawner.Instance.SpawningPaused = false;
            }
        }

        CurrentHealth = maxHealth;
        IsDead = false;
        UpdateHealthBar();
    }

    // 刷新血条UI并通知订阅者
    private void UpdateHealthBar()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
        }
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
