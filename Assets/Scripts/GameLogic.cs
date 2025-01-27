using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameLogic : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [Networked, Capacity(4)] private NetworkDictionary<PlayerRef, Player> Players => default;

    public void PlayerJoined(PlayerRef player)
    {
        if (HasStateAuthority)
        {
            NetworkObject playerObject = Runner.Spawn(playerPrefab, Vector3.up, Quaternion.identity, player); //Spawn Method to instatiate player prefab, spawn player on host and replicate the spawn to other clients
            Players.Add(player, playerObject.GetComponent<Player>()); //Add a reference to the newly spawned player script to our dictionary
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (Players.TryGet(player, out Player playerBehaviour))
        {
            Players.Remove(player);
            Runner.Despawn(playerBehaviour.Object);
        }

    }


}
