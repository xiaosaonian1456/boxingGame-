using UnityEngine;
using Photon.Pun;

public class FistCollision : MonoBehaviourPun
{
    [Header("伤害设置")]
    public float damage = 2f;

    [Header("击中特效")]
    public GameObject hitEffectPrefab; // 击中特效预制体（由EffectPool对象池复用，不要挂自动销毁脚本）

    [Header("击中音效")]
    public AudioClip playerHitClip; // 击中玩家音效（为空时使用 shieldHitClip）
    public AudioClip shieldHitClip; // 击中盾牌音效

    private const int PLAYER_HIT = 0;
    private const int SHIELD_HIT = 1;

    private PhysicsSpringFist physicsSpringFist;
    private LeftPhysicsSpringFist leftPhysicsSpringFist;

    private void Awake()
    {
        physicsSpringFist = GetComponent<PhysicsSpringFist>();
        leftPhysicsSpringFist = GetComponent<LeftPhysicsSpringFist>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 只有自己的拳头执行碰撞检测
        if (!photonView.IsMine) return;

        // 判断当前是哪只拳头在攻击
        bool rightHit = physicsSpringFist != null && physicsSpringFist._isExtending && physicsSpringFist.canDamage;
        bool leftHit = leftPhysicsSpringFist != null && leftPhysicsSpringFist._isLeftExtending && leftPhysicsSpringFist.canDamage;

        if (!rightHit && !leftHit) return;

        // 击中玩家：扣血 + 特效 + 音效
        if (other.CompareTag("Player"))
        {
            PhotonView targetPlayerPhotonView = other.GetComponent<PhotonView>();

            // 不能打自己
            if (targetPlayerPhotonView == null || targetPlayerPhotonView.IsMine) return;

            Vector3 hitPoint = GetHitPoint(other);

            if (rightHit)
            {
                ProcessHit(targetPlayerPhotonView, hitPoint, PLAYER_HIT);
                physicsSpringFist.canDamage = false;
            }
            else if (leftHit)
            {
                ProcessHit(targetPlayerPhotonView, hitPoint, PLAYER_HIT);
                leftPhysicsSpringFist.canDamage = false;
            }
        }

        // 击中盾牌：特效 + 音效
        if (other.CompareTag("Dun"))
        {
            PhotonView targetPlayerPhotonView = other.GetComponentInParent<PhotonView>();

            // 不能打自己
            if (targetPlayerPhotonView == null ) return;

            Vector3 hitPoint = GetHitPoint(other);

            if (rightHit)
            {
                PlayHitEffectAndSound(hitPoint, SHIELD_HIT);
                physicsSpringFist.canDamage = false;
            }
            else if (leftHit)
            {
                PlayHitEffectAndSound(hitPoint, SHIELD_HIT);
                leftPhysicsSpringFist.canDamage = false;
            }
        }
    }

    /// <summary>
    /// 获取碰撞点位置
    /// </summary>
    private Vector3 GetHitPoint(Collider other)
    {
        return other.ClosestPoint(transform.position);
    }

    /// <summary>
    /// 处理击中玩家：造成伤害并同步特效音效
    /// </summary>
    private void ProcessHit(PhotonView targetPhotonView, Vector3 hitPoint, int hitType)
    {
        DealDamage(targetPhotonView, hitPoint);
        PlayHitEffectAndSound(hitPoint, hitType);
    }

    /// <summary>
    /// 对目标造成伤害
    /// </summary>
    private void DealDamage(PhotonView targetPhotonView, Vector3 hitPoint)
    {
        targetPhotonView.RPC(nameof(HealthBar.TakeDamage), RpcTarget.All, damage);
        Debug.Log($"击中玩家：{targetPhotonView.Owner.NickName}，造成{damage}点伤害");
    }

    /// <summary>
    /// 同步生成击中特效并播放音效
    /// </summary>
    private void PlayHitEffectAndSound(Vector3 hitPoint, int hitType)
    {
        photonView.RPC(nameof(RPC_PlayHitEffectAndSound), RpcTarget.All, hitPoint, hitType);
    }

    [PunRPC]
    private void RPC_PlayHitEffectAndSound(Vector3 position, int hitType)
    {
        // 生成特效（走对象池复用，播放完毕自动回收，不再Instantiate/Destroy）
        if (hitEffectPrefab != null)
        {
            EffectPool.Instance.Play(hitEffectPrefab, position, Quaternion.identity);
        }

        // 播放音效
        AudioClip clipToPlay = GetClipByType(hitType);
        if (clipToPlay != null)
        {
            AudioSource.PlayClipAtPoint(clipToPlay, position);
        }
    }

    /// <summary>
    /// 根据击中类型获取对应音效
    /// </summary>
    private AudioClip GetClipByType(int hitType)
    {
        if (hitType == PLAYER_HIT)
        {
            return playerHitClip != null ? playerHitClip : shieldHitClip;
        }

        return shieldHitClip;
    }
}
