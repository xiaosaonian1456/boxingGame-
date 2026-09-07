using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Unity.XR.CoreUtils;

public class PlayerDefence : MonoBehaviourPunCallbacks
{
    [Header("护盾设置")]
    [Tooltip("护盾预制体在 Resources 文件夹中的名称")]
    public string shieldPrefabName = "Dun";
    // [Tooltip("收回护盾所需的最大距离")]
    // public float recallDistance = 2f;

    private Transform LeftController;
    private Transform Lefthandle; // 左手柄的Transform引用
    private Transform Leftfist; // 左手的Transform引用
    private LeftPhysicsSpringFist leftPhysicsSpringFist;
    private LeftTanHuang leftTanHuang;
    private Transform RightController;
    private Transform handle; // 右手柄的Transform引用
    private Transform fist; // 右手的Transform引用
    private PhysicsSpringFist physicsSpringFist;
    private TanHuang tanHuang;
    private XROrigin xrOrigin;
    private Transform mainCamera; // 主相机（VR 头显）

    private GameObject currentShield; // 当前自己生成的护盾
    private bool wasInputPressedLastFrame = false;
    private PlayerMana playerMana; // 蓝量管理

    // Start is called before the first frame update
    void Awake()
    {
        // 找到所有弹簧
        xrOrigin = FindObjectOfType<XROrigin>();
        mainCamera = Camera.main.transform;
        RightController = xrOrigin.transform.Find("[Right InteractionAttachController] Attach");
        if (RightController != null)
        {
            handle = RightController.GetChild(0).transform;
        }
        LeftController = xrOrigin.transform.Find("[Left InteractionAttachController] Attach");
        if (LeftController != null)
        {
            Lefthandle = LeftController.GetChild(0).transform;
        }
    }

    void Start()
    {
        // PlayerMana 挂在场景中的蓝量 UI 上，需要全局查找
        playerMana = FindObjectOfType<PlayerMana>();
        GameObject[] allLeftTanHuang = GameObject.FindGameObjectsWithTag("LeftTanHuang");
        foreach (GameObject leftTanHuangObj in allLeftTanHuang)
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

        GameObject[] allFist = GameObject.FindGameObjectsWithTag("RightQuan");
        foreach (GameObject fistObj in allFist)
        {
            // ✅ 正确：判断这个弹簧本身是不是我的
            PhotonView fistView = fistObj.GetComponent<PhotonView>();
            if (fistView != null && fistView.IsMine)
            {
                fist = fistObj.transform;
                Debug.Log($"找到自己的拳头：{fistObj.name}");
                physicsSpringFist = fist.GetComponent<PhysicsSpringFist>();
                break; // ✅ 找到后立即终止循环，避免被覆盖
            }
        }

        GameObject[] allLeftFist = GameObject.FindGameObjectsWithTag("LeftQuan");
        foreach (GameObject LeftfistObj in allLeftFist)
        {
            // ✅ 正确：判断这个弹簧本身是不是我的
            PhotonView LeftfistView = LeftfistObj.GetComponent<PhotonView>();
            if (LeftfistView != null && LeftfistView.IsMine)
            {
                Leftfist = LeftfistObj.transform;
                Debug.Log($"找到自己的拳头：{Leftfist.name}");
                leftPhysicsSpringFist = Leftfist.GetComponent<LeftPhysicsSpringFist>();
                break; // ✅ 找到后立即终止循环，避免被覆盖
            }
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // 出拳过程中不能操作护盾
        if ((physicsSpringFist != null && physicsSpringFist._isPunching) ||
            (leftPhysicsSpringFist != null && leftPhysicsSpringFist._isLeftPunching))
        {
            return;
        }

        bool inputJustPressed = GetDefenceInputDown();
        if (!inputJustPressed) return;

        // 当前没有护盾且蓝量已满时：生成护盾
        if (currentShield == null && (playerMana == null || playerMana.IsManaFull()))
        {
            defenceway();
        }
        // 已有护盾且靠近自己的护盾时：收回护盾（已取消）
        // else if (IsNearOwnShield())
        // {
        //     RecallShield();
        // }
    }

    /// <summary>
    /// 检测创建/收回护盾的输入（带边缘检测，防止长按连续触发）
    /// </summary>
    private bool GetDefenceInputDown()
    {
        var rightHandDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        bool primaryButtonValue = false;
        bool hasPrimaryButton = rightHandDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out primaryButtonValue);

        var leftHandDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        bool leftPrimaryButtonValue = false;
        bool hasLeftPrimaryButton = leftHandDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out leftPrimaryButtonValue);

        bool vrPressed = (hasLeftPrimaryButton && leftPrimaryButtonValue) || (hasPrimaryButton && primaryButtonValue);
        bool keyboardPressed = Input.GetKeyDown(KeyCode.D);

        bool currentPressed = vrPressed || keyboardPressed;
        bool justPressed = currentPressed && !wasInputPressedLastFrame;
        wasInputPressedLastFrame = currentPressed;

        return justPressed;
    }

    public void defenceway()
    {
        if (currentShield != null) return;

        Debug.Log("开始防御");
        Vector3 spawnPos = mainCamera.position + mainCamera.forward * 1f;
        spawnPos.y = 0f;
        currentShield = PhotonNetwork.Instantiate(shieldPrefabName,
            spawnPos,
            mainCamera.rotation * Quaternion.Euler(6, 180, 0));

        // 创建护盾后消耗所有蓝量
        if (playerMana != null)
        {
            playerMana.ConsumeAllMana();
        }

        // 5秒后自动销毁护盾
        StartCoroutine(DestroyShieldAfterTime(5f));
    }

    /// <summary>
    /// 5秒后自动销毁护盾
    /// </summary>
    private IEnumerator DestroyShieldAfterTime(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (currentShield != null)
        {
            Debug.Log("护盾自动消失");
            PhotonNetwork.Destroy(currentShield);
            currentShield = null;
        }
    }

    // /// <summary>
    // /// 判断是否靠近自己的护盾
    // /// </summary>
    // private bool IsNearOwnShield()
    // {
    //     if (currentShield == null) return false;
    //     return Vector3.Distance(xrOrigin.transform.position, currentShield.transform.position) <= recallDistance;
    // }

    // /// <summary>
    // /// 收回护盾（销毁护盾并重置计数）
    // /// </summary>
    // private void RecallShield()
    // {
    //     if (currentShield == null) return;

    //     Debug.Log("收回护盾");
    //     PhotonNetwork.Destroy(currentShield);
    //     currentShield = null;
    // }
}
