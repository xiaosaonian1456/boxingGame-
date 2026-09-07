using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using System.Reflection;
public class PlayerState : MonoBehaviourPunCallbacks, IPunObservable
{
    public TMP_Text PlayerName;
    // public Image FillImage;
    // public Image BackgroundImage;
    private PhotonView photonView;
    // Start is called before the first frame update
    void Start()
    
    {
        photonView = GetComponentInParent<PhotonView>();
        if(photonView.IsMine && PlayerName != null)
        {
            PlayerName.text = PhotonNetwork.NickName;
        }
        else if(!photonView.IsMine && PlayerName != null)
        {
            PlayerName.text =photonView.Owner.NickName;
        }
    
    
    }


    // Update is called once per frame
    void Update()
    {
    
        
    }
    void LateUpdate()
    {
        
            
        
        
    }
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(PlayerName.text);
            // stream.SendNext(FillImage.fillAmount);
            // stream.SendNext(BackgroundImage.fillAmount);
        }
        else
        {
            PlayerName.text = (string)stream.ReceiveNext();
            // FillImage.fillAmount = (float)stream.ReceiveNext();
            // BackgroundImage.fillAmount = (float)stream.ReceiveNext();
        }
    }
}
