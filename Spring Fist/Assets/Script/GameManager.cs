using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;

    [Header("游戏设置")]
    public int gameOverSceneIndex = 2;
    public float sceneLoadDelay = 0.5f; // 死亡后延迟切换场景

    private bool _isGameOver = false;

    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ✅ 只有主机会调用这个方法
    public void OnPlayerDied(Player deadPlayer)
    {
        if (_isGameOver) return;

        _isGameOver = true;
        Debug.Log($"游戏结束！{deadPlayer.NickName} 被击败了");

       
        // 延迟后切换场景
        StartCoroutine(LoadGameOverSceneCoroutine());
    }

    IEnumerator LoadGameOverSceneCoroutine()
    {
        // 给玩家一点时间看游戏结束UI
        yield return new WaitForSeconds(sceneLoadDelay);

        // ✅ 正确：只有主机调用LoadLevel，所有客户端同步加载
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(gameOverSceneIndex);
        }
    }

  

    // 监听玩家离开事件
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);

        if (_isGameOver) return;

        // 如果游戏中有人离开，直接结束游戏
        if (PhotonNetwork.IsMasterClient)
        {
            OnPlayerDied(otherPlayer);
        }
    }
}