using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIFaceCamera : MonoBehaviour
{
    void Update()
    {
        // 让UI始终面向本地玩家的摄像机
        transform.rotation=Camera.main.transform.rotation;
    }
}
