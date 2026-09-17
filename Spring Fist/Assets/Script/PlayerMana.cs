using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

public class PlayerMana : MonoBehaviourPunCallbacks
{
    [Header("蓝条 UI")]
    [Tooltip("蓝条填充 Image")]
    public Image manaFillImage;
    [Tooltip("蓝条背景 Image")]
    public Image manaBackgroundImage;
    [Tooltip("蓝量数值 Text")]
    public TMP_Text manaText;

    [Header("蓝量设置")]
    [Tooltip("蓝量最大值")]
    public float maxMana = 100f;
    [Tooltip("每秒恢复蓝量")]
    public float manaRegenPerSecond = 5f;

    private float currentMana;

    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;

    void Start()
    {
        // 开局蓝量即为满
        currentMana = maxMana;
        UpdateManaUI();
    }

    void Update()
    {
        

        if (currentMana < maxMana)
        {
            currentMana += manaRegenPerSecond * Time.deltaTime;
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
            UpdateManaUI();
        }
    }

    /// <summary>
    /// 蓝量是否已满
    /// </summary>
    public bool IsManaFull()
    {
        return currentMana >= maxMana;
    }

    /// <summary>
    /// 消耗所有蓝量
    /// </summary>
    public void ConsumeAllMana()
    {
        currentMana = 0f;
        UpdateManaUI();
    }

    /// <summary>
    /// 持续消耗指定蓝量（护盾存续期间逐帧调用），最低扣到 0
    /// </summary>
    public void ConsumeMana(float amount)
    {
        currentMana = Mathf.Max(0f, currentMana - amount);
        UpdateManaUI();
    }

    /// <summary>
    /// 更新蓝条 UI
    /// </summary>
    private void UpdateManaUI()
    {
        if (manaFillImage != null)
        {
            manaFillImage.fillAmount = currentMana / maxMana;
        }

        if (manaText != null)
        {
            manaText.text = currentMana.ToString("F0");
        }
    }
}
