using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

/// <summary>
/// 四人模式（Game4场景）的计分制回合计时器：
/// - 打中对方队伍角色 +1 分（ProjectileFist 命中时 RPC_AddHit，被盾牌防御不加分）
/// - 每回合 80 秒，共 5 回合，打满 5 回合再结算
/// - 回合结束比较红蓝双方本回合击中数，高的一方大局 +1，平局双方各 +1
/// - 5 回合结束按大局分判总胜负（5局3胜的完整打满版）
/// 血量仅作显示，血量归零不触发回合结束。
/// 显示对应计分板图片的5个区域：左侧红大区域=红方本回合击中数(redScoreText)，
/// 中间偏左红小框=红方大局分(redWinsText)，中间盾牌=倒计时(text)，
/// 中间偏右蓝小框=蓝方大局分(blueWinsText)，右侧蓝大区域=蓝方本回合击中数(blueScoreText)。
/// 回合结算/终局结果等长文本显示在计分板下方的 roundResultText（血条和体力条之间）。
/// </summary>
public class GameTimeText4P : MonoBehaviourPunCallbacks
{
    public float startTime = 80f;    // 每回合对战时长（秒）
    public float intervalTime = 5f;  // 回合间倒计时（秒）
    public int totalRounds = 5;      // 总回合数（五局三胜，打满结算）

    private float currentTime;
    private bool isEnd = false;          // 当前回合是否已结束
    private bool _gameStarted = false;
    private double _startServerTime = -1;

    private int currentRound = 0;        // 当前回合数（0=未开始）
    private int redHits = 0;             // 本回合红方击中数
    private int blueHits = 0;            // 本回合蓝方击中数
    private int redWins = 0;             // 红方大局分
    private int blueWins = 0;            // 蓝方大局分

    private bool _waitingNextRound = false;   // 是否处于回合间倒计时
    private double _nextRoundServerTime = -1; // 下一回合开始的服务器时间
    private string _roundResultText = "";     // 本回合结果文本，回合间倒计时时显示
    private bool _isGameOver = false;         // 是否为正常打完五局的结算（断线等异常离开不算）

    public TMP_Text text;            // 中间文本：倒计时（计分板图片中间盾牌区域）

    [Header("计分板比分文本")]
    public TMP_Text redScoreText;    // 红方本回合击中数（计分板图片左侧红色大区域）
    public TMP_Text redWinsText;     // 红方大局分（计分板图片中间偏左红色小框）
    public TMP_Text blueScoreText;   // 蓝方本回合击中数（计分板图片右侧蓝色大区域）
    public TMP_Text blueWinsText;    // 蓝方大局分（计分板图片中间偏右蓝色小框）

    [Header("结算文本")]
    public TMP_Text roundResultText; // 回合结算/终局结果（计分板下方，血条和体力条之间）

    [Header("回合数文本")]
    public TMP_Text roundText;       // 当前回合/总回合（倒计时下方，如 1/5）

    [Header("复活提示文本")]
    public TMP_Text respawnText;     // 复活中提示（与回合结算同一位置，两者不会同时显示）

    private HealthBar _localHealthBar; // 本地玩家的血条（缓存）

    /// <summary>
    /// 当前是否处于有效对局中（开局后、非回合间倒计时、本回合未结束）
    /// 回合间阶段和终局结算阶段攻击不应造成伤害/计分
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
        redHits = 0;
        blueHits = 0;
        redWins = 0;
        blueWins = 0;
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
        RefreshScoreTexts();
        if (roundText != null)
        {
            roundText.text = "";
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
    /// 只有房间人数==4时，房主才会触发第一回合倒计时
    /// </summary>
    void TryStartCountdown()
    {
        if (_gameStarted) return;
        if (PhotonNetwork.CurrentRoom == null) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount != 4) return;

        _gameStarted = true;

        // 用服务器时间作为统一的倒计时起点，所有客户端同步
        photonView.RPC("RPC_StartRound", RpcTarget.AllBuffered, 1, PhotonNetwork.Time);
    }

