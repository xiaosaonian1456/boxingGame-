using UnityEngine;
using Photon.Pun;

/// <summary>
/// 同步 VR 玩家的头部和手部追踪数据
/// 本地玩家：读取 VR 追踪数据并发送
/// 远程玩家：接收同步数据并更新位置
/// </summary>
public class XRPlayerTrackerSync : MonoBehaviourPun, IPunObservable
{
    [Header("追踪目标（必须赋值！）")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("同步设置")]
    public bool syncHead = true;
    public bool syncLeftHand = true;
    public bool syncRightHand = true;

    [Header("平滑设置")]
    public float positionSmooth = 15f;
    public float rotationSmooth = 15f;

    [Header("调试")]
    public bool enableDebugLog = false;

    // 远程玩家的目标位置和旋转
    private Vector3 _targetHeadPosition;
    private Quaternion _targetHeadRotation;
    private Vector3 _targetLeftHandPosition;
    private Quaternion _targetLeftHandRotation;
    private Vector3 _targetRightHandPosition;
    private Quaternion _targetRightHandRotation;

    private PhotonView _photonView;

    void Start()
    {
        _photonView = GetComponent<PhotonView>();

        if (_photonView == null)
        {
            Debug.LogError("XRPlayerTrackerSync: 未找到 PhotonView 组件！", this);
            enabled = false;
            return;
        }

        // 初始化目标值
        if (head != null)
        {
            _targetHeadPosition = head.position;
            _targetHeadRotation = head.rotation;
        }
        if (leftHand != null)
        {
            _targetLeftHandPosition = leftHand.position;
            _targetLeftHandRotation = leftHand.rotation;
        }
        if (rightHand != null)
        {
            _targetRightHandPosition = rightHand.position;
            _targetRightHandRotation = rightHand.rotation;
        }

        if (enableDebugLog)
        {
            Debug.Log($"XRPlayerTrackerSync 初始化 - IsMine: {_photonView.IsMine}", this);
        }
    }

    void Update()
    {
        // 本地玩家：VR 追踪设备会自动更新位置，不需要手动处理
        if (_photonView.IsMine)
        {
            return;
        }

        // 远程玩家：使用同步的数据更新位置
        UpdateRemotePlayerPositions();
    }

    void UpdateRemotePlayerPositions()
    {
        // 更新头部
        if (syncHead && head != null)
        {
            head.position = Vector3.Lerp(head.position, _targetHeadPosition, Time.deltaTime * positionSmooth);
            head.rotation = Quaternion.Lerp(head.rotation, _targetHeadRotation, Time.deltaTime * rotationSmooth);
        }

        // 更新左手
        if (syncLeftHand && leftHand != null)
        {
            leftHand.position = Vector3.Lerp(leftHand.position, _targetLeftHandPosition, Time.deltaTime * positionSmooth);
            leftHand.rotation = Quaternion.Lerp(leftHand.rotation, _targetLeftHandRotation, Time.deltaTime * rotationSmooth);
        }

        // 更新右手
        if (syncRightHand && rightHand != null)
        {
            rightHand.position = Vector3.Lerp(rightHand.position, _targetRightHandPosition, Time.deltaTime * positionSmooth);
            rightHand.rotation = Quaternion.Lerp(rightHand.rotation, _targetRightHandRotation, Time.deltaTime * rotationSmooth);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (_photonView == null)
            return;

        if (stream.IsWriting)
        {
            // 本地玩家：发送追踪数据
            if (syncHead && head != null)
            {
                stream.SendNext(head.position);
                stream.SendNext(head.rotation);
            }

            if (syncLeftHand && leftHand != null)
            {
                stream.SendNext(leftHand.position);
                stream.SendNext(leftHand.rotation);
            }

            if (syncRightHand && rightHand != null)
            {
                stream.SendNext(rightHand.position);
                stream.SendNext(rightHand.rotation);
            }

            if (enableDebugLog)
            {
                Debug.Log($"发送追踪数据 - 头部: {head?.position}, 左手: {leftHand?.position}, 右手: {rightHand?.position}", this);
            }
        }
        else
        {
            // 远程玩家：接收追踪数据
            if (syncHead && head != null)
            {
                _targetHeadPosition = (Vector3)stream.ReceiveNext();
                _targetHeadRotation = (Quaternion)stream.ReceiveNext();
            }

            if (syncLeftHand && leftHand != null)
            {
                _targetLeftHandPosition = (Vector3)stream.ReceiveNext();
                _targetLeftHandRotation = (Quaternion)stream.ReceiveNext();
            }

            if (syncRightHand && rightHand != null)
            {
                _targetRightHandPosition = (Vector3)stream.ReceiveNext();
                _targetRightHandRotation = (Quaternion)stream.ReceiveNext();
            }

            if (enableDebugLog)
            {
                Debug.Log($"接收追踪数据 - 头部: {_targetHeadPosition}, 左手: {_targetLeftHandPosition}, 右手: {_targetRightHandPosition}", this);
            }
        }
    }
}
