using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Core networked player class - handles position sync, interactions, and character data
/// Works with swappable controllers (Player Input or AI)
/// </summary>
public class NetworkedPlayer : NetworkBehaviour
{
    #region Core Components
    [Header("Core Components")]
    [SerializeField] private CharacterController characterController; // May be on child
    [SerializeField] private Animator animator; // May be on child
    
    [Header("Controllers")]
    [SerializeField] private PlayerInputController playerInputController;
    [SerializeField] private AIController aiController;
    
    [Header("Child Components")]
    [SerializeField] private ThirdPersonController thirdPersonController; // Movement handler on child
    [SerializeField] private PlayerInput playerInput; // Input handler on child
    [SerializeField] private Transform playerCameraRoot; // Camera target transform
    [SerializeField] private Transform playerArmature; // Where character model is spawned

    private ICharacterController activeController;
    #endregion
    
    #region Network Variables
    private NetworkVariable<bool> isPlayerControlled = new NetworkVariable<bool>(false);
    #endregion
    
    #region Character Model
    [Header("Character Model")]
    [SerializeField] private GameObject spawnedCharacterModel; // The spawned character model instance
    private bool characterModelSpawned = false;
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
        
        Debug.Log($"NetworkedPlayer: OnNetworkSpawn - Transform position: {transform.position}");
        Debug.Log($"NetworkedPlayer: OnNetworkSpawn - IsOwner: {IsOwner}, IsServer: {IsServer}");
        
        // Auto-find child components if not assigned
        SetupChildComponents();
        
        // Verify NetworkTransform setup
        ValidateNetworkTransformSetup();
        
