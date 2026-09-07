using UnityEngine;
using UnityEngine.InputSystem.XR;
using Photon.Pun;

public class XRRemotePlayerSetup : MonoBehaviourPun
{
    [Header("玩家引用（必须赋值！）")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;
    public Camera playerCamera;

    private XRPlayerTrackerSync _trackerSync;

    void Start()
    {
        Debug.Log($"玩家 {photonView.Owner.NickName} 生成，IsMine：{photonView.IsMine}");

        // 获取追踪同步组件
        _trackerSync = GetComponent<XRPlayerTrackerSync>();

        if (photonView.IsMine)
        {
            Debug.Log("这是本地玩家，正在隐藏模型并启用追踪");
            DisableLocalPlayerRenderers();
            
            // 确保本地玩家的追踪同步组件引用正确
            if (_trackerSync != null)
            {
                SetupTrackerSyncReferences();
            }
        }
        else
        {
            Debug.Log("这是远程玩家，正在显示模型并禁用追踪（使用网络同步）");
            DisableAllTrackedPoseDrivers();
            DisableCamera();
            EnableRemotePlayerRenderers();

            // 确保远程玩家的追踪同步组件引用正确
            if (_trackerSync != null)
            {
                SetupTrackerSyncReferences();
            }

            // 打印所有渲染器的状态
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            Debug.Log($"找到 {allRenderers.Length} 个渲染器");
            foreach (var renderer in allRenderers)
            {
                Debug.Log($"渲染器 {renderer.name} 状态：{renderer.enabled}");
            }
        }
    }

    void SetupTrackerSyncReferences()
    {
        // 如果没有手动赋值，自动查找
        if (_trackerSync.head == null && head != null)
        {
            _trackerSync.head = head;
        }
        if (_trackerSync.leftHand == null && leftHand != null)
        {
            _trackerSync.leftHand = leftHand;
        }
        if (_trackerSync.rightHand == null && rightHand != null)
        {
            _trackerSync.rightHand = rightHand;
        }

        Debug.Log($"追踪同步引用已设置 - 头部: {_trackerSync.head?.name}, 左手: {_trackerSync.leftHand?.name}, 右手: {_trackerSync.rightHand?.name}");
    }

    void DisableAllTrackedPoseDrivers()
    {
        var allDrivers = GetComponentsInChildren<TrackedPoseDriver>(true);
        Debug.Log($"禁用 {allDrivers.Length} 个 TrackedPoseDriver");
        foreach (var driver in allDrivers)
        {
            driver.enabled = false;
        }
    }

    void DisableCamera()
    {
        // 优先使用手动指定的相机
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
            playerCamera.gameObject.SetActive(false);
            Debug.Log($"禁用相机：{playerCamera.name}");
        }
        else
        {
            // 自动查找所有相机
            var cameras = GetComponentsInChildren<Camera>(true);
            Debug.Log($"禁用 {cameras.Length} 个相机");
            foreach (var camera in cameras)
            {
                camera.enabled = false;
                camera.gameObject.SetActive(false);
            }
        }
    }

    void DisableLocalPlayerRenderers()
    {
        // 禁用所有渲染器，但保留拳头相关物体的渲染器（本地玩家需要看到自己的拳头）
        var allRenderers = GetComponentsInChildren<Renderer>(true);
        int disabledCount = 0;
        int skippedCount = 0;
        
        foreach (var renderer in allRenderers)
        {
            // 检查是否是拳头相关的物体（Quan, TanHuang, End, Foward 等）
            string objName = renderer.gameObject.name.ToLower();
            if (objName.Contains("quan") || objName.Contains("tanhuang") || 
                objName.Contains("end") || objName.Contains("foward") ||
                objName.Contains("fist") || objName.Contains("spring"))
            {
                // 保留拳头相关物体的渲染器，让本地玩家能看到自己的拳头
                skippedCount++;
                continue;
            }
            
            renderer.enabled = false;
            disabledCount++;
        }
        
        Debug.Log($"本地玩家：禁用 {disabledCount} 个渲染器，保留 {skippedCount} 个拳头相关渲染器");
    }

    void EnableRemotePlayerRenderers()
    {
        // 启用所有渲染器
        var allRenderers = GetComponentsInChildren<Renderer>(true);
        Debug.Log($"远程玩家：启用 {allRenderers.Length} 个渲染器");
        foreach (var renderer in allRenderers)
        {
            renderer.enabled = true;
        }
    }
}