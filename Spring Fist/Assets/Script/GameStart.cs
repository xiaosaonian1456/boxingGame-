using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

using Photon.Realtime;


public class GameStart : MonoBehaviourPunCallbacks
{
    
    private Transform LeftController;
    private Transform LeftHand;
    private Transform RightController;
    private Transform RightHand;
    private bool startXR=false;
    public GameObject XRO;

    [Header("四人模式出生点（按席位序号：红1、红2、蓝1、蓝2）")]
    public Vector3[] spawnPoints = new Vector3[]
    {
        new Vector3(-1.5f, 0f, -2f),
        new Vector3( 1.5f, 0f, -2f),
        new Vector3(-1.5f, 0f,  2f),
        new Vector3( 1.5f, 0f,  2f),
    };
    // 延迟移动XR Rig，等待PICO头显追踪初始化完成后再放置
    public float spawnApplyDelay = 0.5f;

    private XROrigin xrOrigin;



    private void OnEnable() {



        xrOrigin = FindObjectOfType<XROrigin>();
        
        LeftController = xrOrigin.transform.Find("[Left InteractionAttachController] Attach");
        if (LeftController != null)
        {
            LeftHand = LeftController.GetChild(0);
        }
        RightController = xrOrigin.transform.Find("[Right InteractionAttachController] Attach");
        if (RightController != null)
        {
            RightHand = RightController.GetChild(0);
        }

        //四人模式：按席位序号分配出生点，避免所有玩家重叠在Vector3.zero
        int spawnIndex = GetSpawnIndex();
        Vector3 spawnPos = spawnIndex >= 0 ? spawnPoints[spawnIndex] : Vector3.zero;
        if (spawnIndex >= 0)
        {
            StartCoroutine(MoveRigToSpawnPoint(spawnIndex));
        }

        //生成玩家替身（预制体必须放在Resources文件夹下）
        GameObject player = PhotonNetwork.Instantiate(
            "Collider", // 预制体名称
            spawnPos, // 初始位置
            Quaternion.identity // 初始旋转
        );

        if (RightController != null)
        {
            GameObject rightHand = PhotonNetwork.Instantiate(
            "RightQuan", // 预制体名称
            Vector3.zero, // 初始位置
            Quaternion.identity // 初始旋转
        );
        }
        if (RightController != null)
        {
            GameObject rightTanHuang = PhotonNetwork.Instantiate(
            "RightTanHuang", // 预制体名称
            Vector3.zero, // 初始位置
            Quaternion.identity // 初始旋转
        );
        }

        if (LeftController != null)
        {
            GameObject leftHand = PhotonNetwork.Instantiate(
            "LeftQuan", // 预制体名称
            Vector3.zero, // 初始位置
            Quaternion.identity // 初始旋转
        );
        }

        if (LeftController != null)
        {
            GameObject leftTanHuang = PhotonNetwork.Instantiate(
            "LeftTanHuang", // 预制体名称
            Vector3.zero, // 初始位置
            Quaternion.identity // 初始旋转
        );
        }
    }

