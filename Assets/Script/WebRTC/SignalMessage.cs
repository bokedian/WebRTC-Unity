using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SignalMessageType
{
    Offer,
    Answer,
    Candidate
}

public class SignalMessage
{
    public SignalMessageType Type;
    public string From;
    public string To;
    public string Data;//直接用string存储原始信息
}
