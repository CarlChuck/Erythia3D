using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Core networked player class - handles position sync, interactions, and character data
/// Works with swappable controllers (Player Input or AI)
/// </summary>
public class NetworkedPlayer : NetworkBehaviour
{
    #region Components
    [Header("Core Components")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerStatBlock playerStatBlock;
    
    [Header("Controllers")]
    [SerializeField] private PlayerInputController playerInputController;
    [SerializeField] private AIController aiController;
    
    private ICharacterController activeController;
    #endregion
    
    #region Network Variables
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();
    private NetworkVariable<bool> isPlayerControlled = new NetworkVariable<bool>(false);
    #endregion
    
    #region Movement
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float gravity = -9.81f;
    
    private Vector3 velocity;
    private bool isGrounded;
    #endregion
    
    #region Events
    public System.Action<NetworkedPlayer> OnPlayerControlChanged;
    #endregion
    
    #region Network Lifecycle
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Initialize controllers
        if (playerInputController != null)
        {
            playerInputController.Initialize(this);
        }
        if (aiController != null)
        {
            aiController.Initialize(this);
        }
        
        // Set initial control state
        if (IsOwner)
        {
            SetPlayerControlled(true);
            Debug.Log($"NetworkedPlayer: Spawned for local player (Owner)");
        }
        else
        {
            SetPlayerControlled(false);
            Debug.Log($"NetworkedPlayer: Spawned for remote player");
        }
        
        // Subscribe to network variable changes
        isPlayerControlled.OnValueChanged += OnPlayerControlledChanged;
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        isPlayerControlled.OnValueChanged -= OnPlayerControlledChanged;
        base.OnNetworkDespawn();
    }
    #endregion
    
    #region Update Loop
    private void Update()
    {
        if (IsOwner)
        {
            HandleOwnerUpdate();
        }
        else
        {
            HandleRemoteUpdate();
        }
    }
    
    private void HandleOwnerUpdate()
    {
        // Get input from active controller
        if (activeController != null)
        {
            Vector2 moveInput = activeController.GetMovementInput();
            Vector2 lookInput = activeController.GetLookInput();
            bool jumpInput = activeController.GetJumpInput();
            bool interactInput = activeController.GetInteractionInput();
            
            // Handle movement
            HandleMovement(moveInput, jumpInput);
            
            // Handle rotation
            HandleRotation(lookInput);
            
            // Handle interaction
            if (interactInput)
            {
                HandleInteractionInput();
            }
            
            // Update network position
            UpdateNetworkTransform();
        }
    }
    
