using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;
using System.Threading.Tasks;
using System.Text;

public sealed class IceCandidateInfo
{
    public string Candidate;
    public string SdpMid;
    public int SdpMLineIndex;

    public static IceCandidateInfo FromCandidate(RTCIceCandidate candidate)
    {
        return new IceCandidateInfo
        {
            Candidate = candidate.Candidate,
            SdpMid = candidate.SdpMid,
            SdpMLineIndex = candidate.SdpMLineIndex ?? 0
        };
    }

    public RTCIceCandidate ToCandidate()
    {
        RTCIceCandidateInit init = new RTCIceCandidateInit
        {
            candidate = Candidate,
            sdpMid = SdpMid,
            sdpMLineIndex = SdpMLineIndex
        };

        return new RTCIceCandidate(init);
    }
}

public class PeerChannel : IDisposable
{
    public string label => _channel.Label;
    private RTCDataChannel _channel;
    public RTCDataChannelState ReadyState => _channel.ReadyState;

    public event Action<PeerChannel> DataChannelOpened;
    public event Action<PeerChannel> DataChannelClosed;
    public event Action<PeerChannel, byte[]> DataChannelMessageReceived;

    public PeerChannel(RTCDataChannel channel)
    {
        _channel = channel;
        RegisterCallbacks();
    }

    private void RegisterCallbacks()
    {
        _channel.OnOpen += HandleOpen;
        _channel.OnClose += HandleClose;
        _channel.OnMessage += HandleReceiveMessage;
    }

    private void HandleOpen()
    {
        DataChannelOpened?.Invoke(this);
    }

    private void HandleClose()
    {
        DataChannelClosed?.Invoke(this);
    }

    private void HandleReceiveMessage(byte[] bytes)
    {
        DataChannelMessageReceived?.Invoke(this, bytes);
    }

    public void Send(string message)
    {
        if (_channel == null)
            return;

        if (_channel.ReadyState != RTCDataChannelState.Open)
            return;
        _channel.Send(message);
    }

    public void Send(byte[] bytes)
    {
        if (_channel == null)
            return;

        if (_channel.ReadyState != RTCDataChannelState.Open)
            return;
        _channel.Send(bytes);
    }

    public void Dispose()
    {
        if (_channel == null)
            return;

        _channel.OnOpen -= HandleOpen;
        _channel.OnClose -= HandleClose;
        _channel.OnMessage -= HandleReceiveMessage;

        _channel.Close();
        _channel.Dispose();

        _channel = null;
    }
}
/// <summary>
/// 对于unity的WebRTC的一层封装，其中的异步本质是unity内迭代器
/// </summary>
public sealed class PeerConnectionController : IDisposable
{
    private RTCPeerConnection _peer;
    
    private RTCConfiguration _config = default;
    
    public RTCPeerConnection Peer => _peer;
    #region ICE变量
    public RTCPeerConnectionState ConnectionState => _peer.ConnectionState;
    public RTCIceConnectionState IceConnectionState => _peer.IceConnectionState;

    private List<IceCandidateInfo> _pendingCandidates = new List<IceCandidateInfo>();//缓存凭证的
    private bool _remoteDescriptionSet;

    public event Action<IceCandidateInfo> OnIceCandidateGenerated;//对外不直接暴露RTCIceCandidate
    public event Action<RTCPeerConnectionState> OnConnectionStateChanged;
    public event Action<RTCIceConnectionState> OnIceConnectionStateChanged;
    #endregion
    #region DataChannel变量
    //DataChannel
    private Dictionary<string, PeerChannel> _dataChannels = new Dictionary<string, PeerChannel>();
    public event Action<PeerChannel> DataChannelOpened;
    public event Action<PeerChannel> DataChannelClosed;
    public event Action<PeerChannel, byte[]> DataChannelMessageReceived;
    #endregion
    private VideoStreamTrack _videoTrack;
    public event Action<Texture> OnVideoReceive;
    public VideoStreamTrack _remoteVideoTrack;
    private RTCRtpSender _videoSender;
    public void Init()
    {
        _config.iceServers = new RTCIceServer[]
        {
            new RTCIceServer
            {
                urls=new string[]
                {
                    "stun:stun.l.google.com:19302"
                }
            }
        };
        _peer = new RTCPeerConnection(ref _config);
        //CreateDataChannel("data");
        RegisterCallbacks();
    }
    private void RegisterCallbacks()
    {
        _peer.OnIceCandidate = OnIceCandidate;

        _peer.OnDataChannel = OnDataChannel;

        _peer.OnConnectionStateChange = OnConnectionStateChange;

        _peer.OnIceConnectionChange = OnIceConnectionStateChange;

        _peer.OnIceGatheringStateChange = OnIceGatheringStateChange;

        _peer.OnTrack = OnTrack;
    }
    #region ICE方法
    private void OnIceCandidate(RTCIceCandidate candidate)
    {
        if (candidate == null)
        {
            Debug.Log("[Peer] ICE Gathering Complete");
            return;
        }
        Debug.Log($"[Peer] IceCandidate : {candidate.Candidate}");
        IceCandidateInfo info = IceCandidateInfo.FromCandidate(candidate);

        OnIceCandidateGenerated?.Invoke(info);
    }

