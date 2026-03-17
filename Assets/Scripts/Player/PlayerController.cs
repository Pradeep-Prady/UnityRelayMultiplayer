using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : NetworkBehaviour
{
    public float moveSpeed = 5f;

    [Header("Name UI")]
    public TMP_Text nameText;

    private NetworkVariable<FixedString32Bytes> playerName =
        new NetworkVariable<FixedString32Bytes>(
            "",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private bool cameraAttached;

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnNameChanged;

        if (IsLocalOwnedPlayer())
        {
            // Load player name from menu
            string myName = PlayerPrefs.GetString("PLAYER_NAME", "Player");

            SubmitNameServerRpc(myName);
            TryAttachCamera();
        }

        UpdateNameUI(playerName.Value.ToString());
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
    }

    void Update()
    {
        if (!IsLocalOwnedPlayer()) return;

        if (!cameraAttached)
        {
            TryAttachCamera();
        }

        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            float x = 0;
            float z = 0;

            if (Keyboard.current.aKey.isPressed) x -= 1;
            if (Keyboard.current.dKey.isPressed) x += 1;
            if (Keyboard.current.wKey.isPressed) z += 1;
            if (Keyboard.current.sKey.isPressed) z -= 1;

            input = new Vector2(x, z);
        }

        Vector3 move = new Vector3(input.x, 0f, input.y);

        if (move.sqrMagnitude <= 0f) return;

        // Movement is applied only by the owning client.
        // NetworkTransform synchronizes this to the other peers.
        transform.position += move * moveSpeed * Time.deltaTime;
    }

    [ServerRpc]
    private void SubmitNameServerRpc(string newName)
    {
        playerName.Value = newName;
    }

    private void OnNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
    {
        UpdateNameUI(newName.ToString());
    }

    private void UpdateNameUI(string newName)
    {
        if (nameText != null)
        {
            nameText.text = newName;
        }
    }

    public string GetPlayerName()
    {
        return playerName.Value.ToString();
    }

    private void TryAttachCamera()
    {
        if (!IsLocalOwnedPlayer())
        {
            return;
        }

        Camera mainCamera = Camera.main;
        Scene playerScene = gameObject.scene;

        // Ignore a main camera coming from another scene (for example DontDestroyOnLoad).
        if (mainCamera != null && mainCamera.gameObject.scene != playerScene)
        {
            mainCamera = null;
        }

        if (mainCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);

            foreach (Camera cameraCandidate in cameras)
            {
                if (cameraCandidate.gameObject.scene == playerScene)
                {
                    mainCamera = cameraCandidate;
                    break;
                }
            }
        }

        if (mainCamera == null)
        {
            return;
        }

        CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();

        if (cameraFollow == null)
        {
            cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
            Debug.Log("Added CameraFollow to runtime camera: " + mainCamera.gameObject.name);
        }

        cameraFollow.SetTarget(transform);
        cameraAttached = true;

        Debug.Log(
            $"Camera bound to player netId={NetworkObjectId} owner={OwnerClientId} local={NetworkManager.Singleton.LocalClientId}"
        );
    }

    private bool IsLocalOwnedPlayer()
    {
        return NetworkManager.Singleton != null &&
            OwnerClientId == NetworkManager.Singleton.LocalClientId;
    }
}
