using System.Collections;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// 护盾耐久：挂在护盾（Dun）预制体上，记录受击次数，被打爆时由召唤者客户端销毁护盾并清空召唤者蓝量。
/// 受击特效/音效仍由 ProjectileFist 在命中时播放，本脚本只负责计数与销毁。
/// 打爆分两阶段：先隐藏外观但保留碰撞体一个宽限期（让各客户端仍在飞行中的拳头仍能命中护盾并正常消失，
/// 避免网络销毁比本地模拟的子弹先到，导致子弹穿过护盾位置打到玩家），宽限期结束后才真正销毁。
/// </summary>
public class ShieldDurability : MonoBehaviourPun
{
    [Tooltip("护盾被打爆前能承受的最大受击次数")]
    public int maxHits = 5;
    [Tooltip("护盾被打爆后碰撞体保留的宽限时间（秒），期间护盾不可见但仍能拦住飞行中的拳头")]
    public float breakGraceTime = 0.3f;

    private int _hitCount;
    private bool _broken;

    /// <summary>
    /// 登记一次受击（由 ProjectileFist 命中护盾时通过 RPC 调用）。
    /// 只有召唤者客户端计数并决定是否销毁，避免多客户端重复销毁。
    /// </summary>
    [PunRPC]
    public void RegisterHit()
    {
        if (_broken) return;
        if (!photonView.IsMine) return;

        _hitCount++;
        if (_hitCount >= maxHits)
        {
            _broken = true;
            Debug.Log("护盾被打爆");

            // 所有客户端立即隐藏护盾外观，碰撞体保留到宽限期结束
            photonView.RPC(nameof(RPC_HideShieldVisual), RpcTarget.All);
            StartCoroutine(DestroyAfterGrace());
        }
    }

    /// <summary>
    /// 隐藏护盾外观（所有客户端本地执行），碰撞体保持有效以拦截飞行中的拳头
    /// </summary>
    [PunRPC]
    private void RPC_HideShieldVisual()
    {
        _broken = true;

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = false;
        }
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>
    /// 宽限期结束后由召唤者客户端清空蓝量并销毁护盾
    /// </summary>
    private IEnumerator DestroyAfterGrace()
    {
        yield return new WaitForSeconds(breakGraceTime);
        BreakShield();
    }

    /// <summary>
    /// 护盾被打爆：清空召唤者蓝量并销毁护盾
    /// </summary>
    private void BreakShield()
    {
        // 蓝量是每个客户端本地管理的，此处运行在召唤者客户端，找到的就是召唤者的蓝量
        PlayerMana playerMana = FindObjectOfType<PlayerMana>();
        if (playerMana != null)
        {
            playerMana.ConsumeAllMana();
        }

        PhotonNetwork.Destroy(gameObject);
    }
}
