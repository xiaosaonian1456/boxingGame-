using UnityEngine;

/// <summary>
/// 机器人状态管理器（单机）：管理机器人的AI行为状态（Idle/Chase/Attack）。
/// 挂在带Animator的机器人物体上。自动寻找场景中Tag为Player的物体：
/// 距离超过attackRange则追踪（播放移动动画），进入范围则进入攻击状态
/// （播放Idle动画，每隔attackInterval秒朝玩家播放TwiceAttack攻击动画）。
/// 攻击中玩家跑出范围时，等待攻击动画播完回到Idle后再继续追踪。
/// 血量归零（RobotHealth.OnDeath）时立即播放死亡动画并停止一切AI行为，
/// 死亡动画播放完毕后销毁机器人。
/// 攻击动画（TwiceAttack）的关键帧通过 Animation Event 调用 AttackHit() 发射特效子弹，
/// 子弹由ProjectileFist对象池复用，与玩家的攻击方式一致。
/// </summary>
public class RobotStateManager : MonoBehaviour
{
    public enum RobotState
    {
        Idle,   // 待机：决策下一步（追踪或攻击）
        Chase,  // 追踪：朝玩家移动
        Attack  // 攻击：站桩输出，每隔attackInterval秒打一套TwiceAttack
    }

    [Header("状态")]
    [SerializeField] private RobotState currentState = RobotState.Idle;
    public RobotState CurrentState => currentState;

    [Header("AI索敌/移动设置")]
    public string playerTag = "Player";   // 索敌目标的Tag
    public float attackRange = 10f;       // 索敌/攻击范围（米）：玩家进入此距离机器人站定攻击，超出则追踪
    public float moveSpeed = 2f;          // 追踪移动速度（米/秒）
    public float rotateSpeed = 10f;       // 朝玩家转身速度（越大转得越快）
    public float attackInterval = 1f;     // 攻击状态下每隔多少秒打一套TwiceAttack

    [Header("动画设置")]
    public Animator animator;                 // 机器人的Animator（不填则自动取本物体上的）
    public string attackStateName = "TwiceAttack"; // 攻击动画状态名（用于判断攻击动画是否播完）
    public string deathStateName = "Death";   // 死亡动画状态名（用于判断死亡动画是否播完）
    public float deathDestroyDelay = 0f;      // 死亡动画播完后额外延迟多少秒销毁机器人

    [Header("攻击设置")]
    public Transform firePoint;               // 特效发射点（不填则用机器人自身位置+前方偏移）
    public float fireForwardOffset = 0.5f;    // 无firePoint时发射点的前向偏移（米）
    public float fireHeightOffset = 1.2f;     // 无firePoint时发射点的高度偏移（米）
    public float aimHeightOffset = 0.9f;      // 瞄准点高度偏移（米）：瞄准玩家肚子附近，避免打高
    public float attackCooldown = 0.1f;       // 两次发射的最小间隔（防同一帧事件重复触发）

    [Header("发射拳头设置")]
    public GameObject projectileFistPrefab;   // 兜底子弹预制体（FistSkinManager未配置时使用）
    [Tooltip("子弹飞行距离倍率（相对ProjectileFist.maxDistance）")]
    public float projectileDistanceMultiplier = 1f;

    private float _lastFireTime = -999f;
    private RobotHealth _health;   // 同物体上的血条组件（没有则始终视为存活）
    private Transform _target;     // 追踪目标（Tag为Player的物体）
    private float _nextAttackTime; // 下一次允许发起攻击的Time.time时间戳（=上次攻击动画播完时刻+attackInterval）
    private bool _attackFired;     // 本次攻击已发出（Attack已置true），等待动画播完后收回false
    private bool _attackAnimStarted; // 已观测到TwiceAttack真正开始播放（渡过切入的淡入窗口）
    private float _attackFiredTime; // 本次攻击发出的时间（看门狗用）
    private float _debugTimer;     // 临时诊断用计时器（用完删除）

