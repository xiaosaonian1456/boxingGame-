using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 僵尸状态管理器（单机）：实现僵尸的移动（walk/run追击）、攻击（attack/bite/bite2近战）、
/// 受击（hit）、死亡（death）逻辑，并适配 Avatar_Zombie.controller 的动画播放。
/// 注意：该Animator控制器没有任何参数（Parameters列表为空），不能靠连线切换，
/// 本脚本直接用Animator.CrossFade按状态名播放动画，状态名为：
/// idle / walk / run / attack / bite / bite2 / hit / death。
///
/// 使用方式：
/// 1. 挂在僵尸预制体根节点上（与Animator同物体）；
/// 2. 同物体挂RobotHealth提供血量与死亡事件，僵尸身上的Collider把Tag设为Enemy，
///    玩家的拳头子弹（ProjectileFist）命中后会自动调RobotHealth.TakeDamage扣血；
/// 3. 本脚本订阅RobotHealth：掉血时播放hit受击动画，血量归零时播放death；
///    死亡动画播完后：场景里有ZombieSpawner时回收到对象池复用，没有则销毁；
/// 4. 攻击为近战：进入attackRange后随机播放attack/bite/bite2，攻击动画的咬合关键帧
///    通过Animation Event调用AttackHit()，玩家仍在attackHitRange内才对玩家扣血
///    （单机玩家身上同样挂RobotHealth，本地直接扣血，不走网络）。
/// </summary>
public class ZombieStateManager : MonoBehaviour
{
    public enum ZombieState
    {
        Idle,   // 待机：玩家未进入索敌范围
        Chase,  // 追击：朝玩家移动（远距离run，近距离walk）
        Attack, // 攻击：站定播放attack/bite/bite2
        Hit,    // 受击：播放hit僵直，播完回Idle
        Dead    // 死亡：播放death，播完销毁
    }

    [Header("状态")]
    [SerializeField] private ZombieState currentState = ZombieState.Idle;
    public ZombieState CurrentState => currentState;

    [Header("AI索敌/移动设置")]
    public string playerTag = "Player";   // 索敌目标的Tag
    public float detectRange = 25f;       // 索敌范围（米）：玩家进入此距离开始追击
    public float runRange = 6f;           // 奔跑范围（米）：距离大于此值用run，小于用walk
    public float attackRange = 1.8f;      // 攻击范围（米）：进入此距离站定攻击
    public float walkSpeed = 1f;          // 行走速度（米/秒）
    public float runSpeed = 3.5f;         // 奔跑速度（米/秒）
    public float rotateSpeed = 8f;        // 朝玩家转身速度（越大转得越快）

    [Header("攻击设置")]
    public float attackInterval = 1.5f;   // 两次攻击的最小间隔（秒），从上次攻击动画播完起算
    public float attackDamage = 2f;      // AttackHit命中时对玩家造成的伤害
    public float attackHitRange = 2.2f;   // 咬合判定距离（米）：AttackHit时玩家仍在此距离内才掉血

    [Header("受击设置")]
    public float hitCooldown = 0.8f;      // 受击僵直最小间隔（秒）：防止连续掉血把僵尸一直定在hit动画里
    public float hitStaggerTime = 0.5f;   // 受击僵直时长（秒）：播hit动画这么久后强制回Idle
                                          // （不靠"动画播完"判断：hit剪辑若勾了Loop Time会循环播放，永远等不到播完）

    [Header("群体避让设置")]
    public float avoidRadius = 1.2f;      // 避让半径（米）：追击时此距离内有其他僵尸就会产生绕行力
    public float avoidStrength = 1.5f;    // 避让力度：相对追击方向的权重，越大绕得越开

    [Header("动画设置")]
    public Animator animator;             // 僵尸的Animator（不填则自动取本物体上的）
    public float crossFadeTime = 0.15f;   // 动画切换的淡入时长（秒）
    public float deathDestroyDelay = 1f;  // 死亡动画播完后额外延迟多少秒销毁僵尸

    // Avatar_Zombie.controller中的动画状态名（无参数，直接按名播放）
    private const string AnimIdle = "idle";
    private const string AnimWalk = "walk";
    private const string AnimRun = "run";
    private const string AnimHit = "hit";
    private const string AnimDeath = "death";
    private static readonly string[] AttackAnims = { "attack", "bite", "bite2" }; // 攻击动作随机池

