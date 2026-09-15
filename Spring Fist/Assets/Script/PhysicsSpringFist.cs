using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Unity.XR.CoreUtils;
using System.Runtime.CompilerServices;
public class PhysicsSpringFist : MonoBehaviourPun, IPunObservable
{
    [Header("出拳设置")]
    public KeyCode fireKey = KeyCode.F;
    public float maxDistance = 2.5f;
    public float retractDelay = 0.2f;
    public float punchSpeed = 15f;
    public float retractSpeed = 10f;
    public bool canDamage=true;
    [Header("特效设置")]
    public ParticleSystem punchEffect; // 出拳特效（挂在拳头下的粒子，取消Play On Awake和Looping，常驻不销毁）
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

    
    private Vector3 _initialWorldPos;
    private Vector3 _targetWorldPos;
    public bool _isPunching = false;
    public bool _isExtending = false;
    public GameObject[] TanHuangModels;
   // 当前出拳的偏移量（0 = 收回，maxDistance = 完全伸出）
    private float _currentPunchOffset = 0.2f;
     private TanHuang tanHuang;
    private void Awake() {
        // 找到所有弹簧
        XROrigin xrOrigin=FindObjectOfType<XROrigin>();
            
            
            RightController = xrOrigin.transform.Find("[Right InteractionAttachController] Attach");
            if (RightController != null)
            {
                handle = RightController.GetChild(0).transform;
            }
        
    }
    void Start()
    {
        GameObject[] allTanHuang = GameObject.FindGameObjectsWithTag("RightTanHuang");

foreach (GameObject tanHuangObj in allTanHuang)
{
    // ✅ 正确：判断这个弹簧本身是不是我的
    PhotonView tanHuangPhotonView = tanHuangObj.GetComponent<PhotonView>();
    if (tanHuangPhotonView != null && tanHuangPhotonView.IsMine)
    {
        tanHuang = tanHuangObj.GetComponent<TanHuang>();
        Debug.Log($"找到自己的弹簧：{tanHuangObj.name}");
        break; // ✅ 找到后立即终止循环，避免被覆盖
    }
    
}

// ✅ 空值检查，方便调试
if (tanHuang == null)
{
    Debug.LogError("没有找到自己的弹簧！请检查标签和PhotonView");
}
        
        
    }
    public void Initialize()
    {
        if(photonView.IsMine && handle != null)
        {
            _initialWorldPos = handle.position;
            _targetWorldPos = _initialWorldPos;
        }
    }

    

    void Update()
    {
        if(!photonView.IsMine){
            return;
        }
        // 检测VR右手柄的出拳挥动动作（替代扳机键触发）
        DetectPunchMotion();
        if(Input.GetKeyDown(fireKey)&& !_isPunching&&photonView.IsMine)
        {
            FirePunch();
            Debug.Log("出拳");
        }
        if(_isPunching||_isExtending){
            // 平滑控制出拳偏移量
        float targetOffset = _isExtending ? maxDistance : 0.2f;
        float speed = _isExtending ? punchSpeed : retractSpeed;
        _currentPunchOffset = Mathf.MoveTowards(_currentPunchOffset, targetOffset, speed * Time.deltaTime);

        // 拳头是手柄子物体，只需控制本地偏移
        if (fist != null)
        {
            fist.transform.localPosition = Vector3.forward * _currentPunchOffset;
        }
        }

        // 命中后 canDamage 被 FistCollision 设为 false，立即提前收回拳头
        if (_isExtending && !canDamage)
        {
            EarlyRetractPunch();
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

        _isPunching = true;
        _isExtending = true;

        // 出拳特效：通过RPC让本地和对方客户端各播放一次，特效物体常驻不销毁
        photonView.RPC("RPC_PlayPunchEffect", RpcTarget.All);

        // 延迟收回
        Invoke(nameof(RetractPunch), retractDelay);
    }

    /// <summary>
    /// 播放一次出拳特效（先Stop再Play，连续出拳时能从头重新播放）
    /// </summary>
    [PunRPC]
    void RPC_PlayPunchEffect()
    {
        if (punchEffect == null) return;

        punchEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        punchEffect.Play();
    }

    /// <summary>
    /// 拳头开始收回时停止特效：遍历特效下所有粒子系统并立即清除
    /// （Hovl预制体通常含多个子粒子，只Stop根节点不可靠；StopEmitting会残留已发射粒子）
    /// </summary>
    [PunRPC]
    void RPC_StopPunchEffect()
    {
        if (punchEffect == null) return;

        foreach (ParticleSystem ps in punchEffect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    public void RetractPunch()
    {
        _isExtending = false;

        // 拳头开始收回（包括命中提前收回），RPC通知双方立即停止特效
        photonView.RPC("RPC_StopPunchEffect", RpcTarget.All);

        // 等待收回完成后重置状态
        Invoke(nameof(ResetState), 0.5f);
         Invoke(nameof(ResetTanHuang), 0.3f);
    }

    /// <summary>
    /// 命中目标后提前收回拳头，取消原定的延迟收回
    /// </summary>
    void EarlyRetractPunch()
    {
        CancelInvoke(nameof(RetractPunch));
        RetractPunch();
    }

    void ResetState()
    {
        _isPunching = false;
        canDamage=true;
        
    }
     void ResetTanHuang()
     {
         if (tanHuang == null)
         {
             Debug.LogWarning("[PhysicsSpringFist] tanHuang 未找到，跳过弹簧重置");
             return;
         }
         tanHuang.ResetToInitialState();
     }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            
            stream.SendNext(_isPunching);
            stream.SendNext(_isExtending);
            stream.SendNext(fist.transform.position);
            stream.SendNext(fist.transform.rotation);
         
            
        }
        else
        {
            
            _isPunching = (bool)stream.ReceiveNext();
            _isExtending = (bool)stream.ReceiveNext();
            fist.transform.position = (Vector3)stream.ReceiveNext();
            fist.transform.rotation = (Quaternion)stream.ReceiveNext();
            
            

        }
    }
}