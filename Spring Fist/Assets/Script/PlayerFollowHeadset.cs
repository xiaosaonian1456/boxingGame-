using UnityEngine;
using Photon.Pun;
using Unity.XR.CoreUtils;

/// <summary>
/// 挂在 PlayerNew 预制体上
/// 让玩家的网络替身(PlayerNew)实时跟随本地头显位置
/// 这样其他玩家看到的PlayerNew就在你的真实头显位置
/// </summary>
public class PlayerFollowHeadset : MonoBehaviour
{
    [Header("引用（运行时自动查找）")]
    public Transform headTransform;      // 头显/Main Camera
    
    [Header("配置")]
    public bool followPosition = true;   // 是否跟随位置
    public bool followRotation = true;   // 是否跟随旋转（通常只跟Y轴旋转）
    public bool onlyFollowYaw = true;    // 只跟随水平旋转（推荐）
    
    private PhotonView photonView;
    private bool isInitialized = false;

    void Start()
    {
        photonView = GetComponent<PhotonView>();
        
        // 只有本地玩家才需要跟随头显
        // 其他玩家通过网络同步看到位置
        if (!photonView.IsMine)
        {
            enabled = false;  // 非本地玩家禁用此脚本
            return;
        }
        
        // 延迟查找头显，等待XR系统初始化
        Invoke(nameof(FindHeadTransform), 0.5f);
    }

    void FindHeadTransform()
    {
        // 方式1：通过XROrigin查找
        XROrigin xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            headTransform = xrOrigin.Camera.transform;
            isInitialized = true;
            Debug.Log("[PlayerFollowHeadset] 已找到头显: " + headTransform.name);
            return;
        }
        
        // 方式2：直接找Main Camera
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            headTransform = mainCamera.transform;
            isInitialized = true;
            Debug.Log("[PlayerFollowHeadset] 已找到Main Camera: " + headTransform.name);
            return;
        }
        
        // 方式3：按层级路径查找（根据你的Hierarchy）
        GameObject cameraObj = GameObject.Find("Root/[Building Block] PICO Video Seethrough XR Origin/Camera Offset/Main Camera");
        if (cameraObj != null)
        {
            headTransform = cameraObj.transform;
            isInitialized = true;
            Debug.Log("[PlayerFollowHeadset] 已找到头显(路径): " + headTransform.name);
            return;
        }
        
        Debug.LogError("[PlayerFollowHeadset] 未找到头显！请手动赋值headTransform");
    }

    void Update()
    {
        if (!isInitialized || headTransform == null) return;
        
        // 本地玩家：实时把PlayerNew位置设为头显位置
        // Photon会自动同步给其他玩家
        if (followPosition)
        {
            transform.position = headTransform.position;
        }
        
        if (followRotation)
        {
            if (onlyFollowYaw)
            {
                // 只同步水平旋转（Y轴），保持X和Z为0
                Vector3 headEuler = headTransform.eulerAngles;
                transform.rotation = Quaternion.Euler(0, headEuler.y, 0);
            }
            else
            {
                transform.rotation = headTransform.rotation;
            }
        }
    }
}