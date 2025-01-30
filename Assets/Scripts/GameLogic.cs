using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GameState
{
    Waiting,
    Playing,
}

public class GameLogic : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform SpawnPointPivot;

    [Networked] private Player Winner {  get; set; }
    [Networked, OnChangedRender(nameof(GameStateChanged))] private GameState State { get; set; }
    [Networked, Capacity(4)] private NetworkDictionary<PlayerRef, Player> Players => default;

    public override void Spawned()
    {
        Winner = null;
        State = GameState.Waiting;
        UIManager.Singleton.SetWaitUI(State, Winner);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Detect when a player enters the finish platforms trigger collider
        if (Runner.IsServer && Winner == null && other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent(out Player player)) 
        {
            UnreadyAll();
            Winner = player;
            State = GameState.Waiting;
            Runner.SetIsSimulated(Object, true);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Players.Count < 1)
            return;

        if (Runner.IsServer && State == GameState.Waiting)
        {

            bool areAllReady = true;

            foreach(KeyValuePair<PlayerRef, Player> player in Players)
            {
                if (!player.Value.IsReady)
                {
                    areAllReady = false;
                    break;
                }
            }

            if (areAllReady)
            {
                Winner = null;
                State = GameState.Playing;
                PreparePlayers();
            }
        }

        if(State == GameState.Playing && !Runner.IsResimulation)
            UIManager.Singleton.UpdateLeaderboard(Players.OrderByDescending(p => p.Value.Score).ToArray());
    }

    private void GameStateChanged()
    {
        UIManager.Singleton.SetWaitUI(State, Winner);
    }
    private void PreparePlayers() //Teleport players for each spawnpoint
    {
        float spacingAngle = 360f / Players.Count;
        SpawnPointPivot.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        foreach (KeyValuePair<PlayerRef, Player> player in Players)
        {
            GetNextSpawnPoint(spacingAngle, out Vector3 position, out Quaternion rotation);
            player.Value.Teleport(position, rotation);
        }
    }

    private void UnreadyAll()
    {
        foreach (KeyValuePair<PlayerRef, Player> player in Players)
            player.Value.IsReady = false;
    }

    private void GetNextSpawnPoint(float spacingAngle, out Vector3 position, out Quaternion rotation)
    {
        position = spawnPoint.position;
        rotation = spawnPoint.rotation;
        SpawnPointPivot.Rotate(0f, spacingAngle, 0f);
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (HasStateAuthority)
        {
            //NetworkObject playerObject = Runner.Spawn(playerPrefab, Vector3.up, Quaternion.identity, player); //Spawn Method to instatiate player prefab, spawn player on host and replicate the spawn to other clients
            GetNextSpawnPoint(90f, out Vector3 position, out Quaternion rotation);
            NetworkObject playerObject = Runner.Spawn(playerPrefab, position, rotation, player);
            Players.Add(player, playerObject.GetComponent<Player>()); //Add a reference to the newly spawned player script to our dictionary
            Debug.Log("Player Spawned");
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
