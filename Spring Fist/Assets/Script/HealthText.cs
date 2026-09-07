using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class HealthText : MonoBehaviourPun
{
    private HealthBar healthBar;
    public TMP_Text healthtext;
    public Image healthImage; // 血条填充图片

    void Start()
    {
        // 联机游戏中只能看到自己的血条
        if (photonView.IsMine && PhotonNetwork.IsConnected)
        {
            FindLocalHealthBar();

            if (healthtext != null)
                healthtext.text = "100";

            UpdateHealthImage(1f);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // 如果还没找到本地玩家的 HealthBar，持续搜索（玩家预制体可能稍后才生成）
        if (healthBar == null)
        {
            FindLocalHealthBar();
        }

        if (healthBar != null)
        {
            float ratio = healthBar._currentHealth / healthBar.maxHealth;

            if (healthtext != null)
                healthtext.text = "" + healthBar._currentHealth;

            UpdateHealthImage(ratio);
        }
    }

    /// <summary>
    /// 在场景中查找属于本地玩家的 HealthBar
    /// </summary>
    private void FindLocalHealthBar()
    {
        HealthBar[] healthBars = FindObjectsOfType<HealthBar>();
        foreach (HealthBar hb in healthBars)
        {
            PhotonView pv = hb.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                healthBar = hb;
                break;
            }
        }
    }

    private void UpdateHealthImage(float ratio)
    {
        if (healthImage != null)
        {
            healthImage.fillAmount = Mathf.Clamp01(ratio);
        }
    }
}
