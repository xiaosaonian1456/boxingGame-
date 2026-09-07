using UnityEngine;
using Photon.Pun;
using Unity.XR.CoreUtils;
using TMPro;

public class JiaoZhun : MonoBehaviourPunCallbacks
{
    private bool isReady = false;
    private XROrigin xrOrigin;
    public TMP_Text timeText;
    public TMP_Text readyText;

    void Start()
    {
        xrOrigin = FindObjectOfType<XROrigin>();
        // if (!photonView.IsMine && PhotonNetwork.IsConnected)
        // {
        //     // 非本地玩家：仅禁用当前脚本，物体保持激活
        //     this.enabled = false;
        //     readyText.gameObject.SetActive(false);
        // }
    }

    void Update()
    {
        // if (!photonView.IsMine || isReady) return;
        if(isReady) return;

        // 键盘测试键 C
        if (Input.GetKeyDown(KeyCode.C))
        {
            DoCalibration();
        }

        // VR左手X键
        var leftHandDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (leftHandDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed) && pressed)
        {
            DoCalibration();
        }
    }

    void DoCalibration()
    {
        
        isReady = true;
        Debug.Log("校准完成");

        // 激活倒计时UI
        timeText.gameObject.SetActive(true);
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props["IsReady"] = true;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        // 延迟禁用脚本，确保属性同步完成
        Invoke(nameof(HideSelf), 0.2f);
    }

    void HideSelf()
    {
        // 仅禁用当前脚本（等同于Inspector取消勾选脚本）
        this.enabled = false;
        readyText.gameObject.SetActive(false);
    }
}