    /// <summary>
    /// 开始某一回合：重置血条和本回合击中数，若开始时间在未来则先进入回合间倒计时
    /// </summary>
    [PunRPC]
    void RPC_StartRound(int round, double roundStartServerTime)
    {
        currentRound = round;
        isEnd = false;
        _gameStarted = true;
        redHits = 0;
        blueHits = 0;
        RefreshScoreTexts();

        // 新回合开始，清空上一回合的结算文字
        if (roundResultText != null)
        {
            roundResultText.text = "";
        }

        // 每回合开始重置所有血条为满血（血量仅作显示）
        foreach (HealthBar hb in FindObjectsOfType<HealthBar>())
        {
            hb.ResetForNewRound();
        }

        if (roundStartServerTime > PhotonNetwork.Time)
        {
            // 回合间倒计时阶段，等待到点后再开始计时
            _waitingNextRound = true;
            _nextRoundServerTime = roundStartServerTime;
        }
        else
        {
            _waitingNextRound = false;
            _startServerTime = roundStartServerTime;
            // 立即开始的回合（如第1回合）直接刷新回合数
            UpdateRoundText();
        }
        Debug.Log("第 " + round + " 回合开始，服务器开始时间：" + roundStartServerTime);
    }

    /// <summary>
    /// 刷新回合数显示（如 1/5），只在回合正式开始时调用
    /// </summary>
    void UpdateRoundText()
    {
        if (roundText != null)
        {
            roundText.text = currentRound + "/" + totalRounds;
        }
    }

    /// <summary>
    /// 击中计分：由发射者客户端在命中对方队伍角色时调用（被盾牌防御不触发）
    /// </summary>
    [PunRPC]
    public void RPC_AddHit(string team)
    {
        // 局间/终局阶段的迟到RPC不计分
        if (!IsRoundActive) return;

        if (team == "Red")
        {
            redHits++;
        }
        else if (team == "Blue")
        {
            blueHits++;
        }
        RefreshScoreTexts();
    }

