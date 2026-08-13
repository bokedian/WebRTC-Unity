using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player
{
    public int id;
    public string PlayerName;
    public float maxHp;
    public float maxMp;
    public float attack;
    public float defense;
    public List<Enemy> enemys;
}
public class Enemy
{
    public string Name;
}
