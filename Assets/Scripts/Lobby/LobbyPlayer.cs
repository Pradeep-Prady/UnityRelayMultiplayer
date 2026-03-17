using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    private const int MaxPlayerNameLength = 32;

    public event Action<LobbyPlayer> StateChanged;

    private bool isRegistered;

    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public string PlayerName => playerName.Value.ToString();
    public bool IsReady => isReady.Value;

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += HandleNameChanged;
        isReady.OnValueChanged += HandleReadyChanged;
        LobbyManager.InstanceChanged += HandleLobbyManagerChanged;

        TryRegisterWithLobbyManager();

        if (IsOwner)
        {
            string savedName = PlayerPrefs.GetString("PLAYER_NAME", "Player").Trim();

            if (string.IsNullOrWhiteSpace(savedName))
            {
                savedName = "Player";
            }

            SubmitLobbyStateServerRpc(savedName, false);
        }

        NotifyStateChanged();
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= HandleNameChanged;
        isReady.OnValueChanged -= HandleReadyChanged;
        LobbyManager.InstanceChanged -= HandleLobbyManagerChanged;

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UnregisterPlayer(this);
        }

        isRegistered = false;
    }

    [ServerRpc]
    private void SubmitLobbyStateServerRpc(string requestedName, bool readyState)
    {
        playerName.Value = SanitizePlayerName(requestedName);
        isReady.Value = readyState;
    }

    [ServerRpc]
    public void SetReadyServerRpc(bool ready)
    {
        isReady.Value = ready;
    }

    private void HandleLobbyManagerChanged(LobbyManager manager)
    {
        if (manager == null)
        {
            isRegistered = false;
            return;
        }

        TryRegisterWithLobbyManager();
    }

    private void TryRegisterWithLobbyManager()
    {
        if (isRegistered || LobbyManager.Instance == null)
        {
            return;
        }

        LobbyManager.Instance.RegisterPlayer(this);
        isRegistered = true;
    }

    private void HandleNameChanged(FixedString32Bytes _, FixedString32Bytes __)
    {
        NotifyStateChanged();
    }

    private void HandleReadyChanged(bool _, bool __)
    {
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(this);
    }

    private static FixedString32Bytes SanitizePlayerName(string rawName)
    {
        string trimmedName = string.IsNullOrWhiteSpace(rawName) ? "Player" : rawName.Trim();

        if (trimmedName.Length > MaxPlayerNameLength)
        {
            trimmedName = trimmedName.Substring(0, MaxPlayerNameLength);
        }

        return new FixedString32Bytes(trimmedName);
    }
}
