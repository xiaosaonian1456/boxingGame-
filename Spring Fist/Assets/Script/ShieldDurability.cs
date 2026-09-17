using UnityEngine;
using Photon.Pun;

/// <summary>
/// 护盾耐久：挂在护盾（Dun）预制体上，记录受击次数，被打爆时由召唤者客户端销毁护盾并清空召唤者蓝量。
/// 受击特效/音效仍由 ProjectileFist 在命中时播放，本脚本只负责计数与销毁。
/// </summary>
public class ShieldDurability : MonoBehaviourPun
{
    [Tooltip("护盾被打爆前能承受的最大受击次数")]
    public int maxHits = 5;

    private int _hitCount;

    /// <summary>
    /// 登记一次受击（由 ProjectileFist 命中护盾时通过 RPC 调用）。
    /// 只有召唤者客户端计数并决定是否销毁，避免多客户端重复销毁。
    /// </summary>
    [PunRPC]
    public void RegisterHit()
    {
        if (!photonView.IsMine) return;

        _hitCount++;
        if (_hitCount >= maxHits)
        {
            BreakShield();
        }
    }

    /// <summary>
    /// 护盾被打爆：清空召唤者蓝量并销毁护盾
    /// </summary>
    private void BreakShield()
    {
        Debug.Log("护盾被打爆");

        // 蓝量是每个客户端本地管理的，此处运行在召唤者客户端，找到的就是召唤者的蓝量
        PlayerMana playerMana = FindObjectOfType<PlayerMana>();
        if (playerMana != null)
        {
            playerMana.ConsumeAllMana();
        }

        PhotonNetwork.Destroy(gameObject);
    }
}
