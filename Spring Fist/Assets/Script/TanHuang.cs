using UnityEngine;
using Photon.Pun;
using Unity.XR.CoreUtils;
public class TanHuang : MonoBehaviourPun,IPunObservable
{
    [Header("弹簧引用")]
    public Transform SpringEnd;      // 弹簧末端（固定点，连接手柄）
    public Transform SpringForward;   // 弹簧前端（连接拳头）
    private Transform handle; // 手柄的Transform引用
    private Transform RightController;
    public GameObject Spring;        // 弹簧物体
    private Transform Fist;           // 拳头物体

    [Header("缩放设置")]
    public float scaleMultiplier = 100f; // 缩放倍数
    public float minScale = 0.1f;    // 最小缩放
    
    [Header("初始状态")]
    public bool autoInitialize = true; // 是否自动初始化
    private GameObject[] FistModels;
    // 初始数据记录
    private Vector3 _initialSpringPosition;
   
    private Vector3 _initialSpringScale;
    private Vector3 _initialLocalPosition;
    private float _initialDistance;
    private void Awake() {
        
         XROrigin xrOrigin=FindObjectOfType<XROrigin>();
            
            
            RightController = xrOrigin.transform.Find("[Right InteractionAttachController] Attach");
            if (RightController != null)
            {
                handle = RightController.GetChild(0).transform;
            }
    }
    void Start()
    {
        GameObject[] allFist = GameObject.FindGameObjectsWithTag("RightQuan");
        

foreach (GameObject fistObj in allFist)
{
    // ✅ 正确：判断这个弹簧本身是不是我的
    PhotonView fistView = fistObj.GetComponent<PhotonView>();
    if (fistView != null && fistView.IsMine)
    {
        Fist = fistObj.transform;
        Debug.Log($"找到自己的拳头：{fistObj.name}");
        break; // ✅ 找到后立即终止循环，避免被覆盖
    }
}
       
    }

    /// <summary>
    /// 初始化弹簧状态，记录初始数据
    /// </summary>
   
    public void Initialize()
    {
        if(photonView.IsMine && handle != null&&Spring!=null){
            _initialSpringPosition=handle.position;
            _initialSpringScale=Spring.transform.localScale;
            // 记录脚本所在物体的初始位置
            _initialLocalPosition = transform.localPosition;
        }
        
        
        // 记录初始距离（只取 localPosition 的 forward 方向分量，即 Z 轴距离）
        if (SpringForward != null && Fist != null)
        {
            _initialDistance = Mathf.Abs(Fist.localPosition.z - SpringForward.localPosition.z);
        }
        
        
        
        Debug.Log("弹簧初始化完成！初始距离: " + _initialDistance);
    }

    /// <summary>
    /// 重置弹簧到初始状态
    /// </summary>
    // [PunRPC]
    public void ResetToInitialState()
    {
        if(photonView.IsMine&&PhotonNetwork.IsConnected){
        if (Spring != null)
        {
            transform.localPosition = _initialLocalPosition;
          
            Spring.transform.localScale = _initialSpringScale;
        }
        
        
        
        Debug.Log("弹簧已重置到初始状态");
    }}
    // public void ResetWay(){
    //     photonView.RPC(nameof(TanHuang.ResetToInitialState), RpcTarget.All);
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
    // 只有本地玩家驱动弹簧
    if (!photonView.IsMine) return;

    if (SpringForward == null || Fist == null || Spring == null)
        return;

    // 出拳改为发射拳头子弹（ProjectileFist），手上的拳头和弹簧不再变化，始终保持初始状态
    ResetToInitialState();
}
public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
{
    if (stream.IsWriting)
    {
      
      stream.SendNext(transform.position);
      stream.SendNext(transform.rotation);
      stream.SendNext(Spring.transform.localScale);

      
    }
    else
    {
        transform.position = (Vector3)stream.ReceiveNext();
        transform.rotation = (Quaternion)stream.ReceiveNext();
        Spring.transform.localScale = (Vector3)stream.ReceiveNext();
    }
}


}