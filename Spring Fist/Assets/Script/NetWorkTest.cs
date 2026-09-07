using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using TMPro;
public class NetWorkTest : MonoBehaviourPunCallbacks
{
    // Start is called before the first frame update
    void Start()
    {
        PhotonNetwork.ConnectUsingSettings();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public override void OnConnectedToMaster()
    {
        
        PhotonNetwork.JoinLobby();
    }
    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
        RoomOptions options = new RoomOptions()
        {
            MaxPlayers = 2,
            IsOpen = true,
            IsVisible = true
        };
        // 房间存在就加入，不存在就创建，第一个点击的人自动开房
        PhotonNetwork.JoinOrCreateRoom("158", options, TypedLobby.Default);
    }
}
