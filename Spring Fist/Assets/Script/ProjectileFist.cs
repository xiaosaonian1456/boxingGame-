using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// 发射出去的拳头子弹：由对象池复用，向前飞行，命中敌人扣血后消失，飞到最大距离也消失。
/// 预制体要求：挂本脚本 + Collider(isTrigger)，可把粒子特效作为子物体（发射时自动从头播放）。
/// 命中规则：联机按PhotonView的Owner识别敌我；单机按shooterRoot识别发射者自己（命中自己层级不触发）。
/// 命中Tag为Player（联机走HealthBar RPC扣血；单机无PhotonView时走RobotHealth本地扣血）、
/// 命中Tag为Enemy（机器人/僵尸，RobotHealth本地扣血；发射者也是怪物时直接穿过，怪物之间不互相伤害）、
/// 命中Tag为Dun（盾牌，不扣血只计受击）。
/// </summary>
public class ProjectileFist : MonoBehaviour
{
    [Header("飞行设置")]
    public float speed = 15f;        // 飞行速度（米/秒）
    public float maxDistance = 5f;   // 最大飞行距离，到达后未命中也消失

    [Header("伤害设置")]
    public float damage = 2f;

    [Header("击中特效")]
    public GameObject hitEffectPrefab; // 击中特效预制体（由EffectPool对象池复用，不要挂自动销毁脚本）

    [Header("击中音效")]
    public AudioClip playerHitClip; // 击中玩家音效（为空时使用 shieldHitClip）
    public AudioClip shieldHitClip; // 击中盾牌音效
    [Tooltip("击中音效的最小可听距离（米）：此距离内音量不衰减。默认1米在远程交战时命中音几乎听不见，调大保证击中反馈")]
    public float hitSoundMinDistance = 15f;

    private Vector3 _startPos;
    private Vector3 _flyDirection; // 飞行方向（手柄的+Z），与模型自身朝向解耦
    private bool _isMine;  // 是否为发射者客户端的实例（只有它做伤害判定）
    private int _shooterActorNumber; // 发射者的ActorNumber，所有客户端都忽略属于发射者的碰撞体
    private Transform _shooterRoot;  // 发射者根节点（单机模式用于识别"自己"：命中自己层级内的碰撞体不触发）
    private bool _flying;
    private GameObject _prefab; // 记录来源预制体，回收时用
    private float _distanceMultiplier = 1f; // 飞行距离倍率（如左手发射3倍距离），每次发射时重置

    // 每个预制体对应一个空闲实例池，避免反复Instantiate/Destroy
    private static readonly Dictionary<GameObject, Stack<ProjectileFist>> _freePools = new Dictionary<GameObject, Stack<ProjectileFist>>();
    private static Transform _poolRoot;