    private void OnIceGatheringStateChange(RTCIceGatheringState state)
    {
        Debug.Log($"[Peer] IceGatheringState : {state}");
    }

    private void OnConnectionStateChange(RTCPeerConnectionState state)
    {
        Debug.Log($"[Peer] ConnectionStateChange : {state}");
        OnConnectionStateChanged?.Invoke(state);
    }
    private void OnIceConnectionStateChange(RTCIceConnectionState state)
    {
        Debug.Log($"[Peer] IceConnectionStateChange : {state}");
        switch (state)
        {
            case RTCIceConnectionState.Connected:
                Debug.Log("P2P Connected");
                break;

            case RTCIceConnectionState.Disconnected:
                Debug.LogWarning("ICE Disconnected");
                break;

            case RTCIceConnectionState.Failed:
                Debug.LogError("ICE Failed");
                break;
        }
        OnIceConnectionStateChanged?.Invoke(state);
    }

    /// <summary>
    /// 暴露给外部的添加candidate的函数
    /// </summary>
    /// <param name="info"></param>
    public void AddIceCandidateAsync(IceCandidateInfo info)
    {
        if (!_remoteDescriptionSet)
        {
            _pendingCandidates.Add(info);
            Debug.Log("[Peer] Ice Candidate Cached");
            return;
        }
        AddCandidateInterAsync(info);
    }
    /// <summary>
    /// 实际的添加candidate的函数
    /// </summary>
    /// <param name="info"></param>
    private void AddCandidateInterAsync(IceCandidateInfo info)
    {
        RTCIceCandidate candidate = info.ToCandidate();
        Debug.Log($"[Peer] Add IceCandidate :{info.Candidate}");
        _peer.AddIceCandidate(candidate);
    }

    private void FlushPendingCandidates()
    {
        if (_pendingCandidates.Count == 0) return;
        Debug.Log($"[Peer] Flush {_pendingCandidates.Count} cached candidates");
        foreach (var item in _pendingCandidates)
        {
            AddCandidateInterAsync(item);
        }
        _pendingCandidates.Clear();
    }

    public async void PrintIceCandidatePairs()
    {
        var op = _peer.GetStats();
        await WebRtcAwaiter.WaitAsync(op);
        RTCStatsReport report = op.Value;

        Dictionary<string, RTCIceCandidateStats> candidates = new Dictionary<string, RTCIceCandidateStats>();

        foreach (RTCStats stats in report.Stats.Values)
        {
            if (stats.Type == RTCStatsType.LocalCandidate || stats.Type == RTCStatsType.RemoteCandidate)
            {
                RTCIceCandidateStats candidate = stats as RTCIceCandidateStats;

                if (candidate != null)
                {
                    candidates[candidate.Id] = candidate;
                }
            }
        }

        foreach (RTCStats stats in report.Stats.Values)
        {
            if (stats.Type != RTCStatsType.CandidatePair)
                continue;

            RTCIceCandidatePairStats pair = stats as RTCIceCandidatePairStats;
            if (pair == null)
                continue;

            if (!candidates.TryGetValue(pair.localCandidateId, out RTCIceCandidateStats local))
                continue;

            if (!candidates.TryGetValue(pair.remoteCandidateId, out RTCIceCandidateStats remote))
                continue;

            Debug.Log($"[ICE] Pair: {local.candidateType} <-> {remote.candidateType}");
            Debug.Log($"[ICE] Local: {local.address}:{local.port}");
            Debug.Log($"[ICE] Remote: {remote.address}:{remote.port}");
            Debug.Log($"[ICE] State: {pair.state}, Nominated: {pair.nominated}");
        }
    }
    #endregion