    private void HandleRemoteUpdate()
    {
        // Smoothly interpolate to network position for remote players
        if (Vector3.Distance(transform.position, networkPosition.Value) > 0.1f)
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition.Value, Time.deltaTime * 10f);
        }
        
        if (Quaternion.Angle(transform.rotation, networkRotation.Value) > 1f)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation.Value, Time.deltaTime * 10f);
        }
    }
    #endregion
    
    #region Movement Handling
    private void HandleMovement(Vector2 moveInput, bool jumpInput)
    {
        if (characterController == null) return;
        
        // Check if grounded
        isGrounded = characterController.isGrounded;
        
        // Reset velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        
        // Calculate movement direction
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        moveDirection = transform.TransformDirection(moveDirection);
        moveDirection *= moveSpeed;
        
        // Handle jumping
        if (jumpInput && isGrounded)
        {
            velocity.y = Mathf.Sqrt(-2f * gravity * 2f); // Jump height of 2 units
        }
        
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        
        // Combine horizontal movement with vertical velocity
        Vector3 finalMovement = moveDirection + Vector3.up * velocity.y;
        
        // Move the character
        characterController.Move(finalMovement * Time.deltaTime);
        
        // Update animator
        if (animator != null)
        {
            animator.SetFloat("Speed", moveInput.magnitude);
            animator.SetBool("IsGrounded", isGrounded);
        }
    }
    
    private void HandleRotation(Vector2 lookInput)
    {
        if (lookInput.magnitude > 0.1f)
        {
            // Calculate target rotation based on look input
            float targetAngle = Mathf.Atan2(lookInput.x, lookInput.y) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.AngleAxis(targetAngle, Vector3.up);
            
            // Smoothly rotate towards target
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    private void HandleInteractionInput()
    {
        if (activeController != null)
        {
            // Use camera from PlayerManager to perform raycast
            if (PlayerManager.Instance != null)
            {
                Camera mainCamera = PlayerManager.Instance.GetMainCamera();
                if (mainCamera != null)
                {
                    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit, 10f))
                    {
                        activeController.HandleInteraction(hit.collider.gameObject);
                    }
                }
            }
        }
    }
    #endregion
    
    #region Network Synchronization
    private void UpdateNetworkTransform()
    {
        if (!IsOwner) return;
        
        // Update network variables
        UpdatePositionServerRpc(transform.position, transform.rotation);
    }
    
    [ServerRpc]
    private void UpdatePositionServerRpc(Vector3 position, Quaternion rotation)
    {
        networkPosition.Value = position;
        networkRotation.Value = rotation;
    }
    #endregion
    
    #region Controller Management
    public void SetPlayerControlled(bool controlled)
    {
        if (IsOwner)
        {
            SetPlayerControlledServerRpc(controlled);
        }
    }
    
    [ServerRpc]
    private void SetPlayerControlledServerRpc(bool controlled)
    {
        isPlayerControlled.Value = controlled;
    }
    
    private void OnPlayerControlledChanged(bool previousValue, bool newValue)
    {
        if (IsOwner)
        {
            // Switch active controller based on control state
            if (newValue)
            {
                SetActiveController(playerInputController);
            }
            else
            {
                SetActiveController(aiController);
            }
        }
        
        OnPlayerControlChanged?.Invoke(this);
    }
    
    private void SetActiveController(ICharacterController newController)
    {
        // Deactivate current controller
        if (activeController != null)
        {
            activeController.OnControllerDeactivated();
            
            if (activeController is MonoBehaviour mb)
            {
                mb.enabled = false;
            }
        }
        
        // Activate new controller
        activeController = newController;
        
        if (activeController != null)
        {
            if (activeController is MonoBehaviour mb)
            {
                mb.enabled = true;
            }
            
            activeController.OnControllerActivated();
        }
    }
    
    public ICharacterController GetActiveController()
    {
        return activeController;
    }
    
    public bool IsPlayerControlled()
    {
        return isPlayerControlled.Value;
    }
    #endregion
    
    #region Interaction Handling
    [ServerRpc]
    public void InteractWithObjectServerRpc(ulong targetNetworkObjectId)
    {
        // Find the target object on the server
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetworkObject))
        {
            Interactable interactable = targetNetworkObject.GetComponent<Interactable>();
            if (interactable != null)
            {
                // For backwards compatibility, we need to pass a PlayerController
                // We can create a wrapper or adapter, but for now let's find an existing PlayerController
                // or create one on-the-fly for the interaction
                PlayerController playerControllerForInteraction = GetOrCreatePlayerControllerForInteraction();
                
                if (playerControllerForInteraction != null)
                {
                    interactable.OnInteract(playerControllerForInteraction);
                }
                else
                {
                    Debug.LogWarning($"NetworkedPlayer: Could not find or create PlayerController for interaction with {targetNetworkObject.name}");
                }
            }
        }
    }
    
    /// <summary>
    /// Get or create a PlayerController for legacy interaction support
    /// This maintains backwards compatibility with existing Interactable scripts
    /// </summary>
    private PlayerController GetOrCreatePlayerControllerForInteraction()
    {
        // First, try to find an existing PlayerController in the PlayerManager
        if (PlayerManager.Instance != null)
        {
            PlayerController existingController = PlayerManager.Instance.GetControlledCharacter();
            if (existingController != null)
            {
                return existingController;
            }
        }
        
        // If no existing PlayerController, we could create a temporary one
        // But for now, let's use a different approach - add a PlayerController component to this GameObject
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController == null)
        {
            // Add a PlayerController component for legacy support
            playerController = gameObject.AddComponent<PlayerController>();
            
            // Link it to this NetworkedPlayer
            playerController.SetNetworkedPlayer(this);
            
            Debug.Log("NetworkedPlayer: Created temporary PlayerController for legacy interaction support");
        }
        
        return playerController;
    }
    #endregion
    
    #region Public Interface
    public Vector3 GetPosition()
    {
        return transform.position;
    }
    
    public void SetPosition(Vector3 newPosition)
    {
        if (characterController != null && characterController.enabled)
        {
            Vector3 offset = newPosition - transform.position;
            characterController.Move(offset);
        }
        else
        {
            transform.position = newPosition;
        }
        
        // Update network position if we're the owner
        if (IsOwner)
        {
            UpdateNetworkTransform();
        }
    }
    
    public PlayerStatBlock GetPlayerStatBlock()
    {
        return playerStatBlock;
    }
    
    public CharacterController GetCharacterController()
    {
        return characterController;
    }
    
    public Animator GetAnimator()
    {
        return animator;
    }
    #endregion
} 