using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public interface ISignalingClient : IDisposable
{
    public bool IsConnected { get; }

    public event Action Connected;
    public event Action DisConnected;
    public event Action<SignalMessage> OnMessageReceived;

    public Task ConnectAsync(Uri uri);
    public Task SendAsync(SignalMessage message);

    public Task LoginAsync(string clientId);
    
    public Task DisConnectAsync();
    
}
