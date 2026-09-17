using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// 玩家子弹特效分配器：每个玩家进房（注册）时随机分配一种子弹特效预制体，
/// 通过Photon玩家自定义属性同步给所有客户端；同一玩家左右拳共用同一种。
/// 在场景中放一个空物体挂本脚本，并在Inspector中配置 projectileVariants 列表。
/// </summary>
public class FistSkinManager : MonoBehaviourPunCallbacks
{
    public static FistSkinManager Instance { get; private set; }

    [Tooltip("可分配的子弹特效预制体列表，每个玩家进房时随机分配一种（左右拳共用）")]
    public GameObject[] projectileVariants;

    private const string SKIN_KEY = "FistSkin";
    private int _localIndex = -1; // 本地玩家分配到的下标（-1 = 未分配）

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnJoinedRoom()
    {
        // 进房即分配（=注册后自动随机分配）
        AssignLocalSkin();
    }

    /// <summary>
    /// 获取本地玩家分配到的特效下标，未分配时立即随机分配并同步给其他客户端
    /// </summary>
    public int GetLocalSkinIndex()
    {
        if (_localIndex < 0)
        {
            AssignLocalSkin();
        }
        return _localIndex;
    }

    private void AssignLocalSkin()
    {
        if (projectileVariants == null || projectileVariants.Length == 0) return;

        // 已分配过（如断线重连）直接复用，保证同一玩家始终是同一种特效
        object existing;
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(SKIN_KEY, out existing))
        {
            _localIndex = (int)existing;
            return;
        }

        _localIndex = PickIndex();
        if (_localIndex >= 0)
        {
            Debug.Log($"[FistSkinManager] 本玩家分配到子弹特效：{projectileVariants[_localIndex].name}");
        }

        // 写入玩家自定义属性，自动同步给房间内所有客户端（含后进房的）
        if (PhotonNetwork.InRoom)
        {
            Hashtable props = new Hashtable { { SKIN_KEY, _localIndex } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }
    }

    /// <summary>
    /// 随机挑选下标：尽量避开其他玩家已分配的，列表数量不够分时才允许重复
    /// </summary>
    private int PickIndex()
    {
        List<int> free = new List<int>();
        for (int i = 0; i < projectileVariants.Length; i++)
        {
            free.Add(i);
        }

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p == PhotonNetwork.LocalPlayer) continue;
            object v;
            if (p.CustomProperties.TryGetValue(SKIN_KEY, out v))
            {
                free.Remove((int)v);
            }
        }

        if (free.Count > 0)
        {
            return free[Random.Range(0, free.Count)];
        }
        // 特效种类不够所有玩家分，允许重复，纯随机
        return Random.Range(0, projectileVariants.Length);
    }

    /// <summary>
    /// 按下标取预制体，下标非法返回null（调用方用兜底预制体）
    /// </summary>
    public GameObject GetPrefab(int index)
    {
        if (projectileVariants == null || index < 0 || index >= projectileVariants.Length)
        {
            return null;
        }
        return projectileVariants[index];
    }
}
