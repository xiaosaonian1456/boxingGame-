using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using System.Diagnostics;
using Unity.Mathematics;



public class LeftHand : MonoBehaviourPun
{
    private Transform leftHand;
    public Transform leftHandModel;
    public LeftPhysicsSpringFist leftPhysicsSpringFist;
   
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
            //  leftHandModel.position=leftHand.position+new Vector3(0,0,0.2f);
            //  leftHandModel.rotation = leftHand.rotation * Quaternion.Euler(90, 0, 0);
             leftHandModel.SetParent(leftHand);
             leftHandModel.localPosition=new Vector3(0,0,0.2f);
             leftHandModel.localRotation=Quaternion.Euler(90,0,0);
             leftPhysicsSpringFist.Initialize();
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
