using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Unity.XR.CoreUtils;
public class LeftPhysicsSpringFist : MonoBehaviourPun, IPunObservable
{
    [Header("出拳设置")]
    public KeyCode fireKey = KeyCode.E;
    public float maxDistance = 2.5f;
    public float retractDelay = 0.2f;
    public float punchSpeed = 15f;
    public float retractSpeed = 10f;

    [Header("手柄设置")]
    private Transform LeftController;
    public Transform Lefthandle; // 手柄的Transform引用
    public Transform Leftfist ; // 手的Transform引用

    [Header("动作出拳设置")]
    public float punchVelocityThreshold = 2.2f;   // 前向挥动速度阈值（米/秒），低于此速度不出拳
    public float punchDirectionStrictness = 1.2f; // 前向分量必须超过横向/纵向分量的倍数，防止回收或横移误触发
    public float velocitySmoothing = 10f;         // 速度平滑系数，过滤手柄抖动

    private Vector3 _prevHandlePos;
    private Vector3 _smoothedVelocity;
    private bool _hasPrevHandlePos = false;
    
    private Vector3 _initialWorldPos;
    private Vector3 _targetWorldPos;
    public bool _isLeftPunching = false;
    public bool _isLeftExtending = false;
    public GameObject[] leftTanHuangModels;
   private float _currentPunchOffset = 0.2f;
    public bool canDamage=true;
    [Header("特效设置")]
    public ParticleSystem punchEffect; // 出拳特效（挂在拳头下的粒子，取消Play On Awake和Looping，常驻不销毁）
     private LeftTanHuang leftTanHuang;
    private void Awake() {
        // 找到所有弹簧

        XROrigin xrOrigin=FindObjectOfType<XROrigin>();
            
            
            LeftController = xrOrigin.transform.Find("[Left InteractionAttachController] Attach");
            if (LeftController != null)
            {
                Lefthandle = LeftController.GetChild(0).transform;
            }
    }
    void Start()
    {
        GameObject[] allTanHuang = GameObject.FindGameObjectsWithTag("LeftTanHuang");

foreach (GameObject leftTanHuangObj in allTanHuang)
{
    // ✅ 正确：判断这个弹簧本身是不是我的
    PhotonView leftTanHuangPhotonView = leftTanHuangObj.GetComponent<PhotonView>();
    if (leftTanHuangPhotonView != null && leftTanHuangPhotonView.IsMine)
    {
        leftTanHuang = leftTanHuangObj.GetComponent<LeftTanHuang>();
        Debug.Log($"找到自己的弹簧：{leftTanHuangObj.name}");
        break; // ✅ 找到后立即终止循环，避免被覆盖
    }
}

// ✅ 空值检查，方便调试
if (leftTanHuang == null)
{
    Debug.LogError("没有找到自己的弹簧！请检查标签和PhotonView");
}
       
    }

    void Update()
    {
        if(!photonView.IsMine){
            return;
        }
        // 检测VR左手柄的出拳挥动动作（替代扳机键触发）
        DetectPunchMotion();
        if(Input.GetKeyDown(fireKey)&& !_isLeftPunching&&photonView.IsMine)
        {
            FirePunch();
            Debug.Log("出拳");
        }
        if(_isLeftExtending||_isLeftPunching){
        // 平滑控制出拳偏移量
        float targetOffset = _isLeftExtending ? maxDistance : 0.2f;
        float speed = _isLeftExtending ? punchSpeed : retractSpeed;
        _currentPunchOffset = Mathf.MoveTowards(_currentPunchOffset, targetOffset, speed * Time.deltaTime);

        // 拳头是手柄子物体，只需控制本地偏移
        if (Leftfist != null)
        {
            Leftfist.transform.localPosition = Vector3.forward * _currentPunchOffset;
        }
    }

        // 命中后 canDamage 被 FistCollision 设为 false，立即提前收回拳头
        if (_isLeftExtending && !canDamage)
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
        if (Lefthandle == null || Time.deltaTime <= 0f)
        {
            return;
        }

        Vector3 currentPos = Lefthandle.position;
        if (!_hasPrevHandlePos)
        {
            _prevHandlePos = currentPos;
            _hasPrevHandlePos = true;
            return;
        }

        Vector3 frameVelocity = (currentPos - _prevHandlePos) / Time.deltaTime;
        _prevHandlePos = currentPos;
        _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, frameVelocity, Mathf.Clamp01(velocitySmoothing * Time.deltaTime));

        Vector3 localVelocity = Lefthandle.InverseTransformDirection(_smoothedVelocity);
        if (!_isLeftPunching
            && localVelocity.z > punchVelocityThreshold
            && localVelocity.z > Mathf.Abs(localVelocity.x) * punchDirectionStrictness
            && localVelocity.z > Mathf.Abs(localVelocity.y) * punchDirectionStrictness)
        {
            FirePunch();
            Debug.Log("出拳");
        }
    }
    public void Initialize()
    {
        if(photonView.IsMine && Lefthandle != null)
        {
            _initialWorldPos = Lefthandle.position;
            _targetWorldPos = _initialWorldPos;
        }
    }
    void FirePunch()
    {
        // 防御性检查
        if (Leftfist == null)
        {
            Debug.LogError("[LeftPhysicsSpringFist] Leftfist 未赋值！请在 Inspector 中设置 Leftfist 引用。");
            return;
        }

        _isLeftPunching = true;
        _isLeftExtending = true;

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
        _isLeftExtending = false;

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
        _isLeftPunching = false;
        canDamage=true;
        
    }
     void ResetTanHuang()
     {
         if (leftTanHuang == null)
         {
             Debug.LogWarning("[LeftPhysicsSpringFist] leftTanHuang 未找到，跳过弹簧重置");
             return;
         }
         leftTanHuang.ResetToInitialState();
     }
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(_isLeftPunching);
            stream.SendNext(_isLeftExtending);
            stream.SendNext(Leftfist.transform.position);
            stream.SendNext(Leftfist.transform.rotation);
            
        }
        else
        {
            _isLeftPunching = (bool)stream.ReceiveNext();
            _isLeftExtending = (bool)stream.ReceiveNext();
            Leftfist.transform.position = (Vector3)stream.ReceiveNext();
            Leftfist.transform.rotation = (Quaternion)stream.ReceiveNext();
            
            
        }
    }
}