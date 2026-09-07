using UnityEngine;
using Photon.Pun;

public class PlayerSync : MonoBehaviourPun, IPunObservable
{
    [Header("同步设置")]
    public bool syncPosition = true;
    public bool syncRotation = true;
    public bool syncScale = false;

    [Header("平滑设置")]
    public float positionSmooth = 15f;  // 提高到 15，更快的响应速度
    public float rotationSmooth = 15f;

    [Header("网络设置")]
    public bool sendRateSet = false;

    [Header("调试")]
    public bool enableDebugLog = false;

    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private Vector3 _targetScale;
    private PhotonView _photonView;

    void Start()
    {
        _photonView = GetComponent<PhotonView>();
        
        if (_photonView == null)
        {
            Debug.LogError($"PlayerSync: 未找到 PhotonView 组件！GameObject: {gameObject.name}", this);
            enabled = false;
            return;
        }

        _targetPosition = transform.position;
        _targetRotation = transform.rotation;
        _targetScale = transform.localScale;

        if (enableDebugLog)
        {
            Debug.Log($"PlayerSync 初始化 - IsMine: {_photonView.IsMine}, 位置：{_targetPosition}", this);
        }
    }

    void Update()
    {
        if (_photonView == null || _photonView.IsMine)
        {
            return;
        }

        if (syncPosition)
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * positionSmooth);
        }

        if (syncRotation)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, _targetRotation, Time.deltaTime * rotationSmooth);
        }

        if (syncScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * positionSmooth);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (_photonView == null)
        {
            return;
        }

        if (stream.IsWriting)
        {
            // 发送本地玩家的数据
            if (syncPosition)
            {
                stream.SendNext(transform.position);
            }

            if (syncRotation)
            {
                stream.SendNext(transform.rotation);
            }

            if (syncScale)
            {
                stream.SendNext(transform.localScale);
            }

            if (enableDebugLog)
            {
                Debug.Log($"PlayerSync 发送数据 - 位置：{transform.position}, 旋转：{transform.rotation.eulerAngles}", this);
            }
        }
        else
        {
            // 接收远程玩家的数据
            if (syncPosition)
            {
                _targetPosition = (Vector3)stream.ReceiveNext();
            }

            if (syncRotation)
            {
                _targetRotation = (Quaternion)stream.ReceiveNext();
            }

            if (syncScale)
            {
                _targetScale = (Vector3)stream.ReceiveNext();
            }

            if (enableDebugLog)
            {
                Debug.Log($"PlayerSync 接收数据 - 位置：{_targetPosition}, 旋转：{_targetRotation.eulerAngles}", this);
            }
        }
    }
}