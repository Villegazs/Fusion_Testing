using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
public class Cage : NetworkBehaviour
{
    [SerializeField] private float lifetime = 5f;

    [Networked] private TickTimer DespawnTimer {  get; set; }
    private Player cagedPlayer;

    public void Init (Player cagedPlayer)
    {
        this.cagedPlayer = cagedPlayer;
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && DespawnTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
            cagedPlayer.IsCaged = false;
        }
    }
}
