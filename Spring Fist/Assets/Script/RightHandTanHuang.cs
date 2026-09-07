using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using System.Diagnostics;



public class RightHandTanHuang : MonoBehaviourPun
{
    private Transform rightHand;
    public Transform rightHandTanHuangModel;
    public TanHuang tanHuang;

   
    // Start is called before the first frame update
    private void Awake() {
        // 关键：只有本地玩家才驱动替身
        
        if(photonView.IsMine&&PhotonNetwork.IsConnected){
            XROrigin xrOrigin=FindObjectOfType<XROrigin>();
            
            Transform rightAttachParent = xrOrigin.transform.Find("[Right InteractionAttachController] Attach");
            if (rightAttachParent != null)
            {
                rightHand = rightAttachParent.GetChild(0);
            }
        }
        
    }
    void Start()
    {
        if(photonView.IsMine&&PhotonNetwork.IsConnected)
        {
            if(rightHand!=null){
            //  rightHandTanHuangModel.position=rightHand.position;
            //  rightHandTanHuangModel.rotation = rightHand.rotation * Quaternion.Euler(90, 0, 0);
             rightHandTanHuangModel.SetParent(rightHand);
             rightHandTanHuangModel.localRotation=Quaternion.Euler(90,0,0);
             tanHuang.Initialize();
         }
        }
         

    }
    

    // Update is called once per frame
   
    private void LateUpdate() {
        
        
    }
 

    
}
