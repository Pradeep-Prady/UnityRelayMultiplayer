using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public static event Action<LobbyManager> InstanceChanged;

    [SerializeField] private LobbyPlayer lobbyPlayerPrefab;
    [SerializeField] private string gameSceneName = "GameScene";

    private readonly List<LobbyPlayer> players = new List<LobbyPlayer>();
    private readonly Dictionary<ulong, LobbyPlayer> playersByClientId = new Dictionary<ulong, LobbyPlayer>();

    public event Action LobbyPlayersChanged;

    public IReadOnlyList<LobbyPlayer> Players => players;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple LobbyManager instances detected. Replacing previous instance.");
        }

        Instance = this;
        InstanceChanged?.Invoke(this);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            EnsureLobbyPlayer(clientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke(null);
        }
    }

    public void RegisterPlayer(LobbyPlayer player)
    {
        if (player == null || players.Contains(player))
        {
            return;
        }

        players.Add(player);
        playersByClientId[player.OwnerClientId] = player;
        player.StateChanged += HandlePlayerStateChanged;
        NotifyLobbyPlayersChanged();
    }

    public void UnregisterPlayer(LobbyPlayer player)
    {
        if (player == null)
        {
            return;
        }

        player.StateChanged -= HandlePlayerStateChanged;

        bool removed = players.Remove(player);

        if (playersByClientId.TryGetValue(player.OwnerClientId, out LobbyPlayer registeredPlayer) &&
            registeredPlayer == player)
        {
            playersByClientId.Remove(player.OwnerClientId);
            removed = true;
        }

        if (removed)
        {
            NotifyLobbyPlayersChanged();
        }
    }

    public bool TryGetLocalPlayer(out LobbyPlayer player)
    {
        player = null;

        if (NetworkManager.Singleton == null)
        {
            return false;
        }

        return playersByClientId.TryGetValue(NetworkManager.Singleton.LocalClientId, out player);
    }

    public bool AreAllPlayersReady()
    {
        if (players.Count == 0)
        {
            return false;
        }

        foreach (LobbyPlayer player in players)
        {
            if (!player.IsReady)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryStartGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only the host can start the game.");
            return false;
        }

        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("Cannot start the game until all lobby players are ready.");
            return false;
        }

        NetworkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        return true;
    }

    private void HandleClientConnected(ulong clientId)
    {
        EnsureLobbyPlayer(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!playersByClientId.TryGetValue(clientId, out LobbyPlayer player))
        {
            return;
        }

        playersByClientId.Remove(clientId);

        if (player != null && player.NetworkObject != null && player.NetworkObject.IsSpawned)
        {
            player.NetworkObject.Despawn(true);
        }
    }

    private void EnsureLobbyPlayer(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        if (lobbyPlayerPrefab == null)
        {
            Debug.LogError("LobbyManager is missing the LobbyPlayer prefab reference.");
            return;
        }

        if (playersByClientId.TryGetValue(clientId, out LobbyPlayer existingPlayer) &&
            existingPlayer != null &&
            existingPlayer.NetworkObject != null &&
            existingPlayer.NetworkObject.IsSpawned)
        {
            return;
        }

        LobbyPlayer lobbyPlayerInstance = Instantiate(lobbyPlayerPrefab);
        lobbyPlayerInstance.NetworkObject.SpawnWithOwnership(clientId, true);
    }

    private void HandlePlayerStateChanged(LobbyPlayer _)
    {
        NotifyLobbyPlayersChanged();
    }

    private void NotifyLobbyPlayersChanged()
    {
        LobbyPlayersChanged?.Invoke();
    }
}
