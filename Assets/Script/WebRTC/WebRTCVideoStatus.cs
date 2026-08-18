using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;
using System.Threading.Tasks;
using System.Threading;

public class WebRTCVideoStats
{
    
    public OutboundVideoStats Outbound { get; }
    public InboundVideoStats Inbound { get; }
    public event Action<OutboundVideoStats> OnOutboundStatsUpdated;
    public event Action<InboundVideoStats> OnInboundStatsUpdated;

    private CancellationTokenSource statsCts;
    private Task statsTask;

    public WebRTCVideoStats()
    {
        Outbound = new OutboundVideoStats();
        Inbound = new InboundVideoStats();
    }

    public void Start(RTCRtpSender sender, RTCRtpReceiver receiver)
    {
        Stop();
        statsCts = new CancellationTokenSource();
        statsTask = StatsLoop(sender, receiver, statsCts.Token);
    }

    private async Task StatsLoop(RTCRtpSender sender,RTCRtpReceiver receiver,CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UpdateStats(sender, receiver);

            try
            {
                await Task.Delay(1000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task UpdateStats(RTCRtpSender sender,RTCRtpReceiver receiver)
    {
        Task senderTask = null;
        Task receiverTask = null;

        if (sender != null)
            senderTask = UpdateSender(sender);

        if (receiver != null)
            receiverTask = UpdateReceiver(receiver);

        if (senderTask != null && receiverTask != null)
            await Task.WhenAll(senderTask, receiverTask);
        else if (senderTask != null)
            await senderTask;
        else if (receiverTask != null)
            await receiverTask;
    }

    public void Stop()
    {
        if (statsCts == null)
            return;

        statsCts.Cancel();
        statsCts.Dispose();
        statsCts = null;
        statsTask = null;
    }

    public async Task UpdateSender(RTCRtpSender sender)
    {
        if (sender == null) return;
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
                OnOutboundStatsUpdated?.Invoke(Outbound);
                break;
            }
        }
        
    }

    public async Task UpdateReceiver(RTCRtpReceiver receiver)
    {
        if (receiver == null) return;
        var op = receiver.GetStats();
        await WebRtcAwaiter.WaitAsync(op);
        RTCStatsReport report = op.Value;
        foreach(var stat in report.Stats.Values)
        {
            if (stat.Type != RTCStatsType.InboundRtp)
                continue;
            if(stat is RTCInboundRTPStreamStats inbound)
            {
                Inbound.Update(inbound);
                OnInboundStatsUpdated?.Invoke(Inbound);
                break;
            }
        }
    }
}

public class OutboundVideoStats
{
    //webrtc原始数据
    public bool IsActive { get; private set; }
    public ulong Ssrc { get; private set; }
    //RTP发送
    public ulong PacketsSent { get; private set; }
    public ulong BytesSent { get; private set; }
    //视频
    public ulong FramesEncoded { get; private set; }
    public ulong FramesSent { get; private set; }

    public uint FrameWidth { get; private set; }
    public uint FrameHeight { get; private set; }
    public double FramesPerSecond { get; private set; }

    //码率
    public double TargetBitrate { get; private set; }
    public double Bitrate { get; private set; }
    //编码
    public double TotalEncodeTime { get; private set; }
    public string EncoderImplementation { get; private set; }//实际使用的什么解码器

    //质量限制
    public string QualityLimitationReason { get; private set; }//受影响的原因
    public uint QualityLimitationResolutionChanges { get; private set; }//自己变动的次数

    //内部计算所用变量
    private ulong lastBytesSent;
    private float lastUpdateTime;
    private bool hasPreviousData;

    public void Update(RTCOutboundRTPStreamStats stats)
    {
        IsActive = stats.active;
        Ssrc = stats.ssrc;

        PacketsSent = stats.packetsSent;
        BytesSent = stats.bytesSent;

        FramesEncoded = stats.framesEncoded;
        FramesSent = stats.framesSent;

        FrameWidth = stats.frameWidth;
        FrameHeight = stats.frameHeight;

        FramesPerSecond = stats.framesPerSecond;

        TargetBitrate = stats.targetBitrate;

        TotalEncodeTime = stats.totalEncodeTime;

        QualityLimitationReason = stats.qualityLimitationReason;
        QualityLimitationResolutionChanges = stats.qualityLimitationResolutionChanges;

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

    public void Reset()
    {
        Ssrc = 0;

        PacketsSent = 0;
        BytesSent = 0;

        FramesEncoded = 0;
        FramesSent = 0;

        FrameWidth = 0;
        FrameHeight = 0;

        FramesPerSecond = 0;

        TargetBitrate = 0;
        Bitrate = 0;

        TotalEncodeTime = 0;
        EncoderImplementation = null;

        QualityLimitationReason = null;
        QualityLimitationResolutionChanges = 0;

        lastBytesSent = 0;
        lastUpdateTime = 0;
        hasPreviousData = false;
    }
}

public class InboundVideoStats
{
    //基础信息
    public ulong Ssrc { get; private set; }


    //RTP接收
    public ulong PacketsReceived { get; private set; }
    public ulong BytesReceived { get; private set; }


    //视频
    public ulong FramesReceived { get; private set; }
    public ulong FramesDecoded { get; private set; }

    public uint FrameWidth { get; private set; }
    public uint FrameHeight { get; private set; }

    public double FramesPerSecond { get; private set; }


    //码率
    public double Bitrate { get; private set; }


    //解码
    public double TotalDecodeTime { get; private set; }
    public string DecoderImplementation { get; private set; }//使用的什么解码器


    // ===== 内部计算 =====

    private ulong lastBytesReceived;
    private float lastUpdateTime;
    private bool hasPreviousData;


    public void Update(RTCInboundRTPStreamStats stats)
    {
        Ssrc = stats.ssrc;
        PacketsReceived = stats.packetsReceived;
        BytesReceived = stats.bytesReceived;
        FramesReceived = stats.framesReceived;
        FramesDecoded = stats.framesDecoded;
        FrameWidth = stats.frameWidth;
        FrameHeight = stats.frameHeight;
        FramesPerSecond = stats.framesPerSecond;
        TotalDecodeTime = stats.totalDecodeTime;
        DecoderImplementation = stats.decoderImplementation;

        UpdateBitrate();
    }


    private void UpdateBitrate()
    {
        // 后面实现
        float currentTime = Time.realtimeSinceStartup;

        if (hasPreviousData)
        {
            float deltaTime = currentTime - lastUpdateTime;
            ulong deltaBytes = BytesReceived - lastBytesReceived;

            if (deltaTime > 0)
            {
                Bitrate = deltaBytes * 8.0 / deltaTime;
            }
        }

        lastBytesReceived = BytesReceived;
        lastUpdateTime = currentTime;
        hasPreviousData = true;
    }


    public void Reset()
    {
        // 后面实现
    }
}
