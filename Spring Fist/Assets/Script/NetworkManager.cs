using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    
   
   

    void Start()
    {
        // 自动连接到Photon Master服务器
        PhotonNetwork.ConnectUsingSettings();
        
    }

    // 连接到Master服务器成功
    public override void OnConnectedToMaster()
    {
        Debug.Log("连接到Photon服务器成功");
        PhotonNetwork.JoinLobby(); // 加入大厅
    }

    // 加入大厅成功
    public override void OnJoinedLobby()
    {
        OnJoinRoomButtonClicked();
    }

    // 点击"加入房间"按钮
    public void OnJoinRoomButtonClicked()
    {
       

    
        
        RoomOptions options = new RoomOptions() { MaxPlayers = 4 };
        PhotonNetwork.JoinOrCreateRoom("111", options, default);
       
    }

    // 成功加入房间
    public override void OnJoinedRoom()
    {
        Debug.Log("成功加入房间，当前人数："+PhotonNetwork.CurrentRoom.PlayerCount);
        
       PhotonNetwork.LoadLevel(1);
    }

    // 其他玩家加入房间
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log(newPlayer.NickName+" 加入了房间");
    }

    // 其他玩家离开房间
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log(otherPlayer.NickName+" 离开了房间");
    }
}