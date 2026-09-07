using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
public class GameOverText : MonoBehaviourPun
{
    public TMP_Text gameovertext;
    // Start is called before the first frame update
    void Start()
    {
        gameovertext.text="游戏结束,"+photonView.Owner.NickName;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
