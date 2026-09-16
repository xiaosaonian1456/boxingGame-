using UnityEngine;
using Photon.Pun;
using Unity.XR.CoreUtils;

public class PhysicsSpringFist : MonoBehaviourPun, IPunObservable
{
    [Header("出拳设置")]
    public KeyCode fireKey = KeyCode.F;
    public float punchCooldown = 0.5f;   // 两次出拳的最小间隔
    [Header("发射拳头设置")]
    public GameObject projectileFistPrefab; // 发射的拳头子弹预制体（挂ProjectileFist+触发器Collider，特效作为子物体）
    [Header("动作出拳设置")]
    public float punchVelocityThreshold = 2.2f;   // 前向挥动速度阈值（米/秒），低于此速度不出拳
    public float punchDirectionStrictness = 1.2f; // 前向分量必须超过横向/纵向分量的倍数，防止回收或横移误触发
    public float velocitySmoothing = 10f;         // 速度平滑系数，过滤手柄抖动
    [Header("手柄设置")]
    private Transform RightController;
    private Transform handle; // 手柄的Transform引用
    public Transform fist ; // 手的Transform引用

    private Vector3 _prevHandlePos;
    private Vector3 _smoothedVelocity;
    private bool _hasPrevHandlePos = false;

    // 出拳冷却中（PlayerDefence用它禁止出拳时开盾）
    public bool _isPunching = false;

    private void Awake() {
        // 找到所有弹簧
        XROrigin xrOrigin=FindObjectOfType<XROrigin>();


            RightController = xrOrigin.transform.Find("[Right InteractionAttachController] Attach");
            if (RightController != null)
            {
                handle = RightController.GetChild(0).transform;
            }

    }

    // 拳头不再外伸，无需记录初始位置；保留接口供RightHand调用
    public void Initialize()
    {
    }

    void Update()
    {
        if(!photonView.IsMine){
            return;
        }
        // 检测VR右手柄的出拳挥动动作（替代扳机键触发）
        DetectPunchMotion();
        if(Input.GetKeyDown(fireKey)&& !_isPunching)
        {
            FirePunch();
            Debug.Log("出拳");
        }
    }

    /// <summary>
    /// 检测手柄向前挥动的出拳动作：手柄速度在本地方向上的前向分量(z)超过阈值，
    /// 且明显大于横向(x)/纵向(y)分量时才判定为出拳；回收(z为负)或左右移动不触发
    /// </summary>
    void DetectPunchMotion()
    {
        if (handle == null || Time.deltaTime <= 0f)
        {
            return;
        }

        Vector3 currentPos = handle.position;
        if (!_hasPrevHandlePos)
        {
            _prevHandlePos = currentPos;
            _hasPrevHandlePos = true;
            return;
        }

        Vector3 frameVelocity = (currentPos - _prevHandlePos) / Time.deltaTime;
        _prevHandlePos = currentPos;
        _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, frameVelocity, Mathf.Clamp01(velocitySmoothing * Time.deltaTime));

        Vector3 localVelocity = handle.InverseTransformDirection(_smoothedVelocity);
        if (!_isPunching
            && localVelocity.z > punchVelocityThreshold
            && localVelocity.z > Mathf.Abs(localVelocity.x) * punchDirectionStrictness
            && localVelocity.z > Mathf.Abs(localVelocity.y) * punchDirectionStrictness)
        {
            FirePunch();
            Debug.Log("出拳");
        }
    }

    void FirePunch()
    {
        // 防御性检查
        if (fist == null)
        {
            Debug.LogError("[PhysicsSpringFist] fist 未赋值！请在 Inspector 中设置 fist 引用。");
            return;
        }
        if (projectileFistPrefab == null)
        {
            Debug.LogError("[PhysicsSpringFist] projectileFistPrefab 未赋值！请在 Inspector 中设置发射的拳头预制体。");
            return;
        }

        _isPunching = true;

        // 所有客户端各自从本地对象池取一个拳头子弹发射（携带特效），
        // 手上的拳头和弹簧保持不动；只有发射者客户端的子弹做伤害判定。
        // 飞行方向以手柄的+Z（出拳检测也是按手柄本地z判定的），不能用拳头模型的rotation（它自带X=90°旋转）
        Vector3 punchDir = handle != null ? handle.forward : fist.forward;
        photonView.RPC("RPC_FireProjectile", RpcTarget.All, fist.position, punchDir);

        // 冷却结束后才能再次出拳
        Invoke(nameof(ResetState), punchCooldown);
    }

    [PunRPC]
    void RPC_FireProjectile(Vector3 position, Vector3 direction)
    {
        // 本RPC在每个客户端都执行在发射者的photonView上，直接取发射者的ActorNumber，
        // 让子弹在所有客户端都忽略发射者自己的身体（否则对方客户端上子弹一生成就撞到发射者替身被回收）。
        // 注意不能用ViewID判断：拳头和身体是各自PhotonNetwork.Instantiate的，ViewID不同但Owner相同
        ProjectileFist.Spawn(projectileFistPrefab, position, direction, photonView.IsMine, photonView.OwnerActorNr);
    }

    void ResetState()
    {
        _isPunching = false;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 手上的拳头不再移动，只需同步出拳状态
        if (stream.IsWriting)
        {
            stream.SendNext(_isPunching);
        }
        else
        {
            _isPunching = (bool)stream.ReceiveNext();
        }
    }
}
