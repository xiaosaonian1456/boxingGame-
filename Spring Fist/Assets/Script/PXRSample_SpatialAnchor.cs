using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;
public class PXRSample_SpatialAnchor : MonoBehaviourPunCallbacks, IPunObservable
{
    [HideInInspector]
    public ulong anchorHandle;
    [SerializeField]
    private Text anchorID;
    [SerializeField]
    private GameObject savedIcon;
    [SerializeField]
    private GameObject uiCanvas;

    [SerializeField] private Button btnPersist;
    [SerializeField] private Button btnDestroyAnchor;
    [SerializeField] private Button btnDeleteAnchor;
    private Vector3 MasterAnchorPosition=new Vector3(0,0,0);
    private Quaternion MasterAnchorQuaternion = Quaternion.identity;
    private Vector3 ClientAnchorPosition = new Vector3(0, 0, 0);
    private Quaternion ClientAnchorQuaternion = Quaternion.identity;
    public Vector3 PositonOffset;
    public Quaternion RotationOffset;
    private Transform Root;
   
    
    [Tooltip("平滑插值系数，值越小过渡越慢"), Range(0f, 1f)]
    public float alignLerp = 0.1f;

  
    private void Awake()
    {
        //uiCanvas.SetActive(false);
        uiCanvas.SetActive(true);
        uiCanvas.GetComponent<Canvas>().worldCamera = Camera.main;

        btnPersist.onClick.AddListener(OnBtnPressedPersist);
        btnDestroyAnchor.onClick.AddListener(OnBtnPressedDestroy);
        btnDeleteAnchor.onClick.AddListener(OnBtnPressedUnPersist);
        Root = GameObject.FindWithTag("Root").transform;
    }

    protected void OnEnable()
    {
    }

    protected void OnDisable()
    {
        
    }

    private void Start()
    {

    }


    private void Update()
    {
        if (uiCanvas.activeSelf)
        {
            uiCanvas.transform.LookAt(new Vector3(uiCanvas.transform.position.x * 2 - Camera.main.transform.position.x, uiCanvas.transform.position.y * 2 - Camera.main.transform.position.y, uiCanvas.transform.position.z * 2 - Camera.main.transform.position.z), Vector3.up);
        }
        DownOffset();
       
        
    }

    private void LateUpdate()
    {
        var result = PXR_MixedReality.LocateAnchor(anchorHandle, out var position, out var rotation);
        if (result == PxrResult.SUCCESS)
        {
            transform.position = position;
            transform.rotation = rotation;
            if (PhotonNetwork.IsMasterClient)
            {
                MasterAnchorPosition= position;
                MasterAnchorQuaternion = rotation;
            }
            else
            {
                ClientAnchorPosition = position;
                ClientAnchorQuaternion = rotation;
                
            }
            
            
        }
        else
        {
            PXRSample_SpatialAnchorManager.Instance.SetLogInfo("LocateSpatialAnchor:" + result.ToString());
        }
    }

    private async void OnBtnPressedPersist()
    {
        var result = await PXR_MixedReality.PersistSpatialAnchorAsync(anchorHandle);
        PXRSample_SpatialAnchorManager.Instance.SetLogInfo("PersistSpatialAnchorAsync:" + result.ToString());
        if (result == PxrResult.SUCCESS)
        {
            ShowSaveIcon();
            // 核心修复：传入 anchorHandle 参数

           
        }

    }
    private void DownOffset()
    {
        RotationOffset = MasterAnchorQuaternion * Quaternion.Inverse(ClientAnchorQuaternion);
        PositonOffset = MasterAnchorPosition - RotationOffset * ClientAnchorPosition;
        Root.SetPositionAndRotation(PositonOffset, RotationOffset);
    }

    private void OnBtnPressedDestroy()
    {
        PXRSample_SpatialAnchorManager.Instance.DestroySpatialAnchor(anchorHandle);
    }

    private async void OnBtnPressedUnPersist()
    {
        var result = await PXR_MixedReality.UnPersistSpatialAnchorAsync(anchorHandle);
        PXRSample_SpatialAnchorManager.Instance.SetLogInfo("UnPersistSpatialAnchorAsync:" + result.ToString());
        if (result == PxrResult.SUCCESS)
        {
            OnBtnPressedDestroy();
        }
    }

    public void SetAnchorHandle(ulong handle)
    {
        anchorHandle = handle;
        anchorID.text = "ID: " + anchorHandle;
    }

    public void ShowSaveIcon()
    {
        savedIcon.SetActive(true);
    }
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                stream.SendNext(MasterAnchorPosition);
                stream.SendNext(MasterAnchorQuaternion);
            }
           
            

        }
        else
        {
            if (PhotonNetwork.IsMasterClient)
            {
               
            }
            else
            {
                MasterAnchorPosition=(Vector3)stream.ReceiveNext();
                MasterAnchorQuaternion=(Quaternion)stream.ReceiveNext();
            }
          

        }
    }
}        
