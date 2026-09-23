using UnityEngine;
using Photon.Pun;

public class PlayerCollider : MonoBehaviourPun
{
    private PhotonView _myPhotonView;
    private HealthBar _myHealthBar;

    void Start()
    {
        _myPhotonView = GetComponentInParent<PhotonView>();
        _myHealthBar = GetComponentInParent<HealthBar>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 只有本地玩家的碰撞体执行检测
        if (!_myPhotonView.IsMine) return;

        // 只检测攻击标签的物体
        if (other.CompareTag("Attack"))
        {
            // 获取拳头的PhotonView
            PhotonView fistPhotonView = other.GetComponentInParent<PhotonView>();
            
            // 不能自己打自己
            if (fistPhotonView == null || fistPhotonView.IsMine) return;

            // 四人计分模式（Game4场景）：同队误伤不掉血
            string myTeam = GetTeam(_myPhotonView.Owner);
            string fistTeam = GetTeam(fistPhotonView.Owner);
            if (myTeam != null && fistTeam != null && myTeam == fistTeam) return;

            // ✅ 正确：调用自己的TakeDamage RPC，让自己掉血
            _myPhotonView.RPC(nameof(HealthBar.TakeDamage), RpcTarget.All, 2);
            
            Debug.Log($"被 {fistPhotonView.Owner.NickName} 击中，掉2点血");
        }
    }

    /// <summary>
    /// 读取玩家的队伍自定义属性（四人模式 TeamSelectUI 选座时写入，双人模式为空）
    /// </summary>
    private static string GetTeam(Photon.Realtime.Player player)
    {
        if (player != null && player.CustomProperties.TryGetValue("Team", out object team))
        {
            return team as string;
        }
        return null;
    }
}