    // Animator参数（在Animator窗口的Parameters面板里创建同名参数，切换过渡由连线完成）
    private static readonly int AnimParamMove = Animator.StringToHash("Move");     // Bool：true=追踪移动
    private static readonly int AnimParamAttack = Animator.StringToHash("Attack"); // Bool：true=播放TwiceAttack，打完收回false
    private static readonly int AnimParamDie = Animator.StringToHash("Die");       // Trigger：死亡

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        _health = GetComponent<RobotHealth>();
        if (_health != null)
        {
            _health.OnDeath += HandleDeath;
        }

        // ===== 临时诊断：打印运行时Animator实际挂的controller和参数清单（用完删除）=====
        if (animator != null)
        {
            RuntimeAnimatorController ctrl = animator.runtimeAnimatorController;
            Debug.Log($"[Robot诊断] Awake: Animator物体={animator.gameObject.name} controller={(ctrl != null ? ctrl.name : "空")} controllerID={(ctrl != null ? ctrl.GetInstanceID() : 0)}");
            for (int i = 0; i < animator.parameterCount; i++)
            {
                AnimatorControllerParameter p = animator.GetParameter(i);
                Debug.Log($"[Robot诊断] 参数{i}: 名={p.name} 类型={p.type}");
            }
        }
        // ===== 诊断结束 =====
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDeath -= HandleDeath;
        }
    }

    /// <summary>
    /// 血量归零回调：打断当前一切行为，立即播放死亡动画，动画播完后销毁机器人
    /// </summary>
    private void HandleDeath()
    {
        if (animator == null)
        {
            Destroy(gameObject);
            return;
        }
        animator.SetTrigger(AnimParamDie); // Any State → Death 连线切换
        StartCoroutine(DestroyAfterDeathAnim());
    }

    /// <summary>
    /// 等死亡动画播放完毕后销毁机器人（含子物体上的血条UI等）
    /// </summary>
    private System.Collections.IEnumerator DestroyAfterDeathAnim()
    {
        // 等一帧让Animator真正切换到死亡状态，避免读到上一个状态的信息
        yield return null;

        while (true)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(deathStateName) && info.normalizedTime >= 1f)
            {
                break;
            }
            yield return null;
        }

        if (deathDestroyDelay > 0f)
        {
            yield return new WaitForSeconds(deathDestroyDelay);
        }
        Destroy(gameObject);
    }

    private void Update()
    {
        // 死亡后停止一切AI行为
        if (_health != null && _health.IsDead)
        {
            return;
        }
        if (animator == null)
        {
            return;
        }
        // 目标丢失（或尚未生成）时持续寻找
        if (_target == null)
        {
            FindTarget();
            if (_target == null)
            {
                return;
            }
        }

        float distance = FlatDistance(transform.position, _target.position);

        // 每帧按AI状态校准Move参数：运行初期外部逻辑（XR/网络初始化）会重置Animator，
        // 参数被打回默认值后只设一次就永久丢失，这里自愈
        bool wantMove = currentState == RobotState.Chase;
        if (animator.GetBool(AnimParamMove) != wantMove)
        {
            animator.SetBool(AnimParamMove, wantMove);
        }

        // ===== 临时诊断日志（定位动画不切换问题，用完删除）=====
        _debugTimer += Time.deltaTime;
        if (_debugTimer >= 1f)
        {
            _debugTimer = 0f;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            string animState = info.IsName("Idle") ? "Idle"
                : info.IsName("Move") ? "Move"
                : info.IsName(attackStateName) ? "TwiceAttack"
                : info.IsName(deathStateName) ? "Death" : "未知:" + info.shortNameHash;
            Debug.Log($"[Robot诊断] AI状态={currentState} 动画状态={animState} nT={info.normalizedTime:F2} " +
                      $"Move参数={animator.GetBool(AnimParamMove)} Attack参数={animator.GetBool(AnimParamAttack)} " +
                      $"距玩家={distance:F1}m");
        }
        // ===== 诊断日志结束 =====

        switch (currentState)
        {
            case RobotState.Idle:
                // 待机只是一个决策中转：超出范围去追，进入范围开打
                if (distance > attackRange)
                {
                    EnterChase();
                }
                else
                {
                    EnterAttack();
                }
                break;

            case RobotState.Chase:
                if (distance <= attackRange)
                {
                    EnterAttack();
                    break;
                }
                FaceTarget();
                MoveTowardTarget();
                break;

            case RobotState.Attack:
                UpdateAttack(distance);
                break;
        }
    }

    /// <summary>
    /// 攻击状态：朝玩家站桩。
    /// 攻击间隔完毕且玩家在攻击距离内时把Attack置true（连线切入TwiceAttack）；
    /// 观测到TwiceAttack真正开始播放后，等它播完（两次出拳都发射）才把Attack置false
    /// （必须先"观测到开始"再判断结束：切入动画的淡入窗口内Animator还没切到
    /// TwiceAttack状态，直接判断会把Attack布尔过早收回导致攻击被跳过）；
    /// 玩家跑出范围则回Idle（下一帧自动转入追踪）
    /// </summary>
    private void UpdateAttack(float distance)
    {
        FaceTarget();

        bool attackPlaying = IsAttackAnimPlaying();
        if (attackPlaying)
        {
            _attackAnimStarted = true; // 已观测到TwiceAttack在播放
        }

        // 攻击动画从开始到播完：收回Attack布尔，连线切回Idle，
        // 并从播完这一刻起算attackInterval秒后才允许下一套（保证攻击间隔恒定）
        if (_attackAnimStarted && !attackPlaying)
        {
            _attackAnimStarted = false;
            _attackFired = false;
            animator.SetBool(AnimParamAttack, false);
            _nextAttackTime = Time.time + attackInterval;
        }

        // 看门狗：发射后迟迟没观测到攻击动画播放（如Animator被外部重置吞掉了Attack=true），
        // 复位让下一轮重新发起，避免攻击流程卡死
        if (_attackFired && !_attackAnimStarted && Time.time - _attackFiredTime > 0.5f)
        {
            _attackFired = false;
            animator.SetBool(AnimParamAttack, false);
        }

        // 攻击动画播放中（或已发射、正在切入的淡入窗口内）：等待完成，
        // 即使玩家已跑出范围也要打完这套拳
        if (attackPlaying || _attackFired)
        {
            return;
        }

        // 玩家跑出攻击范围：回到Idle，由Idle状态转入追踪
        if (distance > attackRange)
        {
            EnterIdle();
            return;
        }

        // 距离上次攻击播完已满attackInterval秒：发起攻击（开局第一次_nextAttackTime为0，会立即出手）
        if (Time.time >= _nextAttackTime)
        {
            _attackFired = true;
            _attackFiredTime = Time.time;
            animator.SetBool(AnimParamAttack, true); // 连线切入TwiceAttack，关键帧事件回调AttackHit()完成实际发射
        }
    }

    /// <summary>
    /// 进入攻击状态：立即先打一套（进入范围即出手），之后按attackInterval循环
    /// </summary>
    public void StartAttack()
    {
        EnterAttack();
    }

    private void EnterIdle()
    {
        currentState = RobotState.Idle;
        animator.SetBool(AnimParamMove, false);
    }

    private void EnterChase()
    {
        currentState = RobotState.Chase;
        _attackFired = false;
        _attackAnimStarted = false;
        animator.SetBool(AnimParamAttack, false); // 保险：追踪状态下绝不允许Attack残留true，否则Any State连线会在跑动中触发攻击
        animator.SetBool(AnimParamMove, true); // Idle → Move 连线切换
        Debug.Log($"[Robot诊断] EnterChase: 已设Move=true，立即读回={animator.GetBool(AnimParamMove)}"); // 临时诊断（用完删除）
    }

    private void EnterAttack()
    {
        currentState = RobotState.Attack;
        _attackFired = false;
        _attackAnimStarted = false;
        animator.SetBool(AnimParamMove, false); // Move → Idle 连线切换，站定输出
        // 注意：不再重置攻击节奏，是否能立即出手由_nextAttackTime决定（玩家进出边界不会白送攻击）
    }

    /// <summary>
    /// 寻找场景中Tag为Player的物体作为追踪目标
    /// </summary>
    private void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            _target = player.transform;
        }
    }

    /// <summary>
    /// 朝玩家位置移动（仅在水平面上，不改变高度）
    /// </summary>
    private void MoveTowardTarget()
    {
        Vector3 direction = _target.position - transform.position;
        direction.y = 0f;
        transform.position += direction.normalized * moveSpeed * Time.deltaTime;
    }

    /// <summary>
    /// 平滑转身面向玩家（仅水平旋转）
    /// </summary>
    private void FaceTarget()
    {
        Vector3 direction = _target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotateSpeed * Time.deltaTime);
    }

    private bool IsAttackAnimPlaying()
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName(attackStateName) && info.normalizedTime < 1f;
    }

    /// <summary>
    /// 水平面距离（忽略高度差）
    /// </summary>
    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>
    /// 动画事件回调：攻击动画的出拳关键帧调用此方法，朝玩家方向发射随机特效。
    /// 注意：Animation Event只能调用挂在本GameObject上的组件的公开方法，
    /// 本脚本必须和Animator挂在同一个物体上（或Animator的子/父物体上并保证事件能找到）。
    /// </summary>
    public void AttackHit()
    {
        if (currentState != RobotState.Attack)
        {
            return;
        }
        // 死亡后不再出拳
        if (_health != null && _health.IsDead)
        {
            return;
        }
        // 冷却保护：防止事件重复触发导致一拳发多颗
        if (Time.time - _lastFireTime < attackCooldown)
        {
            return;
        }

        // 每次发射从FistSkinManager特效列表中随机挑一种
        GameObject prefab = GetRandomPrefab();
        if (prefab == null)
        {
            Debug.LogError("[RobotStateManager] 未配置子弹预制体！请配置 FistSkinManager 的特效列表或本组件的 projectileFistPrefab 兜底。");
            return;
        }

        _lastFireTime = Time.time;

        // 发射位置：优先firePoint，否则用机器人位置+高度+前向偏移
        Vector3 position = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up * fireHeightOffset + transform.forward * fireForwardOffset;

        // 发射方向：瞄准玩家胸口位置；目标丢失时退化为机器人正前方
        Vector3 direction = transform.forward;
        if (_target != null)
        {
            Vector3 aimPoint = _target.position + Vector3.up * aimHeightOffset;
            direction = (aimPoint - position).normalized;
        }

        // 朝玩家方向发射（与玩家一致走ProjectileFist对象池）；传入自身transform作为发射者根节点，避免子弹打到自己
        ProjectileFist.Spawn(prefab, position, direction, true, -1, projectileDistanceMultiplier, transform);
        Debug.Log("机器人出拳");
    }

    /// <summary>
    /// 从FistSkinManager的特效列表中随机挑一个预制体，未配置时用兜底预制体
    /// </summary>
    private GameObject GetRandomPrefab()
    {
        if (FistSkinManager.Instance != null
            && FistSkinManager.Instance.projectileVariants != null
            && FistSkinManager.Instance.projectileVariants.Length > 0)
        {
            int index = Random.Range(0, FistSkinManager.Instance.projectileVariants.Length);
            return FistSkinManager.Instance.GetPrefab(index);
        }
        return projectileFistPrefab;
    }
}
