using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
public class ReturnButton : MonoBehaviourPunCallbacks
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnEnable()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
    }
    public void Return()
    {
        
        // 只有确实处于房间内，才执行离开操作
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else{
            PhotonNetwork.Disconnect();
            SceneManager.LoadScene(0);
        }
        
    }
     // 离开房间成功的回调，在这里切场景最稳妥
    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        
    }
    
}