    #region DataChannel方法
    private void OnDataChannel(RTCDataChannel channel)
    {
        Debug.Log("[Peer] Remote DataChannel");

        RegisterChannel(channel);
    }
    public PeerChannel CreateDataChannel(string label)
    {
        if (_dataChannels.TryGetValue(label,out PeerChannel channel))
        {
            Debug.Log("已有同名channel，无需创建");
            return channel;
        }

        RTCDataChannel temp= _peer.CreateDataChannel(label);
        PeerChannel res = RegisterChannel(temp);
        return res;

        
    }

    private PeerChannel RegisterChannel(RTCDataChannel rtcChannel)
    {
        PeerChannel channel = new PeerChannel(rtcChannel);

        _dataChannels.Add(channel.label, channel);

        channel.DataChannelOpened += OnDataChannelOpened;
        channel.DataChannelClosed += OnDataChannelClosed;
        channel.DataChannelMessageReceived += OnDataChannelMessage;

        return channel;
    }

    public PeerChannel GetChannel(string label)
    {
        _dataChannels.TryGetValue(label, out PeerChannel channel);
        return channel;
    }

    private void OnDataChannelOpened(PeerChannel channel)
    {
        Debug.Log("[DataChannel] Open");
        DataChannelOpened?.Invoke(channel);
    }

    private void OnDataChannelClosed(PeerChannel channel)
    {
        Debug.Log("[DataChannel] Close");
        DataChannelClosed?.Invoke(channel);
    }

