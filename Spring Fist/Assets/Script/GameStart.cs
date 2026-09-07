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
    
    
    
    private void OnEnable() {
        
        
        
        XROrigin xrOrigin = FindObjectOfType<XROrigin>();
        
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

        
        //生成玩家替身（预制体必须放在Resources文件夹下）
        GameObject player = PhotonNetwork.Instantiate(
            "Collider", // 预制体名称
            Vector3.zero, // 初始位置
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




