using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class RoomItem : MonoBehaviour
{
    public TMP_Text roomNameText;    // 房间名称文本
    public TMP_Text playerCountText; // 人数文本 如 1/2
    public Button joinButton;        // 加入按钮

    private string currentRoomName;
    private Action<string> onJoinClick;

    /// <summary>
    /// 初始化固定房间条目（只设置房间名和点击回调）
    /// </summary>
    public void Init(string roomName, Action<string> joinCallback)
    {
        currentRoomName = roomName;
        onJoinClick = joinCallback;
        roomNameText.text = roomName;
        // 默认状态：0人，可加入
        SetStatus(0, 2, false);
    }

    /// <summary>
    /// 更新房间人数与按钮状态
    /// </summary>
    public void SetStatus(int currentCount, int maxCount, bool isFull)
    {
        playerCountText.text = "房间人数:" + $"{currentCount}/{maxCount}";
        joinButton.interactable = !isFull;
    }

    // 加入按钮点击事件
    public void OnJoinButtonClick()
    {
        onJoinClick?.Invoke(currentRoomName);
    }
}