    void Update()
    {
        // 复活提示常驻刷新（本地玩家死亡进入复活倒计时时显示）
        UpdateRespawnText();

        if (!_gameStarted || text == null) return;

        // 回合间倒计时：盾牌显示秒数，结算文字显示在下方
        if (_waitingNextRound)
        {
            double remain = _nextRoundServerTime - PhotonNetwork.Time;
            if (remain > 0)
            {
                text.text = Mathf.CeilToInt((float)remain).ToString();
                if (roundResultText != null)
                {
                    roundResultText.text = _roundResultText + "\n" + Mathf.CeilToInt((float)remain) + "秒后进入下一回合";
                }
            }
            else
            {
                _waitingNextRound = false;
                _startServerTime = _nextRoundServerTime;
                // 回合正式开始：刷新回合数，并清空回合间显示的结算文字
                UpdateRoundText();
                if (roundResultText != null)
                {
                    roundResultText.text = "";
                }
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
    /// 刷新计分板显示：大区域为本回合击中数，小框为大局分，中间盾牌为倒计时
    /// </summary>
    void RefreshScoreTexts()
    {
        if (redScoreText != null)
        {
            redScoreText.text = redHits.ToString();
        }
        if (redWinsText != null)
        {
            redWinsText.text = redWins.ToString();
        }
        if (blueScoreText != null)
        {
            blueScoreText.text = blueHits.ToString();
        }
        if (blueWinsText != null)
        {
            blueWinsText.text = blueWins.ToString();
        }
    }

    /// <summary>
    /// 显示结算长文本：优先显示在下方结算文本框，未配置时退回中间盾牌文本
    /// </summary>
    void ShowResultText(string content)
    {
        if (roundResultText != null)
        {
            roundResultText.text = content;
        }
        else
        {
            text.text = content;
        }
    }

    /// <summary>
    /// 查找本地玩家的血条（玩家预制体由Photon异步生成，需要时持续查找）
    /// </summary>
    void FindLocalHealthBar()
    {
        foreach (HealthBar hb in FindObjectsOfType<HealthBar>())
        {
            if (hb.photonView.IsMine)
            {
                _localHealthBar = hb;
                break;
            }
        }
    }

    /// <summary>
    /// 刷新复活提示：本地玩家处于复活倒计时中时显示（与回合结算同一位置），否则清空。
    /// 回合结束时玩家会被立即复活，两个文本不会同时显示。
    /// </summary>
    void UpdateRespawnText()
    {
        if (respawnText == null) return;

        if (_localHealthBar == null)
        {
            FindLocalHealthBar();
        }

        if (_localHealthBar != null && _localHealthBar.IsRespawning)
        {
            respawnText.text = "复活中 " + Mathf.CeilToInt(_localHealthBar.RespawnRemaining) + " 秒";
        }
        else if (respawnText.text.Length > 0)
        {
            respawnText.text = "";
        }
    }

    /// <summary>
    /// 结束当前回合：按击中数判定大局得分，前4回合进入回合间倒计时，第5回合总结算
    /// </summary>
    void EndRound()
    {
        if (isEnd) return;
        isEnd = true;
        Debug.Log("第 " + currentRound + " 回合结束，红 " + redHits + " : " + blueHits + " 蓝");

        string roundResult = JudgeRound();
        RefreshScoreTexts();

        // 回合结束：立即复活复活倒计时中的本地玩家，防止复活提示与结算文字重叠
        if (_localHealthBar == null)
        {
            FindLocalHealthBar();
        }
        if (_localHealthBar != null && _localHealthBar.IsRespawning)
        {
            _localHealthBar.RespawnImmediately();
        }

        if (currentRound < totalRounds)
        {
            // 前4回合：显示本回合结果，房主广播下一回合开始时间（回合间倒计时）
            _roundResultText = "第" + currentRound + "回合：" + roundResult
                + "\n大局 红 " + redWins + " : " + blueWins + " 蓝";
            ShowResultText(_roundResultText);

            if (PhotonNetwork.IsMasterClient)
            {
                double nextRoundTime = PhotonNetwork.Time + intervalTime;
                photonView.RPC("RPC_StartRound", RpcTarget.AllBuffered, currentRound + 1, nextRoundTime);
            }
        }
        else
        {
            // 第5回合结束：按大局分总结算
            string myTeam = GetLocalTeam();
            if (redWins == blueWins)
            {
                ShowResultText("平局（红 " + redWins + " : " + blueWins + " 蓝）");
            }
            else
            {
                string winTeam = redWins > blueWins ? "Red" : "Blue";
                string scoreText = "（红 " + redWins + " : " + blueWins + " 蓝）";
                if (myTeam == null)
                {
                    ShowResultText((winTeam == "Red" ? "红方" : "蓝方") + "胜利" + scoreText);
                }
                else
                {
                    ShowResultText((winTeam == myTeam ? "你方胜利" : "你方失败") + scoreText);
                }
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
    /// 比较本回合红蓝击中数并记录大局分（平局双方各+1），返回结果文本
    /// </summary>
    private string JudgeRound()
    {
        if (redHits > blueHits)
        {
            redWins++;
            return "红方得分";
        }
        if (redHits < blueHits)
        {
            blueWins++;
            return "蓝方得分";
        }
        // 回合平局：双方各加1分
        redWins++;
        blueWins++;
        return "平局，双方各得1分";
    }

    /// <summary>
    /// 读取本地玩家的队伍（TeamSelectUI 在选座时写入的自定义属性）
    /// </summary>
    private string GetLocalTeam()
    {
        if (PhotonNetwork.LocalPlayer != null
            && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Team", out object team))
        {
            return team as string;
        }
        return null;
    }

    [PunRPC]
    void RPC_GameOver(double switchTime)
    {
        _isGameOver = true;
        StartCoroutine(DelaySwitchScene(switchTime));
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

        // 只有正常打完五局收到结算RPC才切结算场景；
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
