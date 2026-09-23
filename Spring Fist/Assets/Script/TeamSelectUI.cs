using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 四人模式：进房后的红蓝方选座面板。
// 界面在运行时动态生成，挂在等待界面（WaitUI）的同父级Canvas下，
// 席位归属通过房间自定义属性 + CAS（期望值为null表示"必须为空"）原子抢占，避免两人抢同一席位。
public class TeamSelectUI : MonoBehaviourPunCallbacks
{
    public static readonly string[] SeatKeys = { "Seat_R1", "Seat_R2", "Seat_B1", "Seat_B2" };
    private static readonly string[] SeatNames = { "红方席位 1", "红方席位 2", "蓝方席位 1", "蓝方席位 2" };
    private static readonly Color RedSeatColor = new Color(0.55f, 0.12f, 0.12f);
    private static readonly Color BlueSeatColor = new Color(0.12f, 0.22f, 0.55f);

    private NetworkLancher launcher;
    private TMP_FontAsset font;
    private readonly Button[] seatButtons = new Button[4];
    private readonly TMP_Text[] seatLabels = new TMP_Text[4];

    // 由NetworkLancher创建后调用：构建面板内容并刷新一次席位状态
    public void Build(NetworkLancher owner, TMP_FontAsset fontAsset)
    {
        launcher = owner;
        font = fontAsset;

        RectTransform rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.9f);

        CreateText(transform, "Title", "四人模式：请选择红蓝方席位（人满自动开始）",
            new Vector2(0f, 260f), new Vector2(900f, 80f), 44);

        CreateText(transform, "RedLabel", "红方",
            new Vector2(-320f, 130f), new Vector2(420f, 60f), 38).color = new Color(1f, 0.4f, 0.4f);
        CreateText(transform, "BlueLabel", "蓝方",
            new Vector2(320f, 130f), new Vector2(420f, 60f), 38).color = new Color(0.4f, 0.55f, 1f);

        for (int i = 0; i < 4; i++)
        {
            Vector2 pos = new Vector2(i < 2 ? -320f : 320f, i % 2 == 0 ? 0f : -140f);
            Color color = i < 2 ? RedSeatColor : BlueSeatColor;
            CreateSeatButton(i, pos, color);
        }

        Refresh();
    }

    private TMP_Text CreateText(Transform parent, string name, string content, Vector2 pos, Vector2 size, int fontSize)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)obj.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TMP_Text text = obj.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void CreateSeatButton(int index, Vector2 pos, Color color)
    {
        GameObject obj = new GameObject("SeatButton_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(transform, false);
        RectTransform rt = (RectTransform)obj.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(420f, 100f);

        Image img = obj.GetComponent<Image>();
        img.color = color;

        Button btn = obj.GetComponent<Button>();
        int captured = index;
        btn.onClick.AddListener(() => OnSeatButtonClicked(captured));

        TMP_Text label = CreateText(obj.transform, "Label", "", Vector2.zero, rt.sizeDelta, 32);
        RectTransform labelRt = (RectTransform)label.transform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        seatButtons[index] = btn;
        seatLabels[index] = label;
    }

    // 席位按钮点击：空位→抢占（若已占其他席位则先释放）；自己的席位→释放
    private void OnSeatButtonClicked(int seatIndex)
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        string key = SeatKeys[seatIndex];
        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        int holder = GetSeatHolder(key);

        if (holder == myActor)
        {
            // 释放自己的席位
            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new Hashtable { { key, null } },
                new Hashtable { { key, myActor } });
            return;
        }
        if (holder != -1) return; // 已被别人占用

        // 先释放自己已占的席位，再抢占新席位
        int oldSeat = GetMySeatIndex();
        if (oldSeat != -1)
        {
            string oldKey = SeatKeys[oldSeat];
            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new Hashtable { { oldKey, null } },
                new Hashtable { { oldKey, myActor } });
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { key, myActor } },
            new Hashtable { { key, null } });
    }

    // 刷新席位显示；房主顺带清理已离开玩家的席位并检测是否可开局
    private void Refresh()
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        for (int i = 0; i < 4; i++)
        {
            int holder = GetSeatHolder(SeatKeys[i]);
            string desc = holder == -1 ? "空位" : holder == myActor ? "我" : $"玩家 {holder}";
            seatLabels[i].text = $"{SeatNames[i]}\n{desc}";
            seatLabels[i].color = holder == myActor ? Color.yellow : Color.white;
            seatButtons[i].interactable = holder == -1 || holder == myActor;
        }

        SyncMyTeamProperty();

        if (PhotonNetwork.IsMasterClient)
        {
            ScrubAbsentSeats();
            TryStart();
        }
    }

    // 把所选队伍同步到玩家自定义属性"Team"（Red/Blue），供游戏场景读取
    private void SyncMyTeamProperty()
    {
        int mySeat = GetMySeatIndex();
        string team = mySeat == -1 ? null : (mySeat < 2 ? "Red" : "Blue");
        Photon.Realtime.Player p = PhotonNetwork.LocalPlayer;
        object current = p.CustomProperties.TryGetValue("Team", out object v) ? v : null;
        if ((string)current == team) return;
        p.SetCustomProperties(new Hashtable { { "Team", team } });
    }

    // 房主开局条件：房间满4人且4个席位都被房内玩家占据
    private void TryStart()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount < 4) return;
        foreach (string key in SeatKeys)
        {
            int holder = GetSeatHolder(key);
            if (holder == -1 || !IsActorInRoom(holder)) return;
        }
        launcher.MasterStartGame();
    }

    // 房主清理：席位占用者已不在房间时，CAS回收该席位
    private void ScrubAbsentSeats()
    {
        foreach (string key in SeatKeys)
        {
            int holder = GetSeatHolder(key);
            if (holder != -1 && !IsActorInRoom(holder))
            {
                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new Hashtable { { key, null } },
                    new Hashtable { { key, holder } });
            }
        }
    }

    private int GetSeatHolder(string key)
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) && value is int actor)
            return actor;
        return -1;
    }

    private int GetMySeatIndex()
    {
        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        for (int i = 0; i < 4; i++)
        {
            if (GetSeatHolder(SeatKeys[i]) == myActor) return i;
        }
        return -1;
    }

    private bool IsActorInRoom(int actorNumber)
    {
        foreach (Photon.Realtime.Player p in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (p.ActorNumber == actorNumber) return true;
        }
        return false;
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Refresh();
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        Refresh();
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Refresh();
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        Refresh();
    }

    public override void OnLeftRoom()
    {
        Destroy(gameObject);
    }
}
