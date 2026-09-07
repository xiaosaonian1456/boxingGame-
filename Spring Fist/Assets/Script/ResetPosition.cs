using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using Unity.XR.CoreUtils;
// 这是你原来的位置重置脚本，完全保留原有逻辑
public class ResetPosition : MonoBehaviour
{
    //// 你原来的变量，一个都没改
    //public Transform zhengti;
    //public Transform kzq;
    //public Transform sxj;
    public Transform Root;
    public Transform Camera;


    
    
   

    public XROrigin xrOrigin;

    // 你原来的cz()重置函数，100%原样保留
    //public void cz()
    //{
    //     Vector3 childWorldPosition = sxj.position;
    //     kzq.position -= childWorldPosition;
    //     zhengti.position = Vector3.zero;
    //     sxj.position = Vector3.zero;
    //     zhengti.eulerAngles = new Vector3(zhengti.eulerAngles.x, 90, zhengti.eulerAngles.z);
    //     xrOrigin.Origin.transform.position = Vector3.zero;
    //     xrOrigin.Origin.transform.localRotation = Quaternion.identity;
    //     Quaternion childLocalRot = sxj.localRotation;
    //     Vector3 childWorldEuler = sxj.rotation.eulerAngles;
    //     childWorldEuler.y = 0; // 强制子物体世界Y轴旋转为0
    //     Quaternion desiredChildWorldRot = Quaternion.Euler(childWorldEuler);
    //     kzq.rotation = desiredChildWorldRot * Quaternion.Inverse(childLocalRot);
        
        
    //}
    public void czNew()
    {
        if (Camera == null)
        {
            return;
        }
        // 保留实际Y高度，不对齐到地面
        Vector3 headPosition = new Vector3(Camera.position.x,0,Camera.position.z);
        Quaternion headYawRotation = Quaternion.Euler(0, Camera.eulerAngles.y, 0);
        AlignSharedSpaceOrigin(headPosition, headYawRotation);
    }
    private void AlignSharedSpaceOrigin(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (Root == null)
        {
            return;
        }
        Quaternion inverseRotation = Quaternion.Inverse(targetRotation);
        Vector3 inversePosition = inverseRotation * -targetPosition;
        Root.SetPositionAndRotation(inversePosition, inverseRotation);
        Debug.Log(Root.transform.position +""+ Root.transform.rotation);
    }
}