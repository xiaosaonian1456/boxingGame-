using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using System.Diagnostics;



public class RightHand : MonoBehaviourPun
{
    private Transform rightHand;
    public Transform rightHandModel;
    public PhysicsSpringFist physicsSpringFist;
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
            //  rightHandModel.position=rightHand.position+new Vector3(0,0,0.2f);
            //  rightHandModel.rotation = rightHand.rotation * Quaternion.Euler(90, 0, 0);
             rightHandModel.SetParent(rightHand);
             rightHandModel.localPosition=new Vector3(0,0,0.2f);
             rightHandModel.localRotation=Quaternion.Euler(90,0,0);
             physicsSpringFist.Initialize();
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
