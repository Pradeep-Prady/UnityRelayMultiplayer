using UnityEngine;
using TMPro;
using Unity.Netcode;
using System;
using UnityEngine.SceneManagement;

public class NetworkUI : NetworkBehaviour
{
    [Header("Managers")]
    public RelayManager relayManager;

    [Header("UI")]
    public TMP_InputField nameInput;
    public TMP_InputField joinCodeInput;
    public TMP_Text statusText;
    public GameObject canvas;

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // ---------------- HOST ----------------

    public async void StartHost()
    {
        if (relayManager == null)
        {
            statusText.text = "RelayManager missing!";
            return;
        }

        if (string.IsNullOrWhiteSpace(nameInput.text))
        {
            statusText.text = "Enter your name";
            return;
        }

        PlayerPrefs.SetString("PLAYER_NAME", nameInput.text);

        statusText.text = "Creating relay...";

        try
        {
            string joinCode = await relayManager.CreateRelay(1);

            statusText.text = $"Join Code: {joinCode}";
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            statusText.text = "Host failed";
        }
    }

    // ---------------- CLIENT ----------------

    public async void StartClient()
    {
        if (relayManager == null)
        {
            statusText.text = "RelayManager missing!";
            return;
        }

        if (string.IsNullOrWhiteSpace(nameInput.text))
        {
            statusText.text = "Enter your name";
            return;
        }

        string joinCode = joinCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            statusText.text = "Enter join code";
            return;
        }

        PlayerPrefs.SetString("PLAYER_NAME", nameInput.text);

        statusText.text = "Joining lobby...";

        try
        {
            bool success = await relayManager.JoinRelay(joinCode);

            if (!success)
            {
                statusText.text = "Join failed";
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            statusText.text = "Join failed";
        }
    }

    // ---------------- CONNECTION EVENTS ----------------

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("Client connected: " + clientId);

        // Only host controls scene changes
        if (!NetworkManager.Singleton.IsHost)
            return;

        int playerCount = NetworkManager.Singleton.ConnectedClients.Count;

        Debug.Log("Players in session: " + playerCount);

        if (SceneManager.GetActiveScene().name == "GameLobbyScene")
        {
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(
            "GameLobbyScene",
            LoadSceneMode.Single
        );
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log("Client disconnected: " + clientId);

        statusText.text = "Player disconnected";

        if (NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        canvas.SetActive(true);
    }

    // ---------------- UI CONTROL ----------------

    [ClientRpc]
    private void HideUIClientRpc()
    {
        canvas.SetActive(false);
    }
}
