using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Unity.WebRTC;
using System;

/// <summary>
/// 仅供webrtc使用的通用协程异步封装
/// </summary>
public static class WebRtcAwaiter
{
    /// <summary>
    /// 此方法是协程封装成的Task，是单线程
    /// </summary>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static Task WaitAsync(AsyncOperationBase operation)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CoroutineRunner.Instance.StartCoroutine(WaitCoroutine(operation, tcs));
        return tcs.Task;
        
    }

    private static IEnumerator WaitCoroutine(AsyncOperationBase operation, TaskCompletionSource<bool> tcs)
    {
        yield return operation;
        if (operation.IsError)
        {
            tcs.SetException(new Exception($"{operation.Error.errorType.ToString()} : {operation.Error.message}"));
            tcs.SetResult(false);
            
        }
        else
        {
            tcs.SetResult(true);
        }
        yield break;
        
    }
}
