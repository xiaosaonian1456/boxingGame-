using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Unity.XR.CoreUtils;
public class DeText : MonoBehaviourPun
{
    // Start is called before the first frame update
    void Start()
    {
        if (photonView.IsMine)
        {
            Debug.Log("1");
        }
        else
        {
            Debug.Log("2");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void AlignToAnchor()
    {
        GameObject anchor = GameObject.Find("SharedAnchor");
        XROrigin xrOrigin = FindObjectOfType<XROrigin>();

        // 把自己的头显位置对齐到锚点位置
        Vector3 offset = Camera.main.transform.position - anchor.transform.position;
        xrOrigin.transform.position -= offset;
    }
}
