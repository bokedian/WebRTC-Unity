using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using LitJson;
using System.Text;

/// <summary>
/// 用作信令交换的客户端
/// </summary>
public class WebSocketSignalingClient : ISignalingClient
{
    private readonly ClientWebSocket _socket = new ClientWebSocket();
    private CancellationTokenSource _cts;
    private Task _receiveTask;
    public bool IsConnected => _socket.State == WebSocketState.Open;

    public event Action Connected;
    public event Action DisConnected;
    public event Action<SignalMessage> OnMessageReceived;

    public async Task ConnectAsync(Uri uri)
    {
        _cts = new CancellationTokenSource();
        await _socket.ConnectAsync(uri, _cts.Token);
        Connected?.Invoke();
        _receiveTask = ReceiveLoopAsync();
    }

    private async Task ReceiveLoopAsync()
    {
        Debug.Log("开启websocket Receive Loop");
        byte[] buffer = new byte[8192];
        while (IsConnected)
        {
            WebSocketReceiveResult result = await _socket.ReceiveAsync(buffer, _cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await DisConnectAsync();
                return;
            }
            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
#if UNITY_EDITOR
            Debug.Log($"[WebSocket] Receive {json}");
#else
            Debug.Log("[WebSocket] Receive");
#endif
            SignalMessage message = JsonMapper.ToObject<SignalMessage>(json);
            OnMessageReceived?.Invoke(message);
        }
    } 

    public async Task DisConnectAsync()
    {
        if (_socket.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
        }
        _cts?.Cancel();
        DisConnected?.Invoke();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        _socket?.Dispose();
    }

    public async Task LoginAsync(string clientId)
    {
        LoginRequest request = new LoginRequest { ClientId = clientId };
        string json = JsonMapper.ToJson(request);

        await SendTextAsync(json);
    }

    public async Task SendAsync(SignalMessage message)
    {
        string json = JsonMapper.ToJson(message);
        await SendTextAsync(json);
    }
    /// <summary>
    /// 真正的发送函数
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private async Task SendTextAsync(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);

        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
    }
}
