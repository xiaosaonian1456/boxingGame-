using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using System.Diagnostics;



public class LeftHandTanHuang : MonoBehaviourPun
{
    private Transform leftHand;
    public Transform leftHandTanHuangModel;
    public LeftTanHuang leftTanHuang;
    private LeftPhysicsSpringFist leftPhysicsSpringFist;
   
    // Start is called before the first frame update
    private void Awake() {
        // 关键：只有本地玩家才驱动替身
        
        if(photonView.IsMine&&PhotonNetwork.IsConnected){
            XROrigin xrOrigin=FindObjectOfType<XROrigin>();
            
            Transform leftAttachParent = xrOrigin.transform.Find("[Left InteractionAttachController] Attach");
            if (leftAttachParent != null)
            {
                leftHand = leftAttachParent.GetChild(0);
            }
        }
        
    }
    void Start()
    {
        if(photonView.IsMine&&PhotonNetwork.IsConnected)
        {
            if(leftHand!=null){
            //  leftHandTanHuangModel.position=leftHand.position;
            //  leftHandTanHuangModel.rotation = leftHand.rotation * Quaternion.Euler(90, 0, 0);
             leftHandTanHuangModel.SetParent(leftHand);
             leftHandTanHuangModel.localRotation=Quaternion.Euler(90,0,0);
             leftTanHuang.Initialize();
         }
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void LateUpdate() {
        
        
    }

    
}
