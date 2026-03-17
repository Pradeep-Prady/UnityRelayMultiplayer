using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System;

public class RelayManager : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;
    private UnityTransport transport;

    private void Awake()
    {
        TryResolveDependencies();
    }

    public async Task<string> CreateRelay(int maxConnections)
    {
        await EnsureServicesInitialized();
        TryResolveDependencies();

        Debug.Log("Creating Relay Allocation...");

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.ConnectionData,
            false
        );

        networkManager.StartHost();

        Debug.Log("Relay Created. Join Code: " + joinCode);

        return joinCode;
    }

    public async Task<bool> JoinRelay(string joinCode)
    {
        await EnsureServicesInitialized();
        TryResolveDependencies();

        Debug.Log("Joining Relay with code: " + joinCode);

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.HostConnectionData,
            false
        );

        networkManager.StartClient();

        return true;
    }

    private async Task EnsureServicesInitialized()
    {
        if (UnityServicesInitializer.InitializationTask != null)
        {
            await UnityServicesInitializer.InitializationTask;
            return;
        }

        throw new InvalidOperationException(
            "Unity Services are not initialized. Add an active UnityServicesInitializer to the scene before hosting or joining."
        );
    }

    private void TryResolveDependencies()
    {
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton != null
                ? NetworkManager.Singleton
                : FindFirstObjectByType<NetworkManager>();
        }

        if (networkManager == null)
        {
            throw new InvalidOperationException(
                "RelayManager could not find a NetworkManager in the active scene."
            );
        }

        if (transport == null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
        }

        if (transport == null)
        {
            throw new InvalidOperationException(
                "RelayManager requires a UnityTransport component on the NetworkManager object."
            );
        }
    }
}