    private RobotHealth _health;      // 同物体上的血条组件（没有则始终视为存活、不受击）
    private Transform _target;        // 追击目标（Tag为Player的物体）
    private int _currentAnimHash;     // 当前已请求播放的动画状态（避免每帧重复CrossFade同一状态）
    private float _nextAttackTime;    // 下一次允许发起攻击的Time.time时间戳
    private float _nextHitTime;       // 下一次允许进入受击僵直的Time.time时间戳
    private float _lastHealth = -1f;  // 上一帧血量（用于从OnHealthChanged判断"掉血"而非回血）
    private bool _animStarted;        // 已观测到攻击动画真正开始播放（渡过CrossFade淡入窗口）
    private string _actionAnim;       // 正在等待播完的攻击动画名（attack/bite/bite2）
    private float _hitEndTime;        // 受击僵直结束的Time.time时间戳
    private float _fixedY;            // 出生时的地面高度：受击动画可能带Y轴位移把模型顶上天，每帧锁回这个高度

    // 场景中所有僵尸的注册表（群体避让用）：静态列表随OnEnable/OnDisable维护，
    // 比每帧FindObjectsOfType/OverlapSphere便宜得多
    private static readonly List<ZombieStateManager> _registry = new List<ZombieStateManager>();

    private void OnEnable()
    {
        _registry.Add(this);
    }

