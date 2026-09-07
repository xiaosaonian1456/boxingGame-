/*******************************************************************************
Copyright © 2015-2022 PICO Technology Co., Ltd.All rights reserved.  

NOTICE：All information contained herein is, and remains the property of 
PICO Technology Co., Ltd. The intellectual and technical concepts 
contained herein are proprietary to PICO Technology Co., Ltd. and may be 
covered by patents, patents in process, and are protected by trade secret or 
copyright law. Dissemination of this information or reproduction of this 
material is strictly forbidden unless prior written permission is obtained from
PICO Technology Co., Ltd. 
*******************************************************************************/

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Pico.Platform;
using Pico.Platform.Models;
using UnityEngine.UI;
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class PXR_SharedAnchorManager : MonoBehaviour
{
    private static PXR_SharedAnchorManager instance = null;
    public static PXR_SharedAnchorManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<PXR_SharedAnchorManager>();
            }
            return instance;
        }
    }


    public Text ExecuteResult;
    public Text loadingTip;
    public GameObject anchorPrefab;
    public GameObject multiPlayer;
    public GameObject loading;
    private float durationTimes = 5.0f;
    private float anchorPoseUpdateTime = 1.0f;
    private bool IsCreateAnchorMode = false;
    private Dictionary<ulong, PXRSample_SpatialAnchor> anchorList  = new Dictionary<ulong, PXRSample_SpatialAnchor>();
    public Dictionary<ulong, Guid> persistDict = new Dictionary<ulong, Guid>();
    private InputDevice rightController;
    [SerializeField] private Transform anchorRoot;

    [SerializeField] private GameObject anchorPreview;
    [SerializeField] private GameObject menuPanel;

    [SerializeField] private Button btnCreateAnchor;
    [SerializeField] private Button btnLoadAllAnchors;
    [SerializeField] private Button btnStartMatch;
    [SerializeField] private Button btnLoadAnchorByUuid;
    [SerializeField] private InputField inputUuid;

    private string accessToken = "act.a19ca2e0f652b184f42ec193db46e3bf2nbSMMkN60A2CKR1mcSI4lWKdme3_lq";
    private string userId = "7528109370605994034";
    private string appId = "9d4e1a9180d13bd92f6715046a28342b";

    private bool btnAClick = false;
    private bool aLock = false;
    private bool btnAState = false;
    private bool gripClick = false;
    private bool gripLock = false;
    private bool gripState = false;
    public const string TAG = "PXRSharedAnchorSample";
    
    private bool isShowUI = false;

    private ConcurrentQueue<Guid> downLoadEventQueue = new ConcurrentQueue<Guid>();
    private bool isProcessing = false;

    public enum CurState
    {
        None,
        Inited,
        EnqueueSend,
        EnqueueResultRecved,
        MatchmakingFound,
        RoomJoinSend,
        RoomJoined,
        RoomUpdatingSend,
        RoomUpdatingRecved,
        RoomGetCurrentSend,
        RoomLeaveSend,
        RoomLeaveRecved,
        SimplestTestEnd,
    }
    int messageNum = 0;
    int logMaxCount = 10;
    int logCount = 0;
    private CurState curState = CurState.None;
    public const int RoomMaxMsgNum = 99;
    Room matchRoom;

    void Awake()
    {
        btnCreateAnchor.onClick.AddListener(OnBtnPressedCreateAnchor);
        btnLoadAllAnchors.onClick.AddListener(OnBtnPressedLoadLocalAnchors);
        btnStartMatch.onClick.AddListener(OnBtnPressedStartMatch);
        btnLoadAnchorByUuid.onClick.AddListener(OnBtnPressedLoadAnchorByUuid);

        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    void Start()
    {
        //Turn on MR mode
        PXR_Manager.EnableVideoSeeThrough = true;
        StartSpatialAnchorProvider();
#if !UNITY_EDITOR
        InitPlatformService();

        MatchmakingService.SetMatchFoundNotificationCallback(ProcessMatchmakingMatchFound);
        NetworkService.SetNotification_Game_ConnectionEventCallback(OnGameConnectionEvent);
#endif
        
    }

    private async void StartSpatialAnchorProvider()
    {
        var result = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.SpatialAnchor);
        SetExecuteResult("StartSenseDataProvider:" + result);
    }

    void OnEnable()
    {
        
    }

    void OnDisable()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        ProcessKeyEvent();

        if (gripClick)
        {
            menuPanel.SetActive(!isShowUI);
            isShowUI = !isShowUI;
        }

        if (IsCreateAnchorMode)
        {
            if (btnAClick)
            {
                SetExecuteResult("ClickButtonToCreateAnchor");
                CreateSpatialAnchor(anchorPreview.transform);
            }
        }

        if (CoreService.Initialized)
        {
            // Loop to check the current state
            CheckState();
        }

        if (!downLoadEventQueue.IsEmpty)
        {
            if (downLoadEventQueue.TryDequeue(out Guid uuid))
            {
                if (isProcessing)
                {
                    downLoadEventQueue.Enqueue(uuid);
                }
                else
                {
                    DownSpatialAnchor(uuid);
                }
            }
        }
    }

    
    void OnApplicationPause(bool pause)
    {

    }

    void OnGameConnectionEvent(Message<GameConnectionEvent> msg)
    {
        var state = msg.Data;
        //LogHelper.LogInfo(TAG, $"OnGameConnectionEvent: {state}");
        if (state == GameConnectionEvent.Connected)
        {
            //LogHelper.LogInfo(TAG, "GameConnection: success！");
        }
        else if (state == GameConnectionEvent.Closed)
        {
            Uninitialize();
            //LogHelper.LogInfo(TAG, "GameConnection: fail！Please re-initialize！");
        }
        else if (state == GameConnectionEvent.GameLogicError)
        {
            Uninitialize();
            //LogHelper.LogInfo(TAG, "GameConnection: fail！After successful reconnection, the logic state is found to be wrong，Please re-initialize！");
        }
        else if (state == GameConnectionEvent.Lost)
        {
            //LogHelper.LogInfo(TAG, "GameConnection: Reconnecting, please wait！");
        }
        else if (state == GameConnectionEvent.Resumed)
        {
            //LogHelper.LogInfo(TAG, "GameConnection: successful reconnection！");
        }
        else if (state == GameConnectionEvent.KickedByRelogin)
        {
            Uninitialize();
            //LogHelper.LogInfo(TAG, "GameConnection: Repeat login! Please reinitialize！");
        }
        else if (state == GameConnectionEvent.KickedByGameServer)
        {
            Uninitialize();
            //LogHelper.LogInfo(TAG, "GameConnection: Server kicks people! Please reinitialize！");
        }
        else
        {
            //LogHelper.LogInfo(TAG, "GameConnection: unknown error！");
        }
    }

    void OnDestroy()
    {
        Uninitialize();
    }

    void ProcessMatchmakingMatchFound(Message<Room> message)
    {
        if (!message.IsError)
        {
            matchRoom = message.Data;
            SetExecuteResult("Match success -> Found room : " + matchRoom.RoomId);
            curState = CurState.MatchmakingFound;
        }
        else
        {
            var error = message.GetError();
            SetExecuteResult($"Match failed : {error.Message}");
        }
    }

    private void CheckState()
    {
        TestMatchmakingAndRoom();
    }

    void TestMatchmakingAndRoom()
    {
        switch (curState)
        {
            case CurState.Inited:
                StartEnqueue();
                break;
            case CurState.MatchmakingFound:
                StartJoinRoom();
                break;
            case CurState.RoomJoinSend:
                break;
            case CurState.RoomJoined:
                StartRoomUpdating();
                break;
            case CurState.RoomUpdatingSend:
                break;
            case CurState.RoomUpdatingRecved:
                break;
            case CurState.RoomGetCurrentSend:
                break;
            case CurState.RoomLeaveSend:
                break;
            case CurState.RoomLeaveRecved:
                EndTest();
                break;
            default:
                break;
        }
    }

    void StartEnqueue()
    {
        SetExecuteResult($"Start enqueue");
        MatchmakingOptions options = new MatchmakingOptions();
        options.SetCreateRoomMaxUsers(2);
        var rst = MatchmakingService.Enqueue2("pool1", options).OnComplete(ProcessMatchmakingEnqueue);
        var result = rst.TaskId;
        SetExecuteResult("Match queue result = " + result);
        if (0 != result)
        {
            SetExecuteResult("Current state：EnqueueSend");
            curState = CurState.EnqueueSend;
        }
    }

    void EndTest()
    {
        curState = CurState.SimplestTestEnd;
        SetExecuteResult("End test！set current state：SimplestTestEnd! Test succeed! \n");
    }

    // enter the room
    void StartJoinRoom()
    {
        SetExecuteResult("StartJoinRoom...");
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.SetTurnOffUpdates(false); // receive updates
        roomOptions.SetRoomId(matchRoom.RoomId);
        SetExecuteResult($"Enter room : {matchRoom.RoomId}");
        var rst = RoomService.Join2(matchRoom.RoomId, roomOptions).OnComplete(ProcessRoomJoin2);
        var result = rst.TaskId;
        SetExecuteResult("Enter room result = " + result);
        if (0 != result)
        {
            SetExecuteResult("Current state：room joined");
            curState = CurState.RoomJoinSend;
        }
    }

    void ProcessMatchmakingEnqueue(Message<MatchmakingEnqueueResult> message)
    {
        if (!message.IsError)
        {
            var result = message.Data;
            curState = CurState.EnqueueResultRecved;
            SetExecuteResult($"Join the matching queue pool name: {result.Pool}，set current state: EnqueueResultReceived");
        }
        else
        {
            var error = message.GetError();
            SetExecuteResult($"Join the matching queue error : {error.Message}");
        }
    }

    void ProcessRoomJoin2(Message<Room> message)
    {
        if (!message.IsError)
        {
            var room = message.Data;
            curState = CurState.RoomJoined;
            SetExecuteResult($"join room Room.ID: {room.RoomId}, set current state: RoomJoined");
        }
        else
        {
            var error = message.GetError();
            SetExecuteResult($"join room error: {error.Message}");
        }
    }

    void StartRoomUpdating()
    {
        // read packet
        var packet = NetworkService.ReadPacket();
        while (packet != null)
        {
            var sender = packet.SenderId;
            ++messageNum;
            var bytes = new byte[packet.Size];
            var bytesSize = packet.GetBytes(bytes);

            Guid fromCloudGuid = new Guid(bytes);

            SetExecuteResult($"Received Uuid: {fromCloudGuid}");
            
            loading.SetActive(true);
            loadingTip.text = "Downloading";

            downLoadEventQueue.Enqueue(fromCloudGuid);
            //DownSpatialAnchor(fromCloudGuid);

            packet.Dispose();
            packet = NetworkService.ReadPacket();

        }
    }

    private async void DownSpatialAnchor(Guid uuid)
    {
        SetLoadingImg(true);
        isProcessing = true;
        var result = await PXR_MixedReality.DownloadSharedSpatialAnchorAsync(uuid);
        isProcessing = false;
        SetLoadingImg(false);
        if (result == PxrResult.SUCCESS)
        {
            var uuids = new[] { uuid };
            var result1 = await PXR_MixedReality.QuerySpatialAnchorAsync(uuids);
            SetExecuteResult("LoadSpatialAnchorAsync:" + result1.result.ToString());
            if (result1.result == PxrResult.SUCCESS)
            {
                foreach (var key in result1.anchorHandleList)
                {
                    if (!anchorList.ContainsKey(key))
                    {
                        Debug.Log("PXR_MRSample SpatialAnchorLoaded handle:" + key);
                        GameObject anchorObject = Instantiate(anchorPrefab);
                        PXRSample_SpatialAnchor anchor = anchorObject.GetComponent<PXRSample_SpatialAnchor>();
                        anchor.SetAnchorHandle(key);
                        
                        //anchor.SetAnchorUuid(uuid);

                        PXR_MixedReality.LocateAnchor(key, out var position, out var orientation);
                        anchor.transform.position = position;
                        anchor.transform.rotation = orientation;
                        anchorList.Add(key, anchor);
                        //anchorList[key].IsSavedLocally = true;
                    }
                }
            }
        }
    }

    void StartRoomLeave()
    {
        SetExecuteResult("leave the room...");
        SetExecuteResult("roomId = " + matchRoom.RoomId);
        var rst = RoomService.Leave(matchRoom.RoomId).OnComplete(ProcessRoomLeave);
        var result = rst.TaskId;
        SetExecuteResult("leave the room result = " + result);
        if (0 != result)
        {
            curState = CurState.RoomLeaveSend;
            SetExecuteResult("set current state：RoomLeaveSend");
        }
    }

    void ProcessRoomLeave(Message<Room> message)
    {
        if (!message.IsError)
        {
            var room = message.Data;
            curState = CurState.RoomLeaveRecved;
            SetExecuteResult($"leaven room Room.ID: {room.RoomId}, set current state: RoomLeaveRecved");
        }
        else
        {
            var error = message.GetError();
            SetExecuteResult($"leave room error: {error.Message}");
        }
    }

    private void InitPlatformService()
    {
        SetExecuteResult("Start initialize");
        CoreService.Initialize();
        if (!CoreService.Initialized)
        {
            SetExecuteResult("pico initialize failed");
            return;
        }

        UserService.GetAccessToken().OnComplete(delegate (Message<string> message)
        {
            if (message.IsError)
            {
                var err = message.GetError();
                SetExecuteResult($"Got access token error {err.Message} code={err.Code}");
                return;
            }

            accessToken = message.Data;
            SetExecuteResult($"Got access token: {accessToken}, GameInitialize begin");
            CoreService.GameInitialize(accessToken).OnComplete(OnGameInitialize);
        });

    }

    void OnGameInitialize(Message<GameInitializeResult> msg)
    {
        if (msg == null)
        {
            SetExecuteResult($"OnGameInitialize: fail, message is null");
            return;
        }

        if (msg.IsError)
        {
            SetExecuteResult($"GameInitialize Failed: {msg.Error.Code}, {msg.Error.Message}");
        }
        else
        {
            SetExecuteResult($"OnGameInitialize: {msg.Data}");
            if (msg.Data == GameInitializeResult.Success)
            {
                SetExecuteResult("GameInitialize: success！");
            }
            else
            {
                Uninitialize();
                SetExecuteResult("GameInitialize: fail！Please re-initialize！");
            }
        }

        UserService.GetLoggedInUser().OnComplete(delegate (Message<User> m)
        {
            if (m.IsError)
            {
                SetExecuteResult($"GetLoggedInUser failed:code={m.Error.Code} message={m.Error.Message}");
                return;
            }

            userId = m.Data.ID;
            SetExecuteResult($"Got user id: {userId}, GameInitialize begin");
        });
        
    }

    void Uninitialize()
    {
        CoreService.GameUninitialize();
    }

    public void SetExecuteResult(string result)
    {
        logCount++;
        //LogHelper.LogInfo(TAG, result);
        if (logCount > logMaxCount)
        {
            ExecuteResult.text = "";
            logCount = 0;
        }
        ExecuteResult.text = result + "\n" + ExecuteResult.text;
    }

    private void ProcessKeyEvent()
    {
        rightController.TryGetFeatureValue(CommonUsages.primaryButton, out btnAState);
        if (btnAState && !aLock)
        {
            btnAClick = true;
            aLock = true;
        }
        else
        {
            btnAClick = false;
        }
        if (!btnAState)
        {
            btnAClick = false;
            aLock = false;
        }

        rightController.TryGetFeatureValue(CommonUsages.gripButton, out gripState);
        if (gripState && !gripLock)
        {
            gripClick = true;
            gripLock = true;
        }
        else
        {
            gripClick = false;
        }
        if (!gripState)
        {
            gripClick = false;
            gripLock = false;
        }
    }

    private void OnBtnPressedCreateAnchor()
    {
        menuPanel.SetActive(false);
        isShowUI = false;
        IsCreateAnchorMode = !IsCreateAnchorMode;
        if (IsCreateAnchorMode)
        {
            btnCreateAnchor.transform.Find("Text").GetComponent<Text>().text = "CancelCreate";
            anchorPreview.SetActive(true);
        }
        else
        {
            btnCreateAnchor.transform.Find("Text").GetComponent<Text>().text = "CreateAnchor";
            anchorPreview.SetActive(false);
        }
    }

    private void OnBtnPressedStartMatch()
    {
        Debug.Log("PXR_MRSample StartMatch");

        curState = CurState.Inited;
        
    }

    private async void OnBtnPressedLoadLocalAnchors()
    {
        var result = await PXR_MixedReality.QuerySpatialAnchorAsync();
        SetExecuteResult("LoadSpatialAnchorAsync:" + result.result.ToString());
        if (result.result == PxrResult.SUCCESS)
        {
            foreach (var key in result.anchorHandleList)
            {
                if (!anchorList.ContainsKey(key))
                {
                    Debug.Log("PXR_MRSample SpatialAnchorLoaded handle:" + key);
                    GameObject anchorObject = Instantiate(anchorPrefab);
                    PXRSample_SpatialAnchor anchor = anchorObject.GetComponent<PXRSample_SpatialAnchor>();
                    anchor.SetAnchorHandle(key);
                    PXR_MixedReality.GetAnchorUuid(key, out var uuid);
                    //anchor.SetAnchorUuid(uuid);

                    PXR_MixedReality.LocateAnchor(key, out var position, out var orientation);
                    anchor.transform.position = position;
                    anchor.transform.rotation = orientation;
                    anchorList.Add(key, anchor);
                    //anchorList[key].IsSavedLocally = true;
                }
            }
        }
    }

    private async void OnBtnPressedLoadAnchorByUuid()
    {
        if (inputUuid.text != String.Empty)
        {
            loading.SetActive(true);
            if (Guid.TryParse(inputUuid.text, out var guid))
            {
                SetExecuteResult($"Valid anchor uuid:{guid}");

                var result = await PXR_MixedReality.DownloadSharedSpatialAnchorAsync(guid);
                if (result == PxrResult.SUCCESS)
                {
                    var uuids = new[] { guid };
                    var result1 = await PXR_MixedReality.QuerySpatialAnchorAsync(uuids);
                    SetExecuteResult("LoadSpatialAnchorAsync:" + result1.result.ToString());
                    if (result1.result == PxrResult.SUCCESS)
                    {
                        foreach (var key in result1.anchorHandleList)
                        {
                            if (!anchorList.ContainsKey(key))
                            {
                                Debug.Log("PXR_MRSample SpatialAnchorLoaded handle:" + key);
                                GameObject anchorObject = Instantiate(anchorPrefab);
                                PXRSample_SpatialAnchor anchor = anchorObject.GetComponent<PXRSample_SpatialAnchor>();
                                anchor.SetAnchorHandle(key);
                                //anchor.SetAnchorUuid(guid);

                                PXR_MixedReality.LocateAnchor(key, out var position, out var orientation);
                                anchor.transform.position = position;
                                anchor.transform.rotation = orientation;
                                anchorList.Add(key, anchor);
                                //anchorList[key].IsSavedLocally = true;
                            }
                        }
                    }
                }
                
            }
            else
            {
                SetExecuteResult("Invalid anchor uuid");
            }
        }
    }

    private void SpatialTrackingStateUpdate(PxrEventSpatialTrackingStateUpdate info)
    {
        Debug.Log("PXR_MRSample TrackingState Event:" + info.state + info.message);
    }

    private async void CreateSpatialAnchor(Transform transform)
    {
        var result = await PXR_MixedReality.CreateSpatialAnchorAsync(transform.position, transform.rotation);
        SetExecuteResult("CreateSpatialAnchorAsync:" + result.ToString());
        if (result.result == PxrResult.SUCCESS)
        {
            GameObject anchorObject = Instantiate(anchorPrefab);
            var anchor = anchorObject.GetComponent<PXRSample_SpatialAnchor>();
            if (anchor == null)
            {
                anchor = anchorObject.AddComponent<PXRSample_SpatialAnchor>();
            }
            anchor.SetAnchorHandle(result.anchorHandle);
            //anchor.SetAnchorUuid(result.uuid);

            anchorList.Add(result.anchorHandle, anchor);

            var result1 = PXR_MixedReality.GetAnchorUuid(result.anchorHandle, out var uuid);
            SetExecuteResult("GetUuid:" + result1.ToString() + "  " + (result.uuid.Equals(uuid)) + "Uuid:" + uuid);
        }
    }

    public void DestroySpatialAnchor(ulong anchorHandle)
    {
        var result = PXR_MixedReality.DestroyAnchor(anchorHandle);
        SetExecuteResult("DestroySpatialAnchor:" + result.ToString());
        if (result == PxrResult.SUCCESS)
        {
            if (anchorList.ContainsKey(anchorHandle))
            {
                Destroy(anchorList[anchorHandle].gameObject);
                anchorList.Remove(anchorHandle);
            }
        }
    }

    public void SetLoadingImg(bool state)
    {
        loading.SetActive(state);
        loadingTip.text = "Uploading";
    }

    public void ShareAnchorToOthers(Guid uuid)
    {
        loading.SetActive(false);
        NetworkService.SendPacketToCurrentRoom(uuid.ToByteArray());
    }

    private void SendPlayerData(PlayerData playerData)
    {
        NetworkService.SendPacketToCurrentRoom(ConvertStructToBytes(playerData));
    }

    private void ReadPlayerData(byte[] bytes)
    {

    }

    private byte[] ConvertStructToBytes(PlayerData playerData)
    {
        int structSize = Marshal.SizeOf(playerData);
        
        byte[] bytes = new byte[structSize];
        
        IntPtr structPtr = Marshal.AllocHGlobal(structSize);
        Marshal.StructureToPtr(playerData, structPtr, false);
        
        Marshal.Copy(structPtr, bytes, 0, structSize);
        
        Marshal.FreeHGlobal(structPtr);

        return bytes;
    }

    private PlayerData ConvertBytesToStruct(byte[] bytes)
    {
        int structSize = Marshal.SizeOf<PlayerData>();
        
        byte[] structBytes = new byte[structSize];
        
        Array.Copy(bytes, structBytes, structSize);
        
        IntPtr structPtr = Marshal.AllocHGlobal(structSize);
        Marshal.Copy(structBytes, 0, structPtr, structSize);
        
        PlayerData myStruct = Marshal.PtrToStructure<PlayerData>(structPtr);
        
        Marshal.FreeHGlobal(structPtr);

        return myStruct;
    }
}

struct PlayerData
{
    private Vector3 playerPosition;
    private Quaternion playerRotation;
    private string userName;
}
