using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using TMPro;

public class NetworkLancher : MonoBehaviourPunCallbacks
{
    [Header("UI面板")]
    public GameObject GonnectUIObj;    // 连接中界面
    public GameObject NameUI;          // 昵称+大厅界面
    public GameObject WaitUI;          // 房间等待界面
   

    [Header("房间列表UI")]
    public Transform roomListContent;  // ScrollView的条目容器
    public GameObject roomItemPrefab;  // 房间条目预制体

    // 本地缓存服务器推送的房间数据
    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    // 管理5个固定的房间UI条目，用于快速更新状态
    private Dictionary<string, RoomItem> roomItemDict = new Dictionary<string, RoomItem>();
    private bool hasLoadedLevel = false;
    private const int TotalRoomCount = 5; // 固定房间总数

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void Start()
    {
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 30;
        PhotonNetwork.ConnectUsingSettings();
    }

    void Update()
    {
        // 修复空引用：只有在房间内才检测人数
        if (PhotonNetwork.IsMasterClient 
            && PhotonNetwork.CurrentRoom != null 
            && PhotonNetwork.CurrentRoom.PlayerCount == 2 
            && !hasLoadedLevel)
        {
            hasLoadedLevel = true;
            PhotonNetwork.LoadLevel(1);
        }
    }

    // 连接Master成功 → 加入大厅
    public override void OnConnectedToMaster()
    {
        GonnectUIObj.SetActive(false);
        PhotonNetwork.JoinLobby();
    }

    // 进入大厅成功 → 生成5个固定房间条目
    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
        NameUI.SetActive(true);
        
        cachedRoomList.Clear();
        roomItemDict.Clear();
        ClearRoomListUI();
        GenerateFixedRoomItems(); // 生成5个固定UI条目
        
        Debug.Log("已进入大厅，房间列表已初始化");
    }

    // 生成5个固定的房间UI条目（Room_1 ~ Room_5）
    private void GenerateFixedRoomItems()
    {
        for (int i = 1; i <= TotalRoomCount; i++)
        {
            string roomName = $"Room_{i}";
            GameObject itemObj = Instantiate(roomItemPrefab, roomListContent);
            RoomItem item = itemObj.GetComponent<RoomItem>();
            
            item.Init(roomName, JoinTargetRoom);
            roomItemDict.Add(roomName, item);
        }
    }

    // 服务器推送房间列表更新 → 只更新条目状态，不销毁重建
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        base.OnRoomListUpdate(roomList);
        UpdateCachedRoomList(roomList);
        UpdateAllRoomItemsStatus();
    }

    // 更新本地缓存的房间数据
    private void UpdateCachedRoomList(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            if (info.RemovedFromList || !info.IsOpen || !info.IsVisible)
            {
                if (cachedRoomList.ContainsKey(info.Name))
                    cachedRoomList.Remove(info.Name);
                continue;
            }

            if (cachedRoomList.ContainsKey(info.Name))
                cachedRoomList[info.Name] = info;
            else
                cachedRoomList.Add(info.Name, info);
        }
    }

    // 批量更新所有固定条目的人数与状态
    private void UpdateAllRoomItemsStatus()
    {
        foreach (var kvp in roomItemDict)
        {
            string roomName = kvp.Key;
            RoomItem item = kvp.Value;

            // 缓存里有该房间 → 用真实数据更新
            if (cachedRoomList.TryGetValue(roomName, out RoomInfo info))
            {
                bool isFull = info.PlayerCount >= info.MaxPlayers;
                item.SetStatus(info.PlayerCount, info.MaxPlayers, isFull);
            }
            // 缓存里没有 → 说明房间没人、未创建，显示0/2，可加入
            else
            {
                item.SetStatus(0, 2, false);
            }
        }
    }

    // 清空现有UI条目
    private void ClearRoomListUI()
    {
        for (int i = roomListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(roomListContent.GetChild(i).gameObject);
        }
    }

    // 点击加入按钮 → 加入/创建目标房间
    public void JoinTargetRoom(string roomName)
    {
        // 设置昵称
        PhotonNetwork.NickName = "Player";
           
        NameUI.SetActive(false);
        WaitUI.SetActive(true);

        RoomOptions options = new RoomOptions()
        {
            MaxPlayers = 2,
            IsOpen = true,
            IsVisible = true
        };
        // 房间存在就加入，不存在就创建，第一个点击的人自动开房
        PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
    }

    // 成功加入房间
    public override void OnJoinedRoom()
    {
        WaitUI.SetActive(true);
        hasLoadedLevel = false;
    }

    // 加入房间失败（房间满员等）
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
        WaitUI.SetActive(false);
        NameUI.SetActive(true);
        Debug.LogError($"加入失败：{message}");
    }

    // 有玩家进入房间 → 房主检测人数开局
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == 2 && !hasLoadedLevel)
        {
            hasLoadedLevel = true;
            PhotonNetwork.LoadLevel(1);
        }
    }

    // 玩家离开房间 → 房主重置开局标记
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            hasLoadedLevel = false;
        }
    }
}