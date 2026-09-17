using System.Collections;
using UnityEngine;
using Photon.Pun;
using Unity.XR.CoreUtils;

public class PlayerDefence : MonoBehaviourPunCallbacks
{
    [Header("护盾设置")]
    [Tooltip("护盾预制体在 Resources 文件夹中的名称")]
    public string shieldPrefabName = "Dun";
    [Tooltip("护盾存续期间每秒扣除的蓝量（蓝量耗尽时护盾消失）")]
    public float manaDrainPerSecond = 20f;
    [Tooltip("解除防御姿态后护盾延迟消失的时间（秒），期间停止扣蓝")]
    public float shieldReleaseDelay = 0.2f;
    [Tooltip("召唤护盾所需的最低蓝量")]
    public float manaRequiredToSummon = 40f;

    [Header("防御姿态检测")]
    [Tooltip("双手距离小于该值判定为抱拳（米）")]
    public float handsTogetherDistance = 0.3f;
    [Tooltip("手在头显前方至少该距离才判定为抬起防御（米）")]
    public float handsForwardMinDistance = 0.1f;
    [Tooltip("手相对头显的最低高度偏移（负值表示低于头显，米）")]
    public float handsMinHeightOffset = -0.5f;
    [Tooltip("手相对头显的最高高度偏移（米）")]
    public float handsMaxHeightOffset = 0.05f;
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
    private Coroutine shieldReleaseCoroutine; // 解除姿态后的延迟销毁协程
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

        // 是否正保持防御姿态（抱拳，或编辑器下按住D调试）
        bool defencePosing = IsDefenceInputActive();

        // 护盾存续期间的状态维护（放在出拳检测之前，出拳期间也照常执行；
        // 护盾被打爆销毁后 currentShield 自动变为 null）
        if (currentShield != null)
        {
            if (defencePosing)
            {
                // 保持姿态：取消延迟销毁倒计时，并持续扣蓝，蓝量耗尽时护盾消失
                CancelShieldRelease();
                if (playerMana != null)
                {
                    playerMana.ConsumeMana(manaDrainPerSecond * Time.deltaTime);
                    if (playerMana.CurrentMana <= 0f)
                    {
                        Debug.Log("蓝量耗尽，护盾消失");
                        PhotonNetwork.Destroy(currentShield);
                        currentShield = null;
                    }
                }
            }
            else if (shieldReleaseCoroutine == null)
            {
                // 解除防御姿态：停止扣蓝，短暂延迟后护盾消失
                shieldReleaseCoroutine = StartCoroutine(DestroyShieldAfterRelease());
            }
        }

        // 出拳过程中不能操作护盾
        if ((physicsSpringFist != null && physicsSpringFist._isPunching) ||
            (leftPhysicsSpringFist != null && leftPhysicsSpringFist._isLeftPunching))
        {
            wasInputPressedLastFrame = defencePosing;
            return;
        }

        // 边缘检测：只有进入姿态的那一帧才触发，保持抱拳不会连续召唤护盾
        bool inputJustPressed = defencePosing && !wasInputPressedLastFrame;
        wasInputPressedLastFrame = defencePosing;
        if (!inputJustPressed) return;

        // 当前没有护盾且蓝量达到召唤要求时：生成护盾
        if (currentShield == null && (playerMana == null || playerMana.CurrentMana >= manaRequiredToSummon))
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
    /// 是否正保持防御输入：双手交叉抱拳的防御姿态，或编辑器下按住D键调试
    /// </summary>
    private bool IsDefenceInputActive()
    {
        return IsCrossedGuardPose() || Input.GetKey(KeyCode.D);
    }

    /// <summary>
    /// 解除防御姿态后延迟销毁护盾（延迟期间重新抱拳则通过 CancelShieldRelease 取消）
    /// </summary>
    private IEnumerator DestroyShieldAfterRelease()
    {
        yield return new WaitForSeconds(shieldReleaseDelay);
        shieldReleaseCoroutine = null;

        if (currentShield != null)
        {
            Debug.Log("解除防御姿态，护盾消失");
            PhotonNetwork.Destroy(currentShield);
            currentShield = null;
        }
    }

    private void CancelShieldRelease()
    {
        if (shieldReleaseCoroutine != null)
        {
            StopCoroutine(shieldReleaseCoroutine);
            shieldReleaseCoroutine = null;
        }
    }

    /// <summary>
    /// 通过追踪双手手柄的位置判断是否呈现双手交叉抱拳的防御姿态：
    /// 1. 双手距离足够近（抱拳）；
    /// 2. 双手位于头显前方且处于胸前高度范围（抬起防御）；
    /// 3. 左右手越过身体中线交叉（头显局部坐标下右手在左手左侧）。
    /// </summary>
    private bool IsCrossedGuardPose()
    {
        if (handle == null || Lefthandle == null || mainCamera == null) return false;

        // 双手靠近（抱拳）
        if (Vector3.Distance(handle.position, Lefthandle.position) > handsTogetherDistance) return false;

        // 转到头显局部空间，消除头部朝向的影响
        Vector3 rightLocal = mainCamera.InverseTransformPoint(handle.position);
        Vector3 leftLocal = mainCamera.InverseTransformPoint(Lefthandle.position);

        if (!IsHandInGuardArea(rightLocal) || !IsHandInGuardArea(leftLocal)) return false;

        // 双手交叉：右手越过中线到左侧，左手越过中线到右侧
        return rightLocal.x < leftLocal.x;
    }

    /// <summary>
    /// 判断单手是否位于头显前方的胸前防御区域（头显局部坐标）
    /// </summary>
    private bool IsHandInGuardArea(Vector3 handLocalPos)
    {
        bool inFront = handLocalPos.z > handsForwardMinDistance;
        bool inHeight = handLocalPos.y > handsMinHeightOffset && handLocalPos.y < handsMaxHeightOffset;
        return inFront && inHeight;
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

        // 护盾存续期间在 Update 中持续扣蓝，蓝量耗尽或被打爆（ShieldDurability）时消失
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
