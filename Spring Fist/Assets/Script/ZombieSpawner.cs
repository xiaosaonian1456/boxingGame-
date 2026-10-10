using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 僵尸生成器（单机，带对象池）：挂在场景任意物体上。
/// 每隔spawnInterval秒，在玩家周围minDistance~maxDistance米的圆环上随机位置
/// 生成一只僵尸，种类从zombiePrefabs数组中随机抽取。
/// 对象池：死亡的僵尸不销毁，由ZombieStateManager死亡动画播完后调Recycle回收，
/// 下次生成同种类时优先复用池里的空闲实例，避免频繁Instantiate/Destroy的GC开销。
/// 场上存活僵尸达到maxAlive时暂停生成，有僵尸死亡空出名额后自动恢复。
/// 单机玩家死亡进入复活倒计时期间（RobotHealth把SpawningPaused置为true）暂停生成，复活后恢复。
/// 生成高度：从生成点上方30米向下打射线找地面，打不到（无地面碰撞体）则沿用玩家高度。
/// 倒计时进入最后stopSpawnRemainingTime秒时：停止生成，并把场上剩余僵尸全部引爆清场
/// （每只僵尸位置播放爆炸特效后回收入池，同时播放一次爆炸音效）。
/// 倒计时结束游戏冻结（Time.timeScale=0）后自动停止生成。
/// </summary>
public class ZombieSpawner : MonoBehaviour
{
    /// 全局访问点：僵尸死亡动画播完后通过它回收入池
    public static ZombieSpawner Instance { get; private set; }

    /// <summary>
    /// 是否暂停生成僵尸（单机玩家复活倒计时期间由RobotHealth置为true，复活后恢复false）。
    /// 只暂停新僵尸生成，不影响倒计时收尾清场
    /// </summary>
    public bool SpawningPaused { get; set; }

    [Header("僵尸预制体（拖入5种）")]
    public GameObject[] zombiePrefabs;

    [Header("索敌设置")]
    public string playerTag = "Player";   // 玩家Tag，用于定位生成中心

    [Header("生成范围（以玩家为中心的圆环）")]
    public float minDistance = 15f;       // 最小生成距离（米）
    public float maxDistance = 20f;       // 最大生成距离（米）

    [Header("生成节奏")]
    public int initialCount = 5;          // 开局立即生成的数量
    public float spawnInterval = 3f;      // 之后每隔多少秒生成一只
    public int maxAlive = 20;             // 场上最多同时存活多少只（达到后暂停生成）

    [Header("倒计时收尾清场")]
    public float stopSpawnRemainingTime = 5f; // 倒计时剩余多少秒时停止生成并引爆清场
    public GameObject explosionEffectPrefab;  // 爆炸特效预制体（在每只僵尸位置实例化）
    public float explosionEffectLifetime = 3f; // 爆炸特效实例自动销毁时间（秒）
    public AudioClip explosionSound;          // 爆炸音效（清场时在玩家位置播放一次）

    private Transform _player;            // 玩家位置（生成圆环的中心）
    private readonly List<GameObject> _alive = new List<GameObject>(); // 存活僵尸（用于计数）
    private float _nextSpawnTime;         // 下一次生成的时间戳
    private bool _cleared;                // 收尾清场是否已执行（只引爆一次）