        // Configure child components for networking
        ConfigureChildComponentsForNetworking();
        
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
            Debug.Log($"NetworkedPlayer: Spawned for local player (Owner) at position: {transform.position}");
        }
        else
        {
            SetPlayerControlled(false);
            Debug.Log($"NetworkedPlayer: Spawned for remote player at position: {transform.position}");
        }
        
        // Subscribe to network variable changes
        isPlayerControlled.OnValueChanged += OnPlayerControlledChanged;
        
        Debug.Log($"NetworkedPlayer: NetworkVariable callbacks registered");
        
        // Register with PlayerManager
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.RegisterNetworkedPlayer(this);
        }
    }
    
    /// <summary>
    /// Auto-find child components if not manually assigned
    /// </summary>
    private void SetupChildComponents()
    {
        // Find CharacterController (may be on child)
        if (characterController == null)
        {
            characterController = GetComponentInChildren<CharacterController>();
        }
        
        // Find Animator (may be on child)
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        
        // Find ThirdPersonController (likely on child)
        if (thirdPersonController == null)
        {
            thirdPersonController = GetComponentInChildren<ThirdPersonController>();
        }
        
        // Find PlayerInput (likely on child)
        if (playerInput == null)
        {
            playerInput = GetComponentInChildren<PlayerInput>();
        }
        
        // Find PlayerCameraRoot (likely on child)
        if (playerCameraRoot == null)
        {
            // Search for PlayerCameraRoot by name
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                if (child.name == "PlayerCameraRoot")
                {
                    playerCameraRoot = child;
                    break;
                }
            }
            
            // Fallback: if no PlayerCameraRoot found, use the movement transform
            if (playerCameraRoot == null && thirdPersonController != null)
            {
                playerCameraRoot = thirdPersonController.transform;
                Debug.LogWarning($"NetworkedPlayer: PlayerCameraRoot not found, using movement transform '{thirdPersonController.name}' as fallback");
            }
        }
        
        Debug.Log($"NetworkedPlayer: Found components - CharacterController: {characterController?.name}, " +
                  $"ThirdPersonController: {thirdPersonController?.name}, PlayerInput: {playerInput?.name}, PlayerCameraRoot: {playerCameraRoot?.name}");
    }
    
    /// <summary>
    /// Configure child components to work properly with networking
    /// </summary>
    private void ConfigureChildComponentsForNetworking()
    {
        // Enable/disable ThirdPersonController based on ownership
        if (thirdPersonController != null)
        {
            // Only the owner should have active ThirdPersonController
            thirdPersonController.enabled = IsOwner;
            Debug.Log($"NetworkedPlayer: ThirdPersonController enabled = {IsOwner} (owner: {IsOwner})");
        }
        
        // Enable/disable PlayerInput based on ownership
        if (playerInput != null)
        {
            playerInput.enabled = IsOwner;
            Debug.Log($"NetworkedPlayer: PlayerInput enabled = {IsOwner} (owner: {IsOwner})");
        }
    }
    
    /// <summary>
    /// Validates that NetworkTransform is properly configured for the movement hierarchy
    /// </summary>
    private void ValidateNetworkTransformSetup()
    {
        // Check if NetworkTransform is on this object
        var networkTransformOnParent = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        
        // Check if there's a CharacterController on a child (where movement actually happens)
        var characterControllerChild = GetComponentInChildren<CharacterController>();
        
        if (networkTransformOnParent != null && characterControllerChild != null && characterControllerChild.transform != transform)
        {
            Debug.LogWarning($"NetworkedPlayer: NetworkTransform is on parent but CharacterController is on child '{characterControllerChild.name}'. " +
                           $"Consider moving NetworkTransform to the child object where movement actually occurs for proper network sync.");
            
            Debug.LogWarning($"NetworkedPlayer: Current setup will cause sync issues - parent stays at spawn while child moves around.");
        }
        
        // Find where movement components are located
        var thirdPersonController = GetComponentInChildren<ThirdPersonController>();
        if (thirdPersonController != null)
        {
            Debug.Log($"NetworkedPlayer: Movement components found on '{thirdPersonController.name}'");
            
            // Check if this child has NetworkTransform
            var networkTransformOnChild = thirdPersonController.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (networkTransformOnChild != null)
            {
                Debug.Log($"NetworkedPlayer: NetworkTransform found on movement object '{thirdPersonController.name}'");
                
                // Check authority setting - this is crucial for ThirdPersonController compatibility
                var isOwnerAuth = networkTransformOnChild.InLocalSpace; // This is one way to check, but not perfect
                Debug.LogWarning($"NetworkedPlayer: IMPORTANT - For ThirdPersonController compatibility, set NetworkTransform to 'Owner Authoritative' (Client Authoritative)");
                Debug.LogWarning($"NetworkedPlayer: ThirdPersonController directly modifies transform.position/rotation, which conflicts with Server Authoritative NetworkTransform");
                Debug.LogWarning($"NetworkedPlayer: Recommended: Change NetworkTransform Authority from 'Server Authoritative' to 'Owner Authoritative'");
            }
            else
            {
                Debug.LogWarning($"NetworkedPlayer: Movement object '{thirdPersonController.name}' lacks NetworkTransform. Movement won't be networked properly.");
            }
            
            // Additional diagnostic for authority conflicts
            if (networkTransformOnChild != null)
            {
                Debug.LogWarning($"NetworkedPlayer: DIAGNOSTIC - If movement works with NetworkTransform disabled but not enabled:");
                Debug.LogWarning($"NetworkedPlayer: → This confirms authority conflict between ThirdPersonController and NetworkTransform");
                Debug.LogWarning($"NetworkedPlayer: → Solution: Set NetworkTransform to 'Owner Authoritative' in Inspector");
            }
        }
    }
    
    public override void OnNetworkDespawn()
    {
        // Cleanup spawned character model
        if (spawnedCharacterModel != null)
        {
            Destroy(spawnedCharacterModel);
            spawnedCharacterModel = null;
            characterModelSpawned = false;
            Debug.Log("NetworkedPlayer: Cleaned up spawned character model");
        }
        
        // Unregister from PlayerManager
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.UnregisterNetworkedPlayer(this);
        }
        
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
        // Note: NetworkTransform automatically handles remote player position interpolation
        // ThirdPersonController handles actual movement for the owner
    }
    
    private void HandleOwnerUpdate()
    {
        // Let ThirdPersonController handle movement - we just handle controller switching and interactions
        if (activeController != null)
        {
            bool interactInput = activeController.GetInteractionInput();
            
            // Handle interaction
            if (interactInput)
            {
                HandleInteractionInput();
            }
        }
        
        // Note: ThirdPersonController on child handles all movement, rotation, jumping, etc.
        // NetworkTransform automatically synchronizes the position across network
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
                
                // Enable ThirdPersonController and PlayerInput for player control
                if (thirdPersonController != null)
                {
                    thirdPersonController.enabled = true;
                }
                if (playerInput != null)
                {
                    playerInput.enabled = true;
                }
            }
            else
            {
                SetActiveController(aiController);
                
                // Disable ThirdPersonController and PlayerInput for AI control
                if (thirdPersonController != null)
                {
                    thirdPersonController.enabled = false;
                }
                if (playerInput != null)
                {
                    playerInput.enabled = false;
                }
            }
            
            Debug.Log($"NetworkedPlayer: Controller switched to {(newValue ? "Player" : "AI")} control");
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
        
        // Note: NetworkTransform automatically handles position synchronization
        // No need to manually update network variables
    }
    
    /// <summary>
    /// Set initial spawn position - called by server after spawning
    /// </summary>
    public void SetInitialPosition(Vector3 spawnPosition)
    {
        Debug.Log($"NetworkedPlayer: SetInitialPosition called with position: {spawnPosition}");
        Debug.Log($"NetworkedPlayer: Current transform position before set: {transform.position}");
        
        // Set position on the root transform
        transform.position = spawnPosition;
        Debug.Log($"NetworkedPlayer: Transform position after direct set: {transform.position}");
        
        // If movement happens on a child object, we need to handle that too
        var movementObject = GetMovementTransform();
        if (movementObject != null && movementObject != transform)
        {
            Debug.Log($"NetworkedPlayer: Found movement object '{movementObject.name}', positioning it at spawn location");
            
            // Calculate offset from parent to movement object
            Vector3 currentOffset = movementObject.position - transform.position;
            
            // Position the movement object at the spawn location
            movementObject.position = spawnPosition;
            
            // Adjust parent to maintain hierarchy relationship
            transform.position = spawnPosition - currentOffset;
            
            Debug.Log($"NetworkedPlayer: Movement object positioned at: {movementObject.position}");
            Debug.Log($"NetworkedPlayer: Parent adjusted to: {transform.position}");
        }
        
        // NetworkTransform will automatically handle synchronization of this position
        // No need to manually set NetworkVariables
        
        Debug.Log($"NetworkedPlayer: SetInitialPosition complete - Final position: {transform.position}");
    }
    
    /// <summary>
    /// Gets the transform where actual movement happens (for camera targeting, etc.)
    /// </summary>
    public Transform GetMovementTransform()
    {
        // Use cached ThirdPersonController reference first
        if (thirdPersonController != null)
        {
            return thirdPersonController.transform;
        }
        
        // Look for CharacterController which indicates where movement happens
        if (characterController != null)
        {
            return characterController.transform;
        }
        
        // Fallback to this transform
        return transform;
    }    
    
    public CharacterController GetCharacterController()
    {
        return characterController;
    }
    
    public Animator GetAnimator()
    {
        return animator;
    }
    
    /// <summary>
    /// Gets the ThirdPersonController that handles movement
    /// </summary>
    public ThirdPersonController GetThirdPersonController()
    {
        return thirdPersonController;
    }
    
    /// <summary>
    /// Gets the PlayerInput component for input handling
    /// </summary>
    public PlayerInput GetPlayerInput()
    {
        return playerInput;
    }
    
    /// <summary>
    /// Gets the PlayerCameraRoot transform
    /// </summary>
    public Transform GetPlayerCameraRoot()
    {
        return playerCameraRoot;
    }
    
    /// <summary>
    /// Spawn the character model based on race and gender
    /// </summary>
    public void SpawnCharacterModel(GameObject characterModel)
    {
        if (characterModelSpawned)
        {
            Debug.LogWarning("NetworkedPlayer: Character model already spawned!");
            return;
        }
        
        if (playerArmature == null)
        {
            Debug.LogError("NetworkedPlayer: Could not find PlayerArmature to spawn character model");
            return;
        }
        
        try
        {                
            spawnedCharacterModel = Instantiate(characterModel, playerArmature);
            spawnedCharacterModel.transform.localPosition = Vector3.zero;
            spawnedCharacterModel.transform.localRotation = Quaternion.identity;
            
            Debug.Log($"NetworkedPlayer: Character model spawned: {spawnedCharacterModel.name}");
            
            // Copy the model's animator avatar to the PlayerArmature's animator
            CopyAnimatorAvatar();
            
            characterModelSpawned = true;
            
            Debug.Log($"NetworkedPlayer: Character model setup complete");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"NetworkedPlayer: Error spawning character model: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Copy the avatar from the spawned character model to the NetworkedPlayer's animator
    /// </summary>
    private void CopyAnimatorAvatar()
    {        
        // Find the animator on the spawned character model
        Animator modelAnimator = spawnedCharacterModel.GetComponent<Animator>();
        
        if (modelAnimator.avatar == null)
        {
            Debug.LogWarning($"NetworkedPlayer: Character model animator has no avatar to copy");
            return;
        }
        
        // Copy the avatar
        animator.avatar = modelAnimator.avatar;
        
        // Disable the model's animator to avoid conflicts
        modelAnimator.enabled = false;
        
        Debug.Log($"NetworkedPlayer: Copied avatar from '{spawnedCharacterModel.name}' to NetworkedPlayer animator");
        Debug.Log($"NetworkedPlayer: Model animator disabled to prevent conflicts");
    }
    #endregion    
    
    /// <summary>
    /// Get the spawned character model
    /// </summary>
    public GameObject GetSpawnedCharacterModel()
    {
        return spawnedCharacterModel;
    }
} 