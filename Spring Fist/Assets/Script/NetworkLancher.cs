using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using TMPro;

public class NetworkLancher : MonoBehaviourPunCallbacks
{
    [Header("主菜单UI")]
    public GameObject MenuUI;          // 主菜单面板（双人对战/四人对战按钮）

    [Header("UI面板")]
    public GameObject GonnectUIObj;    // 连接中界面
    public GameObject NameUI;          // 昵称+大厅界面
    public GameObject WaitUI;          // 房间等待界面


    [Header("房间列表UI")]
    public Transform roomListContent;  // ScrollView的条目容器
    public GameObject roomItemPrefab;  // 房间条目预制体

    // 两个独立Lobby，双人/四人模式的房间列表互不可见
    private static readonly TypedLobby Lobby2P = new TypedLobby("Lobby_2P", LobbyType.Default);
    private static readonly TypedLobby Lobby4P = new TypedLobby("Lobby_4P", LobbyType.Default);

    private int currentMaxPlayers = 2; // 当前模式人数上限
    private TypedLobby currentLobby;   // 当前模式Lobby

    // 本地缓存服务器推送的房间数据
    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    // 管理5个固定的房间UI条目，用于快速更新状态
    private Dictionary<string, RoomItem> roomItemDict = new Dictionary<string, RoomItem>();
    private bool hasLoadedLevel = false;
    private const int TotalRoomCount = 5; // 固定房间总数
    private TeamSelectUI teamSelectUI;    // 四人模式的红蓝选座面板（进房后动态生成）

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void Start()
    {
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 30;
        // 启动后只显示主菜单，点击模式按钮后才连接Photon
        MenuUI.SetActive(true);
        GonnectUIObj.SetActive(false);
        NameUI.SetActive(false);
        WaitUI.SetActive(false);
    }

    void Update()
    {
        // 修复空引用：只有在房间内才检测人数
        // 仅双人模式按人数自动开局；四人模式改为选座满员后由TeamSelectUI触发MasterStartGame
        if (PhotonNetwork.IsMasterClient
            && currentMaxPlayers == 2
            && PhotonNetwork.CurrentRoom != null
            && PhotonNetwork.CurrentRoom.PlayerCount == currentMaxPlayers
            && !hasLoadedLevel)
        {
            hasLoadedLevel = true;
            PhotonNetwork.LoadLevel(1);
        }
    }

    // 主菜单按钮：双人对战
    public void OnClickTwoPlayerMode()
    {
        SelectMode(2);
    }

    // 主菜单按钮：四人对战
    public void OnClickFourPlayerMode()
    {
        SelectMode(4);
    }

    // 选择模式后开始连接Photon
    private void SelectMode(int maxPlayers)
    {
        currentMaxPlayers = maxPlayers;
        currentLobby = (maxPlayers == 2) ? Lobby2P : Lobby4P;
        MenuUI.SetActive(false);
        GonnectUIObj.SetActive(true);
        PhotonNetwork.ConnectUsingSettings();
    }

    // 返回主菜单按钮：断开连接，OnDisconnected里恢复主菜单显示
    public void OnClickBackToMenu()
    {
        PhotonNetwork.Disconnect();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        CloseTeamSelectUI();
        MenuUI.SetActive(true);
        GonnectUIObj.SetActive(false);
        NameUI.SetActive(false);
        WaitUI.SetActive(false);
    }

    // 连接Master成功 → 加入当前模式的大厅
    public override void OnConnectedToMaster()
    {
        GonnectUIObj.SetActive(false);
        PhotonNetwork.JoinLobby(currentLobby);
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

            item.Init(roomName, currentMaxPlayers, JoinTargetRoom);
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
            // 缓存里没有 → 说明房间没人、未创建，显示0/上限，可加入
            else
            {
                item.SetStatus(0, currentMaxPlayers, false);
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
            MaxPlayers = (byte)currentMaxPlayers,
            IsOpen = true,
            IsVisible = true
        };
        // 房间存在就加入，不存在就创建，第一个点击的人自动开房
        PhotonNetwork.JoinOrCreateRoom(roomName, options, currentLobby);
    }

    // 成功加入房间
    public override void OnJoinedRoom()
    {
        hasLoadedLevel = false;
        if (currentMaxPlayers == 4)
        {
            // 四人模式：隐藏等待界面，显示红蓝方选座界面，人满自动开局
            WaitUI.SetActive(false);
            ShowTeamSelectUI();
        }
        else
        {
            // 双人模式：保持原有等待界面逻辑
            WaitUI.SetActive(true);
        }
    }

    // 动态生成四人模式选座面板（挂在WaitUI同父级Canvas下，继承其渲染与交互设置）
    private void ShowTeamSelectUI()
    {
        CloseTeamSelectUI();
        // 复用现有UI的TMP字体，保证中文正常显示
        TMP_Text refText = WaitUI.GetComponentInChildren<TMP_Text>(true);
        GameObject panelObj = new GameObject("TeamSelectUI", typeof(RectTransform), typeof(TeamSelectUI));
        panelObj.transform.SetParent(WaitUI.transform.parent, false);
        panelObj.transform.SetAsLastSibling();
        teamSelectUI = panelObj.GetComponent<TeamSelectUI>();
        teamSelectUI.Build(this, refText != null ? refText.font : null);
    }

    private void CloseTeamSelectUI()
    {
        if (teamSelectUI != null)
        {
            Destroy(teamSelectUI.gameObject);
            teamSelectUI = null;
        }
    }

    // 四人模式选座满员后由TeamSelectUI调用（仅房主），加载四人计分场景Game4
    public void MasterStartGame()
    {
        if (!PhotonNetwork.IsMasterClient || hasLoadedLevel) return;
        hasLoadedLevel = true;
        PhotonNetwork.LoadLevel(3);
    }

    public override void OnLeftRoom()
    {
        CloseTeamSelectUI();
    }

    // 加入房间失败（房间满员等）
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
        WaitUI.SetActive(false);
        NameUI.SetActive(true);
        Debug.LogError($"加入失败：{message}");
    }

    // 有玩家进入房间 → 房主检测人数开局（仅双人模式；四人模式由TeamSelectUI按选座情况触发）
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient && currentMaxPlayers == 2 && PhotonNetwork.CurrentRoom.PlayerCount == currentMaxPlayers && !hasLoadedLevel)
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
