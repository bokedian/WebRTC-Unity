using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.WebRTC;
using System;

public class Test : MonoBehaviour
{
    WebSocketSignalingClient client;
    private async void Start()
    {
        client = new WebSocketSignalingClient();
        await client.ConnectAsync(new Uri("ws://localhost:5191/ws"));
        await client.LoginAsync("bokedian");
    }

    private async void OnDestroy()
    {
        await client.DisConnectAsync();
        client.Dispose();
    }


}
