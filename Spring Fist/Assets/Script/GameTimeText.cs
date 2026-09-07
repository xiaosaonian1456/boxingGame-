using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class GameTimeText : MonoBehaviourPunCallbacks
{
    public float startTime = 180f;   // 每局对战时长（秒）
    public float intervalTime = 5f;  // 局间倒计时（秒）
    public int totalRounds = 3;      // 总对局数（三局两胜）

    private float currentTime;
    private bool isEnd = false;          // 当前局是否已结束
    private bool _gameStarted = false;
    private double _startServerTime = -1;

    private int currentRound = 0;        // 当前局数（0=未开始）
    private int localWins = 0;           // 本地玩家胜场
    private int remoteWins = 0;          // 对方玩家胜场

    private bool _waitingNextRound = false;   // 是否处于局间倒计时
    private double _nextRoundServerTime = -1; // 下一局开始的服务器时间
    private string _roundResultText = "";     // 本局结果文本，局间倒计时时显示
    private bool _isGameOver = false;         // 是否为正常打完三局的结算（断线等异常离开不算）

    public TMP_Text text;
    public Image resultImage; // 血条 Fill 图片引用

    /// <summary>
    /// 当前是否处于有效对局中（开局后、非局间倒计时、本局未结束）
    /// 局间阶段和终局结算阶段攻击不应造成伤害
    /// </summary>
    public bool IsRoundActive
    {
        get { return _gameStarted && !_waitingNextRound && !isEnd; }
    }

    void Awake()
    {
        _gameStarted = false;
        isEnd = false;
        currentTime = 0;
        _startServerTime = -1;
        currentRound = 0;
        localWins = 0;
        remoteWins = 0;
        _waitingNextRound = false;
        _nextRoundServerTime = -1;
        _roundResultText = "";
        _isGameOver = false;
    }

    void Start()
    {
        // 初始化显示
        if (text != null)
        {
            text.text = Mathf.RoundToInt(startTime).ToString();
        }

        // 只有房主判断并开始倒计时
        if (PhotonNetwork.IsMasterClient)
        {
            TryStartCountdown();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // 有玩家加入时，房主重新检查人数
        if (PhotonNetwork.IsMasterClient)
        {
            TryStartCountdown();
        }
    }

    /// <summary>
    /// 只有房间人数==2时，房主才会触发第一局倒计时
    /// </summary>
    void TryStartCountdown()
    {
        if (_gameStarted) return;
        if (PhotonNetwork.CurrentRoom == null) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount != 2) return;

        _gameStarted = true;

        // 用服务器时间作为统一的倒计时起点，所有客户端同步
        photonView.RPC("RPC_StartRound", RpcTarget.AllBuffered, 1, PhotonNetwork.Time);
    }

    /// <summary>
    /// 开始某一局：重置血条，若开始时间在未来则先进入局间倒计时
    /// </summary>
    [PunRPC]
    void RPC_StartRound(int round, double roundStartServerTime)
    {
        currentRound = round;
        isEnd = false;
        _gameStarted = true;

        // 每局开始重置所有血条为满血
        foreach (HealthBar hb in FindObjectsOfType<HealthBar>())
        {
            hb.ResetForNewRound();
        }

        if (roundStartServerTime > PhotonNetwork.Time)
        {
            // 局间倒计时阶段，等待到点后再开始计时
            _waitingNextRound = true;
            _nextRoundServerTime = roundStartServerTime;
        }
        else
        {
            _waitingNextRound = false;
            _startServerTime = roundStartServerTime;
        }
        Debug.Log("第 " + round + " 局开始，服务器开始时间：" + roundStartServerTime);
    }

    void Update()
    {
        if (!_gameStarted || text == null) return;

        // 局间倒计时：显示本局结果和下一局倒计时，到点后进入下一局计时
        if (_waitingNextRound)
        {
            double remain = _nextRoundServerTime - PhotonNetwork.Time;
            if (remain > 0)
            {
                text.text = _roundResultText + "\n" + Mathf.CeilToInt((float)remain) + "秒后进入下一局";
            }
            else
            {
                _waitingNextRound = false;
                _startServerTime = _nextRoundServerTime;
            }
            return;
        }

        if (isEnd) return;

        // 用 PhotonNetwork.Time 计算已经过去的时间，保证所有客户端显示一致
        double elapsed = PhotonNetwork.Time - _startServerTime;
        currentTime = Mathf.Max(0f, startTime - (float)elapsed);
        text.text = Mathf.RoundToInt(currentTime).ToString();

        if (currentTime <= 0)
        {
            EndRound();
        }
    }

    /// <summary>
    /// 结束当前局：记录胜负，前2局进入局间倒计时，第3局进行总结算
    /// </summary>
    void EndRound()
    {
        if (isEnd) return;
        isEnd = true;
        Debug.Log("第 " + currentRound + " 局结束");

        string roundResult = JudgeRound();

        if (currentRound < totalRounds)
        {
            // 前2局：显示本局结果，房主广播下一局开始时间（局间倒计时）
            _roundResultText = "第" + currentRound + "局：" + roundResult;
            text.text = _roundResultText;

            if (PhotonNetwork.IsMasterClient)
            {
                double nextRoundTime = PhotonNetwork.Time + intervalTime;
                photonView.RPC("RPC_StartRound", RpcTarget.AllBuffered, currentRound + 1, nextRoundTime);
            }
        }
        else
        {
            // 第3局结束：三局两胜总结算
            if (localWins > remoteWins)
            {
                text.text = "你赢了（" + localWins + ":" + remoteWins + "）";
            }
            else if (localWins < remoteWins)
            {
                text.text = "你输了（" + localWins + ":" + remoteWins + "）";
            }
            else
            {
                text.text = "平局（" + localWins + ":" + remoteWins + "）";
            }

            // 由房主统一广播切换场景的服务器时间，保证所有客户端同步
            if (PhotonNetwork.IsMasterClient)
            {
                double switchTime = PhotonNetwork.Time + 3.0;
                photonView.RPC("RPC_GameOver", RpcTarget.AllBuffered, switchTime);
            }
        }
    }

    /// <summary>
    /// 根据本地与非本地玩家血量判定本局胜负并记录胜场，返回结果文本
    /// </summary>
    private string JudgeRound()
    {
        HealthBar remoteHealthBar = FindRemoteHealthBar();
        if (resultImage == null || remoteHealthBar == null)
        {
            return "平局";
        }

        float localHealthRatio = resultImage.fillAmount;
        float remoteHealthRatio = remoteHealthBar._currentHealth / remoteHealthBar.maxHealth;

        if (localHealthRatio > remoteHealthRatio)
        {
            localWins++;
            return "你赢了";
        }
        if (localHealthRatio < remoteHealthRatio)
        {
            remoteWins++;
            return "你输了";
        }
        return "平局";
    }

    /// <summary>
    /// 在场景中查找属于非本地玩家的 HealthBar
    /// </summary>
    private HealthBar FindRemoteHealthBar()
    {
        HealthBar[] healthBars = FindObjectsOfType<HealthBar>();
        foreach (HealthBar hb in healthBars)
        {
            PhotonView pv = hb.GetComponent<PhotonView>();
            if (pv != null && !pv.IsMine)
            {
                return hb;
            }
        }
        return null;
    }

    [PunRPC]
    void RPC_GameOver(double switchTime)
    {
        _isGameOver = true;
        StartCoroutine(DelaySwitchScene(switchTime));
    }

    /// <summary>
    /// 由死亡触发的本局结算（流程与时间到点一致）
    /// </summary>
    [PunRPC]
    public void RPC_GameOverByDeath()
    {
        EndRound();
    }

    System.Collections.IEnumerator DelaySwitchScene(double switchTime)
    {
        double waitTime = switchTime - PhotonNetwork.Time;
        if (waitTime > 0)
        {
            yield return new WaitForSeconds((float)waitTime);
        }
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        // 只有正常打完三局收到结算RPC才切结算场景；
        // 断线等异常导致的离开房间（Photon断线会自动回调OnLeftRoom）不应进结算场景
        if (_isGameOver)
        {
            SceneManager.LoadScene(2);
        }
        else
        {
            Debug.LogWarning("非正常结算离开房间（可能是断线），返回登录场景");
            SceneManager.LoadScene(0);
        }
    }

    /// <summary>
    /// 断线回调：打印断线原因，便于排查（断线会先触发OnLeftRoom）
    /// </summary>
    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        Debug.LogError("与Photon服务器断开连接，原因：" + cause);
    }
}