    // 对象池：每种预制体对应一个空闲实例栈
    private readonly Dictionary<GameObject, Stack<GameObject>> _pool = new Dictionary<GameObject, Stack<GameObject>>();
    // 实例→来源预制体映射：回收时知道该回哪个池（含池化复用的实例，映射只增不减）
    private readonly Dictionary<GameObject, GameObject> _prefabOf = new Dictionary<GameObject, GameObject>();

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
        FindPlayer();
        for (int i = 0; i < initialCount; i++)
        {
            TrySpawn();
        }
        _nextSpawnTime = Time.time + spawnInterval;
    }

    private void Update()
    {
        // 清理异常销毁的僵尸（正常死亡走回收流程时已从列表移除）
        _alive.RemoveAll(zombie => zombie == null);

        if (_player == null)
        {
            FindPlayer(); // 玩家可能延迟生成，持续寻找
            return;
        }

        // 倒计时进入最后几秒：停止生成，并把场上剩余僵尸全部引爆清场（只执行一次）。
        // 场景里没有CountdownTimer时（timer==null）不启用清场逻辑，行为与原来一致
        CountdownTimer timer = CountdownTimer.Instance;
        if (timer != null && timer.RemainingTime <= stopSpawnRemainingTime)
        {
            if (!_cleared)
            {
                _cleared = true;
                ExplodeAllAlive();
            }
            return;
        }

        if (Time.time < _nextSpawnTime || _alive.Count >= maxAlive)
        {
            return;
        }

        // 单机玩家复活倒计时期间暂停生成，复活后由RobotHealth恢复
        if (SpawningPaused)
        {
            return;
        }
        TrySpawn();
        _nextSpawnTime = Time.time + spawnInterval;
    }

    /// <summary>
    /// 收尾清场：在每只存活僵尸的位置播放爆炸特效，然后立即回收入池（或销毁）。
    /// 不走走RobotHealth的死亡流程——不播death动画、不计击杀，直接隐藏消失；
    /// 已回收进池的僵尸下次被复用时仍由ResetForReuse完整重置状态。
    /// 爆炸音效只在玩家位置播放一次，避免多只僵尸同时爆炸音量叠加爆音。
    /// </summary>
    private void ExplodeAllAlive()
    {
        if (_alive.Count == 0)
        {
            return;
        }
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, _player.position);
        }
        // 先拷贝快照再遍历：Recycle会修改_alive列表
        GameObject[] snapshot = _alive.ToArray();
        foreach (GameObject zombie in snapshot)
        {
            if (zombie == null)
            {
                continue;
            }
            if (explosionEffectPrefab != null)
            {
                // 特效抬到僵尸胸口高度，避免贴地被地面遮住一半
                GameObject vfx = Instantiate(explosionEffectPrefab,
                    zombie.transform.position + Vector3.up * 1f, Quaternion.identity);
                Destroy(vfx, explosionEffectLifetime);
            }
            ZombieStateManager zsm = zombie.GetComponent<ZombieStateManager>();
            if (zsm != null)
            {
                Recycle(zsm); // 回收入池（隐藏）；正在播放的死亡协程随SetActive(false)停止
            }
            else
            {
                _alive.Remove(zombie);
                Destroy(zombie);
            }
        }
    }

    /// <summary>
    /// 在玩家周围的圆环上随机选点，生成一只随机种类的僵尸，面向玩家。
    /// 优先从对象池取该种类的空闲实例复用，池空才Instantiate新实例
    /// </summary>
    private void TrySpawn()
    {
        if (zombiePrefabs == null || zombiePrefabs.Length == 0 || _player == null)
        {
            return;
        }

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minDistance, maxDistance);
        Vector3 position = _player.position
            + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        position.y = FindGroundY(position);

        GameObject prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];
        // 出生即面向玩家，避免转身动画衔接生硬
        Vector3 lookDir = _player.position - position;
        lookDir.y = 0f;
        Quaternion rotation = lookDir.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(lookDir)
            : Quaternion.identity;

        GameObject zombie = null;
        Stack<GameObject> pool;
        if (_pool.TryGetValue(prefab, out pool))
        {
            // 跳过被外部销毁的无效实例
            while (pool.Count > 0 && zombie == null)
            {
                zombie = pool.Pop();
            }
        }

        if (zombie != null)
        {
            // 复用：先挪到生成点再激活，最后重置状态（顺序不能乱：
            // ResetForReuse要从transform.position读取新的出生高度锁）
            zombie.transform.SetPositionAndRotation(position, rotation);
            zombie.SetActive(true);
            ZombieStateManager zsm = zombie.GetComponent<ZombieStateManager>();
            if (zsm != null)
            {
                zsm.ResetForReuse();
            }
        }
        else
        {
            zombie = Instantiate(prefab, position, rotation);
            _prefabOf[zombie] = prefab;
        }
        _alive.Add(zombie);
    }

    /// <summary>
    /// 回收死亡僵尸（由ZombieStateManager在死亡动画播完后调用）：
    /// 本生成器产出的僵尸隐藏入池等待复用；非本生成器产出的（如场景里手摆的）直接销毁
    /// </summary>
    public void Recycle(ZombieStateManager zombie)
    {
        if (zombie == null)
        {
            return;
        }
        GameObject obj = zombie.gameObject;
        GameObject prefab;
        if (!_prefabOf.TryGetValue(obj, out prefab))
        {
            Destroy(obj);
            return;
        }
        _alive.Remove(obj);
        obj.SetActive(false);

        Stack<GameObject> pool;
        if (!_pool.TryGetValue(prefab, out pool))
        {
            pool = new Stack<GameObject>();
            _pool[prefab] = pool;
        }
        pool.Push(obj);
    }

    /// <summary>
    /// 从生成点上方30米向下打射线找地面高度；打不到就沿用玩家的脚底高度
    /// </summary>
    private float FindGroundY(Vector3 position)
    {
        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out hit, 60f))
        {
            return hit.point.y;
        }
        return _player.position.y;
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            _player = player.transform;
        }
    }
}
