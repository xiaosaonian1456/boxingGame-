using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using System.Diagnostics;

public class PlayerAvatarDriver : MonoBehaviourPun
{
    [Header("本地XR引用")]
    private Transform xrCamera;
  

    [Header("替身模型引用")]
    public Transform headModel;
    public GameObject MineCanvas;
    
    
    private void Awake() {
        
    }
    
    void Start()
    {
        // 关键：只有本地玩家才驱动替身
        if(photonView.IsMine&&PhotonNetwork.IsConnected){

            // 获取本地XROrigin的引用（确保场景中只有一个XROrigin）
            XROrigin xrOrigin=FindObjectOfType<XROrigin>();
            xrCamera=xrOrigin.Camera.transform;
            
            
            // 隐藏本地玩家自己的替身模型（避免看到自己的头）
            MineCanvas.SetActive(false);}
            headModel.SetParent(xrCamera);
            headModel.transform.localPosition=new Vector3(0,-1f,0);
            headModel.transform.localRotation=Quaternion.identity;
        
        
       
    
    }
    private void Update() 
    {
        

        
       
                    
         
    }

    void LateUpdate()
    {
        //    if(xrCamera!=null&&photonView.IsMine&&PhotonNetwork.IsConnected){
        //       // 用本地XR设备的位置更新替身的位置
              
        //       headModel.position=xrCamera.position;
        //       headModel.rotation=xrCamera.rotation;
              
        //   }
        
        
         if(photonView.IsMine&&PhotonNetwork.IsConnected&&MineCanvas.activeSelf){
             // 隐藏本地玩家自己的替身模型（避免看到自己的头）
            MineCanvas.SetActive(false);
            
         }
      

     
        
        
        
        
    }

    
}