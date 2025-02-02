using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum InputButton
{
    Jump,
    UseAbility,
    Grapple,
    Glide,
}

public struct NetInput : INetworkInput  // Will allow us to feed this struct to fusion so it can be replicated properly to the server
{
    public NetworkButtons Buttons; //Store our button presses
    public Vector2 Direction; //Movement Direction
    public Vector2 LookDelta;
    public AbilityMode AbilityMode;
}