using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;

    private readonly Dictionary<ulong, NetworkObject> spawnedPlayers = new();
    private bool hasSpawnedPlayers = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        // Only spawn players AFTER all clients have finished loading the scene.
        // Spawning earlier causes a race condition where clients miss spawn messages.
        NetworkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager?.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
        }
    }

    private void HandleLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut
    )
    {
        if (!IsServer)
            return;

        if (SceneManager.GetActiveScene().name != sceneName)
            return;

        TrySpawnPlayersForCurrentScene();
    }

    private void TrySpawnPlayersForCurrentScene()
    {
        if (hasSpawnedPlayers)
            return;

        if (playerPrefab == null)
        {
            Debug.LogError("GameManager: Player prefab is missing.");
            return;
        }

        // Find spawn points at runtime by tag instead of relying on serialized references,
        // which can be lost during networked scene loads.
        GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");

        if (spawnPointObjects.Length == 0)
        {
            Debug.LogWarning("GameManager: No SpawnPoint tags found. Spawning at default positions.");
        }

        SpawnPlayers(spawnPointObjects);
        hasSpawnedPlayers = true;
    }

    private void SpawnPlayers(GameObject[] spawnPointObjects)
    {
        int index = 0;

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            ulong clientId = client.ClientId;

            // Prevent duplicate player objects
            if (client.PlayerObject != null && client.PlayerObject.IsSpawned)
            {
                spawnedPlayers[clientId] = client.PlayerObject;
                continue;
            }

            Vector3 spawnPos;
            Quaternion spawnRot;

            if (spawnPointObjects.Length > 0)
            {
                Transform sp = spawnPointObjects[index % spawnPointObjects.Length].transform;
                spawnPos = sp.position;
                spawnRot = sp.rotation;
            }
            else
            {
                // Fallback: spread players apart if no spawn points exist
                spawnPos = new Vector3(index * 2f, 0f, 0f);
                spawnRot = Quaternion.identity;
            }

            GameObject playerInstance = Instantiate(playerPrefab, spawnPos, spawnRot);

            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError("Player prefab must contain a NetworkObject.");
                Destroy(playerInstance);
                return;
            }

            networkObject.SpawnAsPlayerObject(clientId, true);

            spawnedPlayers[clientId] = networkObject;
            index++;

            Debug.Log($"Spawned player for client {clientId} at {spawnPos}");
        }
    }
}
