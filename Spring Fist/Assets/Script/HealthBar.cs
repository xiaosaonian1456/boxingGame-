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

    void Start()
    {
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

            // 死亡后通过RPC通知房主，由房主统一切换场景
            photonView.RPC("RPC_RequestGameOver", RpcTarget.MasterClient);
            Debug.Log("血量归零，已通知房主切换场景");
        }
    }

    // ✅ 扣血RPC：收到后只有拥有者自己改血，再播报给其他人同步UI
    [PunRPC]
    public void TakeDamage(float damage)
    {
        // 只有本地玩家能修改自己的血量（防作弊）
        if (photonView.IsMine)
        {
            // 局间倒计时等非对局阶段忽略伤害
            GameTimeText gameTimeText = FindObjectOfType<GameTimeText>();
            if (gameTimeText != null && !gameTimeText.IsRoundActive)
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
    /// 新一局开始时重置：满血并清除死亡/结算标记
    /// </summary>
    public void ResetForNewRound()
    {
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
