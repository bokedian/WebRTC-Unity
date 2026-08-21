using System;
using System.Threading.Tasks;
using Unity.WebRTC;
using LitJson;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SessionManager : IDisposable
{
    private string _remoteId;
    private PeerConnectionController _peerConnection;
    private string _localId;
    private ISignalingClient _signalingClient;
    public RawImage showImage;//用于播放的
    public VideoStreamTrack remoteVideoTrack;//远端的视频来源

    public SessionManager(string localId, PeerConnectionController peer,ISignalingClient client)
    {
        _localId = localId;
        _peerConnection = peer;
        _signalingClient = client;
    }

    public void Init()
    {
        //注册对应回调
        _peerConnection.OnIceCandidateGenerated += OnIceCandidate;
        _peerConnection.OnVideoReceive += ReceiveVideo;

        _signalingClient.OnMessageReceived += OnSignalMessageReceived;

    }
    /// <summary>
    /// 连接某个客户端
    /// </summary>
    /// <param name="remoteId"></param>
    /// <returns></returns>
    public async Task StartOfferAsync(string remoteId)
    {
        _remoteId = remoteId;
        string sdp = await _peerConnection.CreateOfferAsync();
        SignalMessage message = new SignalMessage
        {
            Type = SignalMessageType.Offer,
            From = _localId,
            To = _remoteId,
            Data = sdp
        };
        Debug.Log("[Session] Send Offer");
        await _signalingClient.SendAsync(message);
    }

    private async void OnIceCandidate(IceCandidateInfo info)
    {
        SignalMessage message = new SignalMessage()
        {
            Type = SignalMessageType.Candidate,
            From = _localId,
            To = _remoteId,
            Data = JsonMapper.ToJson(info)
        };
        await _signalingClient.SendAsync(message);
    }

    private async void OnSignalMessageReceived(SignalMessage message)
    {
        Debug.Log("[Session] OnSignalMessageReceived");
        switch (message.Type)
        {
            case SignalMessageType.Offer:
                await HandleOfferAsync(message);
                break;
            case SignalMessageType.Answer:
                await HandleAnswerAsync(message);
                break;
            case SignalMessageType.Candidate:
                HandleCandidateAsync(message);
                break;
        }
    }

    public void ReceiveVideo(Texture texture)
    {
        if (showImage != null)
        {
            showImage.texture = texture;
            remoteVideoTrack = _peerConnection._remoteVideoTrack;
        }
    }

    public void SetFramerate(uint rate)
    {
        _peerConnection.SetVideoFramerate(rate);
    }

    public void SetVideoSize(double scale)
    {
        _peerConnection.SetVideoResolutionScale(scale);
    }

    private async Task HandleOfferAsync(SignalMessage message)
    {
        Debug.Log("[Session] Receive Offer");
        _remoteId = message.From;
        await _peerConnection.SetRemoteOfferAsync(message.Data);
        //收到了offer，需要给对面answer
        string answer = await _peerConnection.CreateAnswerAsync();
        SignalMessage res = new SignalMessage()
        {
            Type = SignalMessageType.Answer,
            From = _localId,
            To = _remoteId,
            Data = answer
        };
        await _signalingClient.SendAsync(res);
    }

    private async Task HandleAnswerAsync(SignalMessage message)
    {
        //收到answer只需要本地设置，不需要发送
        await _peerConnection.SetRemoteAnswerAsync(message.Data);
    }

    private void HandleCandidateAsync(SignalMessage message)
    {
        Debug.Log("[Session] Receive Candidate");
        IceCandidateInfo candidate = JsonMapper.ToObject<IceCandidateInfo>(message.Data);
        _peerConnection.AddIceCandidateAsync(candidate);
    }
    /// <summary>
    /// 在datachannel中发送信息
    /// </summary>
    /// <param name="message"></param>
    public void SendData(string label, string message)
    {
        _peerConnection.Send(label, message);
    }

    public void CreateVideoTrack(Camera cam)
    {
        _peerConnection.CreateVideoTrack(cam);
    }

    public void CreateAudioTrack(AudioSource source)
    {
        _peerConnection.CreateAudioTrack(source);
    }
    public void PrintPeerCandidate()
    {
        _peerConnection.PrintIceCandidatePairs();
    }

    public void CreateDataChannel(string label)
    {
        _peerConnection.CreateDataChannel(label);
    }
    public void Dispose()
    {
        _peerConnection.OnIceCandidateGenerated -= OnIceCandidate;

        _signalingClient.OnMessageReceived -= OnSignalMessageReceived;
    }
}
