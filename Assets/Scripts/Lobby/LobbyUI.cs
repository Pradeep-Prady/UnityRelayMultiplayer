using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerList;
    [SerializeField] private GameObject playerRowPrefab;
    [SerializeField] private GameObject readyButton;
    [SerializeField] private GameObject startGameButton;

    private Button readyButtonComponent;
    private Button startGameButtonComponent;
    private TMP_Text readyButtonLabel;
    private LobbyManager subscribedLobbyManager;

    private void Awake()
    {
        readyButtonComponent = readyButton != null ? readyButton.GetComponent<Button>() : null;
        startGameButtonComponent = startGameButton != null ? startGameButton.GetComponent<Button>() : null;
        readyButtonLabel = readyButton != null ? readyButton.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void OnEnable()
    {
        LobbyManager.InstanceChanged += HandleLobbyManagerChanged;
        TrySubscribeToLobbyManager();
        RefreshUI();
    }

    private void OnDisable()
    {
        LobbyManager.InstanceChanged -= HandleLobbyManagerChanged;
        UnsubscribeFromLobbyManager();
    }

    public void OnReadyPressed()
    {
        if (LobbyManager.Instance == null)
        {
            return;
        }

        if (!LobbyManager.Instance.TryGetLocalPlayer(out LobbyPlayer localPlayer))
        {
            return;
        }

        localPlayer.SetReadyServerRpc(!localPlayer.IsReady);
    }

    public void OnStartGamePressed()
    {
        LobbyManager.Instance?.TryStartGame();
    }

    private void HandleLobbyManagerChanged(LobbyManager _)
    {
        TrySubscribeToLobbyManager();
        RefreshUI();
    }

    private void TrySubscribeToLobbyManager()
    {
        if (subscribedLobbyManager == LobbyManager.Instance)
        {
            return;
        }

        UnsubscribeFromLobbyManager();

        subscribedLobbyManager = LobbyManager.Instance;

        if (subscribedLobbyManager != null)
        {
            subscribedLobbyManager.LobbyPlayersChanged += RefreshUI;
        }
    }

    private void UnsubscribeFromLobbyManager()
    {
        if (subscribedLobbyManager == null)
        {
            return;
        }

        subscribedLobbyManager.LobbyPlayersChanged -= RefreshUI;
        subscribedLobbyManager = null;
    }

    private void RefreshUI()
    {
        RebuildPlayerList();
        RefreshButtons();
    }

    private void RebuildPlayerList()
    {
        if (playerList == null)
        {
            return;
        }

        for (int i = playerList.childCount - 1; i >= 0; i--)
        {
            Destroy(playerList.GetChild(i).gameObject);
        }

        if (LobbyManager.Instance == null || playerRowPrefab == null)
        {
            return;
        }

        foreach (LobbyPlayer player in LobbyManager.Instance.Players)
        {
            GameObject rowObject = Instantiate(playerRowPrefab, playerList);
            PlayerRow row = rowObject.GetComponent<PlayerRow>();

            if (row == null)
            {
                row = rowObject.AddComponent<PlayerRow>();
            }

            bool isLocalPlayer = NetworkManager.Singleton != null &&
                player.OwnerClientId == NetworkManager.Singleton.LocalClientId;

            row.SetData(player.PlayerName, player.IsReady, isLocalPlayer);
        }
    }

    private void RefreshButtons()
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        if (startGameButton != null)
        {
            startGameButton.SetActive(isHost);
        }

        if (startGameButtonComponent != null)
        {
            startGameButtonComponent.interactable = isHost &&
                LobbyManager.Instance != null &&
                LobbyManager.Instance.AreAllPlayersReady();
        }

        if (readyButtonComponent != null)
        {
            readyButtonComponent.interactable = LobbyManager.Instance != null &&
                LobbyManager.Instance.TryGetLocalPlayer(out _);
        }

        if (readyButtonLabel != null)
        {
            if (LobbyManager.Instance != null &&
                LobbyManager.Instance.TryGetLocalPlayer(out LobbyPlayer localPlayer))
            {
                readyButtonLabel.text = localPlayer.IsReady ? "UNREADY" : "READY";
            }
            else
            {
                readyButtonLabel.text = "READY";
            }
        }
    }
}