    /// <summary>
    /// 从对象池取一个拳头子弹并发射（没有空闲实例才Instantiate）
    /// </summary>
    public static ProjectileFist Spawn(GameObject prefab, Vector3 position, Vector3 direction, bool isMine, int shooterActorNumber, float distanceMultiplier = 1f, Transform shooterRoot = null)
    {
        if (prefab == null) return null;

        ProjectileFist fist = null;
        Stack<ProjectileFist> pool;
        if (_freePools.TryGetValue(prefab, out pool))
        {
            // 跳过被外部销毁的无效实例
            while (pool.Count > 0 && fist == null)
            {
                fist = pool.Pop();
            }
        }

        if (fist == null)
        {
            GameObject obj = Instantiate(prefab, GetPoolRoot());
            obj.name = prefab.name; // 去掉(Clone)后缀，方便在Hierarchy中辨认
            fist = obj.GetComponent<ProjectileFist>();
            if (fist == null)
            {
                Debug.LogError("[ProjectileFist] 预制体上没有 ProjectileFist 组件！");
                Destroy(obj);
                return null;
            }
            fist._prefab = prefab;

            // 触发器要产生OnTriggerEnter需要Rigidbody，缺了自动补一个运动学刚体
            if (fist.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = fist.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        fist.Launch(position, direction, isMine, shooterActorNumber, distanceMultiplier, shooterRoot);
        return fist;
    }

    private static Transform GetPoolRoot()
    {
        if (_poolRoot == null)
        {
            _poolRoot = new GameObject("ProjectileFistPool").transform;
        }
        return _poolRoot;
    }

    private void Launch(Vector3 position, Vector3 direction, bool isMine, int shooterActorNumber, float distanceMultiplier, Transform shooterRoot)
    {
        _flyDirection = direction.normalized;
        _startPos = position;
        _isMine = isMine;
        _shooterActorNumber = shooterActorNumber;
        _shooterRoot = shooterRoot;
        _distanceMultiplier = distanceMultiplier;
        _flying = true;

        transform.position = position;
        // 模型朝向 = 飞行方向 + 预制体自身调好的旋转偏移（如X=90°），互不影响
        transform.rotation = Quaternion.LookRotation(_flyDirection) * _prefab.transform.rotation;

        gameObject.SetActive(true);

        // 播放拳头携带的特效（先清除上次残留再从头播放）
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear();
            ps.Play();
        }
    }

    private void Update()
    {
        if (!_flying) return;

        transform.position += _flyDirection * speed * Time.deltaTime;

        // 到达最大距离仍未命中，回收
        if (Vector3.Distance(_startPos, transform.position) >= maxDistance * _distanceMultiplier)
        {
            Despawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_flying) return;

        // 击中玩家
        if (other.CompareTag("Player"))
        {
            // 不能打中自己：命中发射者自己层级内的碰撞体则直接忽略（单机模式按发射者根节点识别）
            if (IsOwnCollider(other)) return;

            // 用GetComponentInParent：玩家身体碰撞体可能在子物体上，PhotonView在根节点（与PlayerCollider一致）
            PhotonView targetView = other.GetComponentInParent<PhotonView>();
            if (targetView == null)
            {
                // 单机模式（玩家身上无PhotonView）：本地直接扣血
                if (_isMine)
                {
                    RobotHealth localHealth = other.GetComponentInParent<RobotHealth>();
                    if (localHealth != null)
                    {
                        localHealth.TakeDamage(damage);
                        Debug.Log($"拳头子弹击中玩家（单机），造成{damage}点伤害");
                    }
                }
                PlayHitEffectAndSound(other.ClosestPoint(transform.position),
                    playerHitClip != null ? playerHitClip : shieldHitClip);
                Despawn();
                return;
            }

            // 所有客户端都忽略属于发射者的碰撞体（子弹生成在发射者拳头处，生成瞬间必与发射者替身重叠）。
            // 按OwnerActorNr判断：拳头/身体/盾牌是各自PhotonNetwork.Instantiate的，ViewID不同但Owner相同
            if (targetView.OwnerActorNr == _shooterActorNumber) return;

            if (_isMine)
            {
                // 四人模式：复活倒计时中的敌人无法受伤、击中也不加分
                HealthBar targetHealth = targetView.GetComponent<HealthBar>();
                if (targetHealth != null && targetHealth.IsRespawning)
                {
                    PlayHitEffectAndSound(other.ClosestPoint(transform.position),
                        playerHitClip != null ? playerHitClip : shieldHitClip);
                    Despawn();
                    return;
                }

                // 四人计分模式（Game4场景）：同队误伤不掉血也不加分
                GameTimeText4P scoreTimer = FindObjectOfType<GameTimeText4P>();
                string myTeam = null;
                string targetTeam = null;
                if (scoreTimer != null)
                {
                    myTeam = GetTeam(PhotonNetwork.LocalPlayer);
                    targetTeam = GetTeam(targetView.Owner);
                    if (myTeam != null && targetTeam != null && myTeam == targetTeam)
                    {
                        PlayHitEffectAndSound(other.ClosestPoint(transform.position),
                            playerHitClip != null ? playerHitClip : shieldHitClip);
                        Despawn();
                        return;
                    }
                }

                // 只有发射者判定伤害，由目标客户端自己扣血（与FistCollision同一套RPC）
                targetView.RPC(nameof(HealthBar.TakeDamage), RpcTarget.All, damage);
                Debug.Log($"拳头子弹击中玩家：{targetView.Owner.NickName}，造成{damage}点伤害");

                // 四人计分模式（Game4场景）：命中对方队伍角色给本队+1分；同队误伤不加分
                if (scoreTimer != null && myTeam != null && targetTeam != null && myTeam != targetTeam)
                {
                    scoreTimer.photonView.RPC(nameof(GameTimeText4P.RPC_AddHit), RpcTarget.All, myTeam);
                }
            }

            // 各客户端本地播放击中特效和音效（双方子弹都在本地模拟，位置近似即可，省去额外RPC）
            PlayHitEffectAndSound(other.ClosestPoint(transform.position),
                playerHitClip != null ? playerHitClip : shieldHitClip);
            Despawn();
        }
        // 击中机器人/僵尸（Enemy）：不能打中自己（机器人出拳瞬间子弹与自身碰撞体重叠）；
        // 命中对方则本地扣血（单机，RobotHealth不走网络），播特效音效后消失。
        // 怪物之间不互相伤害：发射者也是怪物（机器人/僵尸）时，命中其他Enemy直接忽略，拳头穿过不消失
        else if (other.CompareTag("Enemy"))
        {
            if (IsOwnCollider(other)) return;
            if (IsMonsterShooter()) return;

            if (_isMine)
            {
                RobotHealth robotHealth = other.GetComponentInParent<RobotHealth>();
                if (robotHealth != null)
                {
                    robotHealth.TakeDamage(damage);
                    // 在目标头顶弹出伤害飘字（优先挂到它的popupAnchor/血条Canvas下，保证UI文本能渲染）
                    DamagePopupSpawner.Show(damage, robotHealth.transform.position, robotHealth.popupAnchor);
                    Debug.Log($"拳头子弹击中机器人，造成{damage}点伤害");
                }
            }

            PlayHitEffectAndSound(other.ClosestPoint(transform.position),
                playerHitClip != null ? playerHitClip : shieldHitClip);
            Despawn();
        }
        // 击中盾牌：不扣血，计入护盾受击次数，播特效音效后消失。
        // 自己的拳头也会打到自己的盾牌并计入受击次数（子弹生成在拳头处，举盾出拳会立刻命中自己的盾）
        else if (other.CompareTag("Dun"))
        {
            // 双方客户端都在本地模拟子弹，只有发射者客户端登记受击，避免重复计数
            if (_isMine)
            {
                PhotonView shieldView = other.GetComponentInParent<PhotonView>();
                if (shieldView != null)
                {
                    shieldView.RPC(nameof(ShieldDurability.RegisterHit), RpcTarget.All);
                }
            }

            PlayHitEffectAndSound(other.ClosestPoint(transform.position), shieldHitClip);
            Despawn();
        }
    }

    /// <summary>
    /// 命中的碰撞体是否属于发射者自己（单机模式用：子弹生成在发射者拳头处，
    /// 生成瞬间必与自身碰撞体重叠，避免打到自己）。联机模式由OwnerActorNr判断，不走这里
    /// </summary>
    private bool IsOwnCollider(Collider other)
    {
        return _shooterRoot != null && other.transform.IsChildOf(_shooterRoot);
    }

    /// <summary>
    /// 发射者是否为怪物（机器人/僵尸）：怪物之间不互相伤害。
    /// 用组件而不是Tag判断——机器人预制体的根节点不一定打Enemy Tag，但一定挂RobotStateManager
    /// </summary>
    private bool IsMonsterShooter()
    {
        return _shooterRoot != null
            && (_shooterRoot.GetComponentInParent<RobotStateManager>() != null
                || _shooterRoot.GetComponentInParent<ZombieStateManager>() != null);
    }

    private void PlayHitEffectAndSound(Vector3 position, AudioClip clip)
    {
        // 击中特效走EffectPool复用，播放完毕自动回收
        if (hitEffectPrefab != null)
        {
            EffectPool.Instance.Play(hitEffectPrefab, position, Quaternion.identity);
        }

        if (clip != null)
        {
            PlayClipAtPosition(clip, position);
        }
    }

    /// <summary>
    /// 在指定位置播放3D音效。不用AudioSource.PlayClipAtPoint：它的minDistance固定为1米，
    /// 几米外音量就衰减到几乎听不见；这里minDistance可调，保证远程命中也能听到击中反馈
    /// </summary>
    private void PlayClipAtPosition(AudioClip clip, Vector3 position)
    {
        GameObject oneShot = new GameObject("HitSound_OneShot");
        oneShot.transform.position = position;
        AudioSource source = oneShot.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f; // 保持3D方向感，仅扩大不衰减范围
        source.minDistance = hitSoundMinDistance;
        source.Play();
        Destroy(oneShot, clip.length + 0.1f);
    }

    /// <summary>
    /// 读取玩家的队伍自定义属性（四人模式 TeamSelectUI 选座时写入，双人模式为空）
    /// </summary>
    private static string GetTeam(Photon.Realtime.Player player)
    {
        if (player != null && player.CustomProperties.TryGetValue("Team", out object team))
        {
            return team as string;
        }
        return null;
    }

    /// <summary>
    /// 回收入池：停止特效并隐藏，等待下次复用，不销毁
    /// </summary>
    private void Despawn()
    {
        _flying = false;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        gameObject.SetActive(false);
        transform.SetParent(GetPoolRoot(), false);

        Stack<ProjectileFist> pool;
        if (!_freePools.TryGetValue(_prefab, out pool))
        {
            pool = new Stack<ProjectileFist>();
            _freePools[_prefab] = pool;
        }
        pool.Push(this);
    }
}
