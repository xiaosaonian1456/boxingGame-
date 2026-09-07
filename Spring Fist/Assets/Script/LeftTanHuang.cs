using UnityEngine;
using Photon.Pun;
using Unity.XR.CoreUtils;
public class LeftTanHuang : MonoBehaviourPun,IPunObservable
{
    [Header("弹簧引用")]
    public Transform LeftSpringEnd;      // 弹簧末端（固定点，连接手柄）
    public Transform LeftSpringForward;   // 弹簧前端（连接拳头）
    private Transform Lefthandle; // 手柄的Transform引用
    private Transform LeftController;
    public GameObject LeftSpring;        // 弹簧物体
    private Transform LeftFist;           // 拳头物体
   private LeftPhysicsSpringFist leftPhysicsSpringFist;
    [Header("缩放设置")]
    public float scaleMultiplier = 100f; // 缩放倍数
    public float minScale = 0.1f;    // 最小缩放
    
    [Header("初始状态")]
    public bool autoInitialize = true; // 是否自动初始化
    private GameObject[] LeftFistModels;
    // 初始数据记录
    private Vector3 _initialSpringPosition;
   
    private Vector3 _initialSpringScale;
    private Vector3 _initialLocalPosition;
    private float _initialDistance;
    private void Awake() {
        
        XROrigin xrOrigin=FindObjectOfType<XROrigin>();
            
            
            LeftController = xrOrigin.transform.Find("[Left InteractionAttachController] Attach");
            if (LeftController != null)
            {
                Lefthandle = LeftController.GetChild(0).transform;
            }
    }
    void Start()
    {
        GameObject[] allFist = GameObject.FindGameObjectsWithTag("LeftQuan");

foreach (GameObject fistObj in allFist)
{
    // ✅ 正确：判断这个弹簧本身是不是我的
    PhotonView fistView = fistObj.GetComponent<PhotonView>();
    if (fistView != null && fistView.IsMine)
    {
        LeftFist = fistObj.transform;
        Debug.Log($"找到自己的拳头：{LeftFist.name}");
        leftPhysicsSpringFist=LeftFist.GetComponent<LeftPhysicsSpringFist>();
        break; // ✅ 找到后立即终止循环，避免被覆盖
    }
}
       
    }

    /// <summary>
    /// 初始化弹簧状态，记录初始数据
    /// </summary>
    public void Initialize()
    {
       if(photonView.IsMine && Lefthandle != null&&LeftSpring!=null){
            _initialSpringPosition=Lefthandle.position;
            _initialSpringScale=LeftSpring.transform.localScale;
            // 记录脚本所在物体的初始位置
            _initialLocalPosition = transform.localPosition;
        }
        
        
        // 记录初始距离（只取 localPosition 的 forward 方向分量，即 Z 轴距离）
        if (LeftSpringForward != null && LeftFist != null)
        {
            _initialDistance = Mathf.Abs(LeftFist.localPosition.z - LeftSpringForward.localPosition.z);
        }
        
        
        
        Debug.Log("左弹簧初始化完成！初始距离: " + _initialDistance);
    }

    /// <summary>
    /// 重置弹簧到初始状态
    /// </summary>
    // [PunRPC]
    public void ResetToInitialState()
    {
        if(photonView.IsMine&&PhotonNetwork.IsConnected){
        if (LeftSpring != null)
        {
            transform.localPosition = _initialLocalPosition;
          
            LeftSpring.transform.localScale = _initialSpringScale;
        }
        
        
        
        Debug.Log("左弹簧已重置到初始状态");
    }}
    // public void ResetWay(){
    //     photonView.RPC(nameof(LeftTanHuang.ResetToInitialState), RpcTarget.All);
    // }

    /// <summary>
    /// 获取初始距离
    /// </summary>
    // public float GetInitialDistance()
    // {
    //     if(photonView.IsMine&&PhotonNetwork.IsConnected){
    //     return _initialDistance;
    //     }
    //     else
    //     {
    //         return 0;
    //     }
    // }

    /// <summary>
    /// 获取当前距离
    /// </summary>
    // public float GetCurrentDistance()
    // {
    //     if(photonView.IsMine&&PhotonNetwork.IsConnected){
    //     if (SpringForward == null || Fist == null)
    //         return 0;
        
    //     return Vector3.Distance(SpringForward.localPosition, Fist.localPosition);
    // }
    // else
    // {
    //     return 0;
    // }
    // }

    void Update()
{
    if(!photonView.IsMine)
        return;
    // ✅ 所有客户端都执行弹簧计算，不需要任何所有权判断
    if (LeftSpringForward == null || LeftFist == null || LeftSpring == null)
        return;
    if(leftPhysicsSpringFist._isLeftPunching||leftPhysicsSpringFist._isLeftExtending){
        // 所有客户端都根据自己看到的拳头位置计算弹簧长度
    float distance = Mathf.Abs(LeftFist.localPosition.z - LeftSpringForward.localPosition.z);
    Vector3 currentScale = LeftSpring.transform.localScale;
    currentScale.y = Mathf.Max(distance * scaleMultiplier, minScale);
    LeftSpring.transform.localScale = currentScale;
    }
    else{
        ResetToInitialState();
    }
}
public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
{
    if (stream.IsWriting)
    {
       
      stream.SendNext(transform.position);
      stream.SendNext(transform.rotation);
      stream.SendNext(LeftSpring.transform.localScale);

      
    }
    else
    {
        
        transform.position = (Vector3)stream.ReceiveNext();
        transform.rotation = (Quaternion)stream.ReceiveNext();
        LeftSpring.transform.localScale = (Vector3)stream.ReceiveNext();
    }
}


}