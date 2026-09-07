using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Tip : MonoBehaviour
{
    public TMP_Text TipText;
    public HealthBar healthBar;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        TipText.text = "当前血量:"+healthBar._currentHealth;
    }
}
