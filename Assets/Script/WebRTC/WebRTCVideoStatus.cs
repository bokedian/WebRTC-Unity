using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;

public class WebRTCVideoStats
{
    
    public OutboundVideoStats Outbound { get; } = new OutboundVideoStats();

    public WebRTCVideoStats()
    {
        Outbound = new OutboundVideoStats();
    }

    public async void UpdateSender(RTCRtpSender sender)
    {
        var op = sender.GetStats();
        await WebRtcAwaiter.WaitAsync(op);
        RTCStatsReport report = op.Value;

        foreach (var stat in report.Stats.Values)
        {
            if (stat.Type != RTCStatsType.OutboundRtp)
                continue;

            if (stat is RTCOutboundRTPStreamStats outbound)
            {
                Outbound.Update(outbound);

                break;
            }
        }
    }

    public void UpdateReceiver(RTCRtpReceiver receiver)
    {

    }
}

public class OutboundVideoStats
{
    //webrtc原始数据
    public ulong Ssrc;
    public ulong PacketsSent;
    public ulong BytesSent;

    public ulong FramesEncoded;
    public ulong FramesSent;
    public ulong KeyFramesEncoded;

    public uint FrameWidth;
    public uint FrameHeight;
    public double FramesPerSecond;

    public double TotalEncodeTime;
    public double TargetBitrate;
    //自己计算数据
    public double Bitrate { get; private set; }
    //内部计算状态
    private ulong lastBytesSent;
    private float lastUpdateTime;
    private bool hasPreviousData;

    public void Update(RTCOutboundRTPStreamStats stats)
    {
        Ssrc = stats.ssrc;

        PacketsSent = stats.packetsSent;
        BytesSent = stats.bytesSent;

        FramesEncoded = stats.framesEncoded;
        FramesSent = stats.framesSent;
        KeyFramesEncoded = stats.keyFramesEncoded;

        FrameWidth = stats.frameWidth;
        FrameHeight = stats.frameHeight;

        FramesPerSecond = stats.framesPerSecond;

        TargetBitrate = stats.targetBitrate;

        TotalEncodeTime = stats.totalEncodeTime;

        UpdateBitrate();
    }

    private void UpdateBitrate()
    {
        float currentTime = Time.realtimeSinceStartup;

        if (hasPreviousData)
        {
            float deltaTime = currentTime - lastUpdateTime;
            ulong deltaBytes = BytesSent - lastBytesSent;

            if (deltaTime > 0)
            {
                Bitrate = deltaBytes * 8.0 / deltaTime;
            }
        }

        lastBytesSent = BytesSent;
        lastUpdateTime = currentTime;
        hasPreviousData = true;
    }
}
