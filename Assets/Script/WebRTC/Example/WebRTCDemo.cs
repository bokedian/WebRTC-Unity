using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using System;
using System.IO;

public class WebRTCDemo : MonoBehaviour
{
    public InputField Server, ClientId;
    public InputField RemoteId, Message;
    public Button ConnectBtn, loginBtn;
    public Button CreateOfferBtn, SendBtn;
    public Text log;
    public Camera testCam;
    public RawImage rawImage;//显示远端图像的

    public Button CreateChannelBtn;
    public InputField ChannelId;

    private PeerConnectionController peer;
    private SessionManager sessionManager;
    private WebSocketSignalingClient signalingClient;

    public AudioSource localAudioSource;
    public AudioSource remoteAudioSource;
    void Start()
    {
        signalingClient = new WebSocketSignalingClient();
        peer = new PeerConnectionController();
        peer.Init();
        ConnectBtn.onClick.AddListener(ConnectBtn_OnClick);
        loginBtn.onClick.AddListener(LoginBtn_OnClick);
        SendBtn.onClick.AddListener(SendBtn_OnClick);
        CreateOfferBtn.onClick.AddListener(CreateOfferBtn_OnClick);
        CreateChannelBtn.onClick.AddListener(CreateChannelBtn_OnClick);
        Application.logMessageReceivedThreaded += HandleLog;
        StartCoroutine(WebRTC.Update());
        //添加视频的回调事件
        peer.VideoStats.OnOutboundStatsUpdated += HandleOutBound;
        peer.VideoStats.OnInboundStatsUpdated += HandleInBound;
        peer.remoteAudioSource = remoteAudioSource;
    }


    private void Update()
    {
        //if (sessionManager != null)
        //{
        //    if (sessionManager.remoteVideoTrack != null)
        //    {
        //        //Debug.Log("Set Texture");
        //        rawImage.texture = sessionManager.remoteVideoTrack.Texture;
        //    }
        //}

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            localAudioSource.Play();
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            localAudioSource.Pause();
        }

    }
    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        //log.text = condition;
#if !UNITY_EDITOR
        string rootpath = Directory.GetParent(Application.dataPath).FullName;
        string logDirectory = Path.Combine(rootpath, "Logs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
        string path = logDirectory + "/log.txt";
        if (!File.Exists(path))
        {
            File.Create(path);
        }
        string content = "\n" + condition;
        File.AppendAllText(path, content);
#endif
    }
    private async void ConnectBtn_OnClick()
    {   
        await signalingClient.ConnectAsync(new Uri("ws://localhost:5191/ws"));
    }

    private async void LoginBtn_OnClick()
    {
        if (signalingClient == null) return;
        string localid = ClientId.text;
        await signalingClient.LoginAsync(localid);
        if (sessionManager == null)
        {
            sessionManager = new SessionManager(ClientId.text, peer, signalingClient);
            sessionManager.Init();
            sessionManager.showImage = rawImage;
        }
    }

    public void SendBtn_OnClick()
    {
        if (sessionManager == null) return;
        string label = ChannelId.text;
        sessionManager.SendData(label, Message.text);
    }

    private async void CreateOfferBtn_OnClick()
    {
        //sessionManager.CreateVideoTrack(testCam);
        sessionManager.CreateAudioTrack(localAudioSource);
        await sessionManager.StartOfferAsync(RemoteId.text);
    }

    private void CreateChannelBtn_OnClick()
    {
        string label = ChannelId.text;
        sessionManager.CreateDataChannel(label);
    }

    private void HandleOutBound(OutboundVideoStats stats)
    {
        Debug.Log($"[发送端]: 分辨率:{testCam.targetTexture.width}*{testCam.targetTexture.height} 发送帧率:{stats.FramesPerSecond} ");
        Debug.Log($"[发送端]: FrameEncoded:{stats.FramesEncoded} FramesSent:{stats.FramesSent} ");
        Debug.Log($"[发送端]: 实际编码器:{stats.EncoderImplementation} 受影响原因:{stats.QualityLimitationReason} ");
    }

    private void HandleInBound(InboundVideoStats stats)
    {
        Debug.Log($"[接收端]: 分辨率:{rawImage.texture.width}*{rawImage.texture.height} 帧率:{stats.FramesPerSecond} ");
        Debug.Log($"[接收端]: FramesDecoded:{stats.FramesDecoded} FramesReceived:{stats.FramesReceived} ");
        Debug.Log($"[接收端]: 实际解码器:{stats.DecoderImplementation}");
    }
    private async void OnDestroy()
    {
        Application.logMessageReceivedThreaded -= HandleLog;
        await signalingClient.DisConnectAsync();
        signalingClient.Dispose();
    }
}
