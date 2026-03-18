using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : NetworkBehaviour
{
    private const float WalkSpeed = 0.5f;
    private const float RunSpeed = 1.0f;
    private const float AnimSyncThreshold = 0.01f;
    private const float AttackCooldown = 0.8f;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 5f;

    [Header("Name UI")]
    [SerializeField] private TMP_Text nameText;

    [Header("Animation")]
    [SerializeField] private Animator bodyAnimator;

    [Header("Combat")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private LayerMask playerLayer;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int AttackLayerIndex = 0;

    private bool cameraAttached;
    private bool isAttacking;
    private float lastAttackTime = float.MinValue;

    private readonly NetworkVariable<FixedString32Bytes> playerName =
        new NetworkVariable<FixedString32Bytes>(
            "",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<Vector2> movementInput =
        new NetworkVariable<Vector2>(
            Vector2.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    private void Awake()
    {
        if (bodyAnimator == null)
        {
            Debug.LogError("[PlayerController] Body Animator is not assigned on the prefab.");
        }
    }

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnNameChanged;
        movementInput.OnValueChanged += OnMovementInputChanged;

        if (IsLocalOwnedPlayer())
        {
            string myName = PlayerPrefs.GetString("PLAYER_NAME", "Player");
            SubmitNameServerRpc(myName);
            TryAttachCamera();
        }

        UpdateNameUI(playerName.Value.ToString());
        ApplyMovementInput(movementInput.Value);
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
        movementInput.OnValueChanged -= OnMovementInputChanged;
    }

    private void Update()
    {
        if (!IsLocalOwnedPlayer()) return;

        if (!cameraAttached)
        {
            TryAttachCamera();
        }

        // Poll attack input first — works regardless of movement state.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryAttack();
        }

        // Reset isAttacking once the animator has left the Attack state.
        if (isAttacking)
        {
            AnimatorStateInfo stateInfo = bodyAnimator.GetCurrentAnimatorStateInfo(AttackLayerIndex);

            if (!stateInfo.IsName("Attack"))
            {
                isAttacking = false;
            }
        }

        float x = 0f;
        float z = 0f;
        bool isRunning = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.wKey.isPressed) z += 1f;
            if (Keyboard.current.sKey.isPressed) z -= 1f;
            isRunning = Keyboard.current.leftShiftKey.isPressed;
        }

        // Normalize first so diagonals don't move faster, then apply speed tier.
        // WalkSpeed = 0.5, RunSpeed = 1.0 — must match parent 1D blend tree thresholds.
        Vector2 rawInput = new Vector2(x, z);
        bool hasInput = rawInput.sqrMagnitude > 0f;
        float speedTier = isRunning ? RunSpeed : WalkSpeed;
        Vector2 scaledInput = hasInput ? rawInput.normalized * speedTier : Vector2.zero;

        // Camera-relative, Y-flattened world-space movement direction.
        Vector3 worldMoveDir = Vector3.zero;
        Camera cam = Camera.main;

        if (cam != null)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            worldMoveDir = camForward * scaledInput.y + camRight * scaledInput.x;
        }
        else if (hasInput)
        {
            worldMoveDir = new Vector3(scaledInput.x, 0f, scaledInput.y);
        }

        // Convert to character-local space for the 2D blend tree.
        // Passing the scaled vector (not unit-normalized) so Speed == its magnitude.
        Vector3 localMoveDir = transform.InverseTransformDirection(worldMoveDir);
        Vector2 animInput = new Vector2(localMoveDir.x, localMoveDir.z);

        // Drive local animator immediately — no NetworkVariable roundtrip.
        ApplyMovementInput(animInput);

        // Sync to remote peers only when the value has meaningfully changed.
        if (Vector2.Distance(movementInput.Value, animInput) > AnimSyncThreshold)
        {
            movementInput.Value = animInput;
        }

        if (!hasInput) return;

        // Smoothly rotate toward world movement direction.
        Quaternion targetRot = Quaternion.LookRotation(worldMoveDir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            turnSpeed * 100f * Time.deltaTime
        );

        // Move and lock Y to prevent sinking through the ground.
        Vector3 nextPosition = transform.position + worldMoveDir.normalized * (speedTier * moveSpeed) * Time.deltaTime;
        nextPosition.y = transform.position.y;
        transform.position = nextPosition;
    }

    /// <summary>Attempts an attack if cooldown has elapsed and no attack is in progress.</summary>
    private void TryAttack()
    {
        if (bodyAnimator == null) return;

        bool cooldownElapsed = Time.time - lastAttackTime >= AttackCooldown;

        if (isAttacking || !cooldownElapsed) return;

        isAttacking = true;
        lastAttackTime = Time.time;

        bodyAnimator.SetTrigger(AttackHash);
    }

    /// <summary>
    /// Called via Animation Event on the back fist clip at the frame the fist connects.
    /// Add an Animation Event in the Animation window pointing to this method.
    /// </summary>
    public void OnAttackHit()
    {
        if (!IsLocalOwnedPlayer()) return;

        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 direction = transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, attackRange, playerLayer))
        {
            Debug.Log($"[PlayerController] Hit: {hit.collider.name}");
            // TODO: call damage / knockback ServerRpc here.
        }

        Debug.DrawRay(origin, direction * attackRange, Color.red, 0.5f);
    }

    /// <summary>Sets MoveX, MoveZ, and Speed on the body animator.</summary>
    private void ApplyMovementInput(Vector2 input)
    {
        if (bodyAnimator == null) return;

        // Speed = magnitude: 0 (idle), 0.5 (walk), 1.0 (run).
        float speed = input.magnitude;

        bodyAnimator.SetFloat(MoveXHash, input.x);
        bodyAnimator.SetFloat(MoveZHash, input.y);
        bodyAnimator.SetFloat(SpeedHash, speed);
    }

    [ServerRpc]
    private void SubmitNameServerRpc(string newName)
    {
        playerName.Value = newName;
    }

    private void OnNameChanged(FixedString32Bytes _, FixedString32Bytes newName)
    {
        UpdateNameUI(newName.ToString());
    }

    private void OnMovementInputChanged(Vector2 _, Vector2 newInput)
    {
        // Apply only for remote players
        if (!IsLocalOwnedPlayer())
        {
            ApplyMovementInput(newInput);
        }
    }

    private void UpdateNameUI(string newName)
    {
        if (nameText != null)
        {
            nameText.text = newName;
        }
    }

    private void TryAttachCamera()
    {
        if (!IsLocalOwnedPlayer()) return;

        Camera cam = Camera.main;
        Scene playerScene = gameObject.scene;

        if (cam != null && cam.gameObject.scene != playerScene)
        {
            cam = null;
        }

        if (cam == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);

            foreach (Camera c in cameras)
            {
                if (c.gameObject.scene == playerScene)
                {
                    cam = c;
                    break;
                }
            }
        }

        if (cam == null) return;

        CameraFollow follow = cam.GetComponent<CameraFollow>();

        if (follow == null)
        {
            follow = cam.gameObject.AddComponent<CameraFollow>();
        }

        follow.SetTarget(transform);
        cameraAttached = true;
    }

    private bool IsLocalOwnedPlayer()
    {
        return NetworkManager.Singleton != null &&
               OwnerClientId == NetworkManager.Singleton.LocalClientId;
    }
}