    // 获取本地玩家的出生点序号：四人模式按选座席位（红1=0、红2=1、蓝1=2、蓝2=3），
    // 双人模式返回-1保持原有行为；席位属性丢失时按ActorNumber排序兜底
    private int GetSpawnIndex()
    {
        // PICO真机（Android）：大空间技术已统一四台设备的世界原点，玩家按真实站位自然分开，
        // 返回-1不做任何出生点偏移，保持原有大空间行为
        if (Application.platform == RuntimePlatform.Android)
        {
            return -1;
        }

        // 电脑端（编辑器/PC包）：大空间不可用，相机都报本地原点附近坐标会重叠，按席位分配出生点
        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.MaxPlayers != 4)
        {
            return -1;
        }

        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        for (int i = 0; i < TeamSelectUI.SeatKeys.Length; i++)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TeamSelectUI.SeatKeys[i], out object value)
                && value is int holder && holder == myActor)
            {
                return i;
            }
        }

        Player[] players = PhotonNetwork.PlayerList;
        System.Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].ActorNumber == myActor)
            {
                return i % spawnPoints.Length;
            }
        }
        return -1;
    }

    // 把XR Rig整体移动到出生点：相机/手柄由PICO追踪驱动、相对于Rig定位，
    // 移动Rig后替身与拳头仍实时跟随头显和手柄，只是世界坐标带上了出生点偏移
    private IEnumerator MoveRigToSpawnPoint(int index)
    {
        yield return new WaitForSeconds(spawnApplyDelay);
        if (xrOrigin == null || xrOrigin.Camera == null)
        {
            yield break;
        }

        Vector3 spawnPos = spawnPoints[index];
        Transform rig = xrOrigin.transform;
        Transform cam = xrOrigin.Camera.transform;

        // 绕头显水平旋转Rig，让玩家面向场地中心（不影响头显追踪，只改变Rig朝向）
        Vector3 toCenter = new Vector3(-spawnPos.x, 0f, -spawnPos.z);
        if (toCenter.sqrMagnitude > 0.001f)
        {
            float targetYaw = Quaternion.LookRotation(toCenter).eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(cam.eulerAngles.y, targetYaw);
            rig.RotateAround(cam.position, Vector3.up, yawDelta);
        }

        // 把头显脚下的水平投影平移到出生点，保留PICO追踪的相对偏移与Y高度
        rig.position += new Vector3(spawnPos.x - cam.position.x, 0f, spawnPos.z - cam.position.z);
        Debug.Log($"[GameStart] 出生点 {index}：Rig已移动到 {rig.position}");
    }


    // Start is called before the first frame update
    void Start()
    {
        

        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //public void StartOrigin(){

    //    GameObject MineXRO = PhotonNetwork.Instantiate("[Building Block] PICO Video Seethrough XR Origin (XR Rig)", Vector3.zero, Quaternion.identity);
    //    if(PhotonNetwork.IsMasterClient){
            
            
    //    }
       
        // 调用你之前的位置重置函数，校准本地玩家位置
        //FindObjectOfType<ResetPosition>().cz();

        // 找到属于自己的 XROrigin
        // XROrigin xrOrigin = null;
        // XROrigin[] allXROrigins = FindObjectsOfType<XROrigin>();
        // foreach (XROrigin origin in allXROrigins)
        // {
        //     PhotonView pv = origin.GetComponent<PhotonView>();
        //     if (pv != null && pv.IsMine)
        //     {
        //         xrOrigin = origin;
        //         break;
        //     }
        // }
            
        //     LeftController = xrOrigin.transform.Find("[Left InteractionAttachController] Attach");
        //     if (LeftController != null)
        //     {
        //         LeftHand = LeftController.GetChild(0);
        //     }
        //     RightController = xrOrigin.transform.Find("[Right InteractionAttachController] Attach");
        //     if (RightController != null)
        //     {
        //         RightHand = RightController.GetChild(0);
        //     }
           
        // // PhotonNetwork.Instantiate("Player", new Vector3(0, 1, 0), Quaternion.identity, 0);
        //  // 生成玩家替身（预制体必须放在Resources文件夹下）
        // GameObject player=PhotonNetwork.Instantiate(
        //     "PlayerNew", // 预制体名称
        //     Vector3.zero, // 初始位置
        //     Quaternion.identity // 初始旋转
        // );
        
        // if(RightController!=null){
        //     GameObject rightHand=PhotonNetwork.Instantiate(
        //     "RightQuan", // 预制体名称
        //     Vector3.zero, // 初始位置
        //     Quaternion.identity // 初始旋转
        // );}
        // if(RightController!=null){
        //     GameObject rightTanHuang=PhotonNetwork.Instantiate(
        //     "RightTanHuang", // 预制体名称
        //     Vector3.zero, // 初始位置
        //     Quaternion.identity // 初始旋转
        // );}
        
        //  if(LeftController!=null){
        //      GameObject leftHand=PhotonNetwork.Instantiate(
        //      "LeftQuan", // 预制体名称
        //      Vector3.zero, // 初始位置
        //      Quaternion.identity // 初始旋转
        //  );}
        
        //  if(LeftController!=null){
        //      GameObject leftTanHuang=PhotonNetwork.Instantiate(
        //      "LeftTanHuang", // 预制体名称
        //      Vector3.zero, // 初始位置
        //      Quaternion.identity // 初始旋转
        //  );}
    }