    private void OnDataChannelMessage(PeerChannel channel, byte[] bytes)
    {
        string message = Encoding.UTF8.GetString(bytes);
        Debug.Log($"[DataChannel][{channel.label}] Receive : {message}");
        DataChannelMessageReceived?.Invoke(channel, bytes);
    }
    /// <summary>
    /// 在DataChannel中发送数据
    /// </summary>
    /// <param name="message"></param>
    public void Send(string label, string message)
    {
        if (_dataChannels.TryGetValue(label, out PeerChannel pc))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            Debug.Log($"[DataChannel][{pc.label}] Send : {message}");
            pc.Send(bytes);
        }

    }
    #endregion

    #region 视频流方法

    private void OnTrack(RTCTrackEvent e)
    {
        Debug.Log($"[Peer] Receive Track: {e.Track.Kind}");

        if (e.Track is VideoStreamTrack videoTrack)
        {
            Debug.Log("[Peer] Receive VideoTrack");
            _remoteVideoTrack = videoTrack;
            _remoteVideoTrack.OnVideoReceived += OnVideoReceived;
        }
    }
    public void CreateVideoTrack(Camera camera)
    {
        //RenderTexture texture = new RenderTexture(1920, 1080, 0);
        //texture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B8G8R8A8_SRGB;
        //camera.targetTexture = texture;
        _videoTrack = new VideoStreamTrack(camera.targetTexture);
        _videoSender = _peer.AddTrack(_videoTrack);
        Debug.Log("[Peer] VideoTrack added");
    }

    private void OnVideoReceived(Texture texture)
    {
        Debug.Log("[Peer] OnVideoReceived");
        OnVideoReceive?.Invoke(texture);
    }

    public void SetVideoFramerate(uint framerate)
    {
        if (_videoSender == null)
        {
            Debug.Log("[Peer] No videoSender");
            return;
        }

        RTCRtpSendParameters parameters = _videoSender.GetParameters();
        foreach (var encoding in parameters.encodings)
        {
            encoding.maxFramerate = framerate;
        }

        RTCError error = _videoSender.SetParameters(parameters);//大概是返回结果

        if(error.errorType!= RTCErrorType.None)
        {
            Debug.Log($"[Peer][Video] SetFramerate failed: {error.message}");
            return;
        }
        Debug.Log($"[Peer][Video] maxFramerate={framerate}");
    }
    /// <summary>
    /// 默认的分辨率为1920*1080
    /// </summary>
    /// <param name="scale"></param>
    public void SetVideoResolutionScale(double scale)
    {
        if (_videoSender == null)
        {
            Debug.LogWarning("[Peer] Video sender is null");
            return;
        }

        RTCRtpSendParameters parameters = _videoSender.GetParameters();
        //这个scale是被除数，长和宽都会除。scale=2，则size是960*540

        foreach (RTCRtpEncodingParameters encoding in parameters.encodings)
        {
            encoding.scaleResolutionDownBy = scale;
        }

        RTCError error = _videoSender.SetParameters(parameters);

        if (error.errorType != RTCErrorType.None)
        {
            Debug.LogError($"[Video] SetResolutionScale failed: {error.message}");
            return;
        }

        Debug.Log($"[Video] ScaleResolutionDownBy = {scale}");
    }

    public void PrintVideoParameters()
    {
        if (_videoSender == null)
        {
            Debug.LogWarning("[Peer] Video sender is null");
            return;
        }

        RTCRtpSendParameters parameters = _videoSender.GetParameters();

        Debug.Log($"[Video] Encoding count: {parameters.encodings.Length}");

        for (int i = 0; i < parameters.encodings.Length; i++)
        {
            RTCRtpEncodingParameters encoding = parameters.encodings[i];

            Debug.Log($"[Video] Encoding {i}: active={encoding.active}, maxBitrate={encoding.maxBitrate}, maxFramerate={encoding.maxFramerate}, scaleResolutionDownBy={encoding.scaleResolutionDownBy}");
        }
    }

    #endregion
    public async Task<string> CreateOfferAsync()
    {
        var op = _peer.CreateOffer();
        await WebRtcAwaiter.WaitAsync(op);
        if (op.IsError)
        {
            throw new Exception(op.Error.message);
        }
        RTCSessionDescription description = op.Desc;

        RTCSetSessionDescriptionAsyncOperation setLocalOp = _peer.SetLocalDescription(ref description);
        await WebRtcAwaiter.WaitAsync(setLocalOp);
        if (setLocalOp.IsError)
        {
            throw new Exception(setLocalOp.Error.message);
        }
        Debug.Log("[Peer] CreateOffer");
        return description.sdp;
    }

    public async Task<string> CreateAnswerAsync()
    {
        RTCSessionDescriptionAsyncOperation answerOp = _peer.CreateAnswer();
        await WebRtcAwaiter.WaitAsync(answerOp);

        var answer = answerOp.Desc;

        RTCSetSessionDescriptionAsyncOperation setLocalOp = _peer.SetLocalDescription(ref answer);
        await WebRtcAwaiter.WaitAsync(setLocalOp);
        Debug.Log("[Peer] CreateAnswer");
        return answer.sdp;
    }

    public async Task SetRemoteOfferAsync(string sdp)
    {
        RTCSessionDescription offer = new RTCSessionDescription
        {
            type = RTCSdpType.Offer,
            sdp = sdp
        };

        RTCSetSessionDescriptionAsyncOperation setRemoteOp = _peer.SetRemoteDescription(ref offer);
        await WebRtcAwaiter.WaitAsync(setRemoteOp);
        if (setRemoteOp.IsError)
        {
            throw new Exception(setRemoteOp.Error.message);
        }

        //处理缓存candidate
        _remoteDescriptionSet = true;
        FlushPendingCandidates();
        Debug.Log("[Peer] SetRemoteOffer");
    }
    public async Task SetRemoteAnswerAsync(string sdp)
    {
        RTCSessionDescription answer = new RTCSessionDescription
        {
            type = RTCSdpType.Answer,
            sdp = sdp
        };

        RTCSetSessionDescriptionAsyncOperation setLocalOp = _peer.SetRemoteDescription(ref answer);
        await WebRtcAwaiter.WaitAsync(setLocalOp);
        Debug.Log("[Peer] SetRemoteAnswer");
    }
    
    
    public void Dispose()
    {
        foreach (PeerChannel channel in _dataChannels.Values)
        {
            channel.Dispose();
        }

        _dataChannels.Clear();
        if (_peer != null)
        {
            _peer.OnIceCandidate = null;
            _peer.OnDataChannel = null;
            _peer.OnConnectionStateChange = null;
            _peer.OnIceConnectionChange = null;

            _peer.Close();
            _peer.Dispose();
            _peer = null;
        }

        
    }
}
