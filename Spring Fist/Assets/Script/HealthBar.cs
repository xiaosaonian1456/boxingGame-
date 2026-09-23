using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class HealthBar : MonoBehaviourPunCallbacks
{
    [Header("基础设置")]
    public float maxHealth = 360f; // 直接在Inspector改总血量
    public Image fillImage;        // 拖入血条填充图片
    public bool loadOver = false;
    public bool isLoad = false;
    public float _currentHealth;

    [Header("四人模式复活设置")]
    public float respawnDelay = 5f; // 死亡后复活倒计时（秒）
    public float fourPlayerMaxHealth = 100f; // 四人模式总血量（临时调整，不影响双人模式的maxHealth）

    /// <summary>
    /// 是否处于四人模式的复活倒计时中（通过RPC同步到所有客户端）。
    /// 复活期间无法攻击/防御/受伤，击中复活中的敌人不加分。
    /// 双人模式下永远为 false。
    /// </summary>
    public bool IsRespawning { get; private set; }

    /// <summary>复活剩余秒数（仅本地拥有者倒计时准确，供UI显示）</summary>
    public float RespawnRemaining
    {
        get { return IsRespawning ? Mathf.Max(0f, _respawnEndTime - Time.time) : 0f; }
    }

    private Coroutine _respawnCoroutine;
    private float _respawnEndTime;

    void Start()
    {
        // 四人模式（场景中有GameTimeText4P）临时使用100血量，双人模式保持预制体原值
        if (FindObjectOfType<GameTimeText4P>() != null)
        {
            maxHealth = fourPlayerMaxHealth;
        }

        // 所有人开局满血
        _currentHealth = maxHealth;
        UpdateHealthBar();
    }

    private void Update()
    {
        // 只有血条的拥有者检测自己是否死亡
        if (_currentHealth <= 0 && !loadOver && photonView.IsMine)
        {
            loadOver = true;

            if (FindObjectOfType<GameTimeText4P>() != null)
            {
                // 四人计分模式：进入复活倒计时，期间无法攻击/防御/受伤
                photonView.RPC("RPC_SetRespawning", RpcTarget.All, true);

                // 死亡瞬间强制收回护盾，复活期间不能维持防御
                PlayerDefence defence = GetComponent<PlayerDefence>();
                if (defence != null)
                {
                    defence.ForceReleaseShield();
                }

                _respawnCoroutine = StartCoroutine(RespawnCoroutine());
                Debug.Log("血量归零，" + respawnDelay + "秒后复活");
            }
            else
            {
                // 双人模式：死亡后通过RPC通知房主，由房主统一切换场景
                photonView.RPC("RPC_RequestGameOver", RpcTarget.MasterClient);
                Debug.Log("血量归零，已通知房主切换场景");
            }
        }
    }

    /// <summary>
    /// 四人模式复活流程：倒计时结束后满血复活，恢复攻击/防御/受伤
    /// </summary>
    private IEnumerator RespawnCoroutine()
    {
        _respawnEndTime = Time.time + respawnDelay;
        yield return new WaitForSeconds(respawnDelay);
        _respawnCoroutine = null;

        // 复活：血量恢复满并同步给其他客户端
        _currentHealth = maxHealth;
        UpdateHealthBar();
        photonView.RPC("SyncHealth", RpcTarget.Others, _currentHealth);

        loadOver = false;
        photonView.RPC("RPC_SetRespawning", RpcTarget.All, false);
        Debug.Log("复活完成，血量已恢复");
    }

    // 复活状态同步RPC：所有客户端更新标记（发射端据此判断命中复活中的敌人不加分）
    [PunRPC]
    public void RPC_SetRespawning(bool value)
    {
        IsRespawning = value;
    }

    // ✅ 扣血RPC：收到后只有拥有者自己改血，再播报给其他人同步UI
    [PunRPC]
    public void TakeDamage(float damage)
    {
        // 只有本地玩家能修改自己的血量（防作弊）
        if (photonView.IsMine)
        {
            // 复活倒计时期间无法受伤
            if (IsRespawning)
            {
                return;
            }

            // 局间倒计时等非对局阶段忽略伤害（兼容双人GameTimeText与四人GameTimeText4P）
            GameTimeText gameTimeText = FindObjectOfType<GameTimeText>();
            if (gameTimeText != null && !gameTimeText.IsRoundActive)
            {
                return;
            }
            GameTimeText4P gameTimeText4P = FindObjectOfType<GameTimeText4P>();
            if (gameTimeText4P != null && !gameTimeText4P.IsRoundActive)
            {
                return;
            }

            _currentHealth = Mathf.Clamp(_currentHealth - damage, 0, maxHealth);
            UpdateHealthBar();

            // 扣血后把最新血量播报给对手同步显示
            photonView.RPC("SyncHealth", RpcTarget.Others, _currentHealth);
        }
    }

    // 血量同步RPC：非拥有者接收并刷新血条
    [PunRPC]
    public void SyncHealth(float newHealth)
    {
        if (!photonView.IsMine)
        {
            _currentHealth = newHealth;
            UpdateHealthBar();
        }
    }

    // 死亡通知RPC：只有房主接收并通知 GameTimeText 执行结算
    [PunRPC]
    public void RPC_RequestGameOver(PhotonMessageInfo info)
    {
        // 只由房主执行
        if (!PhotonNetwork.IsMasterClient) return;

        if (!isLoad)
        {
            isLoad = true;
            Debug.Log($"收到玩家 {info.Sender.NickName} 的死亡通知，房主通知 GameTimeText 执行结算");

            GameTimeText gameTimeText = FindObjectOfType<GameTimeText>();
            if (gameTimeText != null)
            {
                gameTimeText.photonView.RPC("RPC_GameOverByDeath", RpcTarget.AllBuffered);
            }
        }
    }

    /// <summary>
    /// 立即复活（四人模式回合结束时由GameTimeText4P调用，避免复活提示与回合结算文字重叠）
    /// </summary>
    public void RespawnImmediately()
    {
        if (!IsRespawning) return;

        // 打断进行中的复活倒计时（仅拥有者本地有协程）
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }

        _currentHealth = maxHealth;
        loadOver = false;
        UpdateHealthBar();

        if (photonView.IsMine)
        {
            photonView.RPC("SyncHealth", RpcTarget.Others, _currentHealth);
            photonView.RPC("RPC_SetRespawning", RpcTarget.All, false);
        }
        Debug.Log("回合结束，立即复活");
    }

    /// <summary>
    /// 新一局开始时重置：满血并清除死亡/结算/复活标记
    /// </summary>
    public void ResetForNewRound()
    {
        // 打断进行中的复活倒计时（仅拥有者本地有协程）
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }
        IsRespawning = false;

        _currentHealth = maxHealth;
        loadOver = false;
        isLoad = false;
        UpdateHealthBar();
    }

    // 更新血条UI
    private void UpdateHealthBar()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = _currentHealth / maxHealth;
        }
    }
}