    private void OnDisable()
    {
        _registry.Remove(this);
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        // 禁用root motion：移动完全由脚本控制，防止动画剪辑自带的位移推动模型（hit剪辑的Y位移会把模型顶起来）
        animator.applyRootMotion = false;
        _fixedY = transform.position.y;
        // 刚体设为运动学：僵尸的移动/避让全由脚本驱动，僵尸之间不发生物理推挤——
        // 否则追击中的僵尸会把站定攻击的僵尸撞开。子弹自带运动学刚体，触发器命中不受影响
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        _health = GetComponent<RobotHealth>();
        if (_health != null)
        {
            _health.OnDeath += HandleDeath;
            _health.OnHealthChanged += HandleHealthChanged;
            _lastHealth = _health.maxHealth;
        }
        PlayAnim(AnimIdle);
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDeath -= HandleDeath;
            _health.OnHealthChanged -= HandleHealthChanged;
        }
    }

    /// <summary>
    /// 锁定出生高度：即使动画剪辑带Y轴位移，也不让模型被顶起或沉入地下。
    /// 放在LateUpdate：动画在每帧Update之后才写入骨骼/根节点位移，Update里锁会被当帧动画覆盖。
    /// 死亡后也要继续锁：death剪辑的倒地动作带根节点下沉位移，会把尸体拖进地下
    /// </summary>
    private void LateUpdate()
    {
        Vector3 pos = transform.position;
        if (pos.y != _fixedY)
        {
            pos.y = _fixedY;
            transform.position = pos;
        }
    }

    private void Update()
    {
        // 死亡后停止一切AI行为（死亡动画由HandleDeath驱动）
        if (currentState == ZombieState.Dead || (_health != null && _health.IsDead))
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
                EnterIdle();
                return;
            }
        }

        float distance = FlatDistance(transform.position, _target.position);

        switch (currentState)
        {
            case ZombieState.Idle:
                // 玩家进入索敌范围开始追击，直接进攻击范围则开打
                if (distance <= attackRange)
                {
                    EnterAttack();
                }
                else if (distance <= detectRange)
                {
                    EnterChase();
                }
                break;

            case ZombieState.Chase:
                if (distance <= attackRange)
                {
                    EnterAttack();
                    break;
                }
                FaceTarget();
                MoveTowardTarget(distance);
                break;

            case ZombieState.Attack:
                UpdateAttack(distance);
                break;

            case ZombieState.Hit:
                UpdateHit();
                break;
        }
    }

    /// <summary>
    /// 攻击状态：站定面向玩家，攻击间隔完毕且在攻击距离内时随机播放一套
    /// attack/bite/bite2；等该动画真正开始并播完后切回Idle，并起算attackInterval。
    /// 玩家跑出范围则回Chase继续追击（一套攻击动画即使玩家跑开也会打完）。
    /// </summary>
    private void UpdateAttack(float distance)
    {
        FaceTarget();

        // 正在播放攻击动画：等它播完（先观测到"开始"再判断"结束"，
        // 否则CrossFade淡入窗口内Animator还没切到攻击状态，会误判为已播完）
        if (!string.IsNullOrEmpty(_actionAnim))
        {
            if (IsAnimPlaying(_actionAnim))
            {
                _animStarted = true;
                return;
            }
            if (!_animStarted)
            {
                return; // 淡入窗口内，还没切到攻击状态，继续等
            }
            // 攻击动画播完：切回Idle（控制器无连线，不手动切回会停在攻击最后一帧僵直），
            // 并从这一刻起算攻击间隔
            _actionAnim = null;
            _animStarted = false;
            _nextAttackTime = Time.time + attackInterval;
            PlayAnim(AnimIdle);
        }

        // 玩家跑出攻击范围：回追击（下一帧按距离自动选walk/run）
        if (distance > attackRange)
        {
            EnterChase();
            return;
        }

        // 间隔已满：随机挑一套攻击动作播放，咬合伤害由动画事件AttackHit()判定
        if (Time.time >= _nextAttackTime)
        {
            _actionAnim = AttackAnims[Random.Range(0, AttackAnims.Length)];
            _animStarted = false;
            PlayAnim(_actionAnim);
        }
    }

    /// <summary>
    /// 受击状态：僵直hitStaggerTime秒后回Idle重新决策。
    /// 用计时器而不是"等动画播完"：hit剪辑若勾了Loop Time会循环播放，永远等不到播完
    /// </summary>
    private void UpdateHit()
    {
        if (Time.time >= _hitEndTime)
        {
            EnterIdle();
        }
    }

    /// <summary>
    /// 动画事件回调：attack/bite/bite2的咬合关键帧调用此方法，
    /// 玩家仍在咬合判定距离内则对玩家扣血。
    /// 注意：Animation Event只能调用挂在Animator同物体上的组件的公开方法，
    /// 本脚本必须和Animator挂在同一个GameObject上。
    /// </summary>
    public void AttackHit()
    {
        if (currentState != ZombieState.Attack || string.IsNullOrEmpty(_actionAnim))
        {
            return;
        }
        if (_target == null)
        {
            return;
        }
        if (FlatDistance(transform.position, _target.position) > attackHitRange)
        {
            return; // 玩家已跑出咬合距离，打空
        }
        // 单机玩家身上挂RobotHealth做本地血条，直接本地扣血（与拳头子弹打玩家的单机路径一致）
        RobotHealth playerHealth = _target.GetComponentInParent<RobotHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
    }

    /// <summary>
    /// 血量变化回调：掉血（而非回血）且未死亡时播放受击动画。
    /// 有hitCooldown保护，避免高频掉血把僵尸永久定在僵直里；攻击动画播放中不受击，保证出手不被打断。
    /// </summary>
    private void HandleHealthChanged(float current, float max)
    {
        bool damaged = _lastHealth >= 0f && current < _lastHealth;
        _lastHealth = current;
        if (!damaged || currentState == ZombieState.Dead)
        {
            return;
        }
        if (currentState == ZombieState.Attack && !string.IsNullOrEmpty(_actionAnim))
        {
            return; // 攻击动作播放中，不吃受击僵直（伤害照扣，只是不播hit）
        }
        if (Time.time < _nextHitTime)
        {
            return;
        }
        _nextHitTime = Time.time + hitCooldown;
        _hitEndTime = Time.time + hitStaggerTime;
        currentState = ZombieState.Hit;
        _actionAnim = null;
        PlayAnim(AnimHit);
    }

    /// <summary>
    /// 血量归零回调：打断当前一切行为，立即播放死亡动画，播完后回收入池（或销毁）
    /// </summary>
    private void HandleDeath()
    {
        currentState = ZombieState.Dead;
        _actionAnim = null;
        // 上报击杀数：只有血量归零的真正死亡才计数（收尾爆炸清场直接回收，不会走到这里）
        if (ZombieKillCounter.Instance != null)
        {
            ZombieKillCounter.Instance.AddKill();
        }
        // 关掉碰撞体：尸体不再挡子弹、不再触发命中（复用时由ResetForReuse重新打开）
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        if (animator == null)
        {
            Destroy(gameObject, deathDestroyDelay);
            return;
        }
        PlayAnim(AnimDeath);
        StartCoroutine(DestroyAfterDeathAnim());
    }

    /// <summary>
    /// 等死亡动画播放完毕后回收僵尸：场景里有ZombieSpawner时回收到对象池等待复用
    /// （避免频繁Instantiate/Destroy的GC开销），没有生成器时（如场景里手摆的）直接销毁
    /// </summary>
    private System.Collections.IEnumerator DestroyAfterDeathAnim()
    {
        // 等一帧让Animator真正切换到死亡状态，避免读到上一个状态的信息
        yield return null;

        while (true)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(AnimDeath) && info.normalizedTime >= 1f)
            {
                break;
            }
            yield return null;
        }

        if (deathDestroyDelay > 0f)
        {
            yield return new WaitForSeconds(deathDestroyDelay);
        }

        if (ZombieSpawner.Instance != null)
        {
            ZombieSpawner.Instance.Recycle(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 对象池复用前的状态重置（由ZombieSpawner在激活实例后调用）：
    /// 复活满血、打开碰撞体、回到Idle状态并刷新出生高度锁。
    /// 调用前必须先把transform挪到新的生成位置——出生高度锁从这里读取
    /// </summary>
    public void ResetForReuse()
    {
        StopAllCoroutines(); // 保险：清掉可能残留的死亡协程
        currentState = ZombieState.Idle;
        _target = null;
        _actionAnim = null;
        _animStarted = false;
        _currentAnimHash = 0;
        _nextAttackTime = 0f;
        _nextHitTime = 0f;
        _hitEndTime = 0f;
        _fixedY = transform.position.y; // 出生高度锁更新为新生成点的高度
        // 重新打开死亡时关掉的碰撞体
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
        }
        if (_health != null)
        {
            _health.ResetHealth();
            _lastHealth = _health.maxHealth;
        }
        PlayAnim(AnimIdle);
    }

    private void EnterIdle()
    {
        currentState = ZombieState.Idle;
        PlayAnim(AnimIdle);
    }

    private void EnterChase()
    {
        currentState = ZombieState.Chase;
        _actionAnim = null;
        _animStarted = false;
    }

    private void EnterAttack()
    {
        currentState = ZombieState.Attack;
        _actionAnim = null;
        _animStarted = false;
        PlayAnim(AnimIdle); // 站定，等UpdateAttack到点出手
    }

    /// <summary>
    /// 寻找场景中Tag为Player的物体作为追击目标
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
    /// 朝玩家位置移动（仅在水平面上，不改变高度），并按距离切换walk/run动画。
    /// 叠加群体避让力：追击路上被其他僵尸挡住时自动绕开
    /// </summary>
    private void MoveTowardTarget(float distance)
    {
        bool running = distance > runRange;
        PlayAnim(running ? AnimRun : AnimWalk);

        Vector3 direction = _target.position - transform.position;
        direction.y = 0f;
        direction.Normalize();

        // 分离转向：追击方向 + 避让力，被同伴挡住时自然从侧面绕过去
        direction += ComputeSeparation() * avoidStrength;

        float speed = running ? runSpeed : walkSpeed;
        transform.position += direction.normalized * speed * Time.deltaTime;
    }

    /// <summary>
    /// 计算群体避让力（separation steering）：对avoidRadius内的每只活僵尸，
    /// 叠加一个方向远离它、距离越近强度越大的向量；再带一点侧向偏置，
    /// 避免两只僵尸迎头互顶卡死（径向排斥对称时会僵住，侧向分量打破对称）
    /// </summary>
    private Vector3 ComputeSeparation()
    {
        Vector3 separation = Vector3.zero;
        for (int i = 0; i < _registry.Count; i++)
        {
            ZombieStateManager other = _registry[i];
            // 跳过自己和尸体（尸体不挡路）
            if (other == this || other.currentState == ZombieState.Dead)
            {
                continue;
            }
            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist < 0.001f || dist >= avoidRadius)
            {
                continue;
            }
            float weight = 1f - dist / avoidRadius; // 越近越使劲躲
            separation += away.normalized * weight;
            separation += Vector3.Cross(Vector3.up, away.normalized) * (weight * 0.4f); // 侧向绕行分量
        }
        return separation;
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

    /// <summary>
    /// 按状态名播放动画（控制器无参数，只能直接播状态）；同一状态不重复CrossFade
    /// </summary>
    private void PlayAnim(string stateName)
    {
        int hash = Animator.StringToHash(stateName);
        if (_currentAnimHash == hash)
        {
            return;
        }
        _currentAnimHash = hash;
        animator.CrossFade(stateName, crossFadeTime);
    }

    private bool IsAnimPlaying(string stateName)
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName(stateName) && info.normalizedTime < 1f;
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
}
