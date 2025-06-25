using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class NetworkedPlayer : AreaNetworkBehaviour
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
    [SerializeField] private NetworkTransform networkTransform; // NetworkTransform for syncing position

    private ICharacterController activeController;
    #endregion
    
    #region Network Variables
    private NetworkVariable<int> networkRace = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> networkGender = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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
        Debug.Log($"NetworkedPlayer: OnNetworkSpawn - NetworkTransform IsOwner: {networkTransform.IsOwner}, IsServer: {networkTransform.IsServer}");        
        
        // Subscribe to visual changes
        networkRace.OnValueChanged += OnVisualsChanged;
        networkGender.OnValueChanged += OnVisualsChanged;

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
        if (PlayerManager.LocalInstance != null)
        {
            PlayerManager.LocalInstance.RegisterNetworkedPlayer(this);
        }

        // If we have already received the visual info, spawn the model now.
        if (IsClient && networkRace.Value != 0 && networkGender.Value != 0)
        {
            SpawnCharacterModel(networkRace.Value, networkGender.Value);
        }
        
        // Area assignment for NetworkedPlayer
        if (IsServer)
        {
            // Assign this NetworkedPlayer to area 1 (IthoriaSouth) by default
            // In the future, this could be based on character data or spawn location
            int defaultAreaId = 1; // IthoriaSouth
            SetArea(defaultAreaId);
            
            // Notify ServerManager that this client is in this area
            if (ServerManager.Instance != null)
            {
                ServerManager.Instance.AssignClientToArea(OwnerClientId, defaultAreaId);
                Debug.Log($"NetworkedPlayer: Assigned client {OwnerClientId} to area {defaultAreaId}");
            }
            else
            {
                Debug.LogWarning("NetworkedPlayer: ServerManager.Instance is null, cannot assign client to area");
            }
        }
    }
    private void OnVisualsChanged(int previousValue, int newValue)
    {
        // If both race and gender are set, spawn the model, but only on clients.
        if (IsClient && networkRace.Value != 0 && networkGender.Value != 0)
        {
            SpawnCharacterModel(networkRace.Value, networkGender.Value);
        }
    }
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
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from visual changes
        networkRace.OnValueChanged -= OnVisualsChanged;
        networkGender.OnValueChanged -= OnVisualsChanged;

        // Cleanup spawned character model
        if (spawnedCharacterModel != null)
        {
            Destroy(spawnedCharacterModel);
            spawnedCharacterModel = null;
            characterModelSpawned = false;
            Debug.Log("NetworkedPlayer: Cleaned up spawned character model");
        }
        
        // Unregister from PlayerManager
        if (PlayerManager.LocalInstance != null)
        {
            PlayerManager.LocalInstance.UnregisterNetworkedPlayer(this);
        }
        
        // Unsubscribe from network variable changes
        isPlayerControlled.OnValueChanged -= OnPlayerControlledChanged;
        
        // Area cleanup - ServerManager will handle client removal in its OnClientDisconnected callback
        // No additional cleanup needed here as AreaNetworkBehaviour base class handles the area-specific cleanup
        
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
            if (PlayerManager.LocalInstance != null)
            {
                Camera mainCamera = PlayerManager.LocalInstance.GetMainCamera();
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
            SetPlayerControlledRpc(controlled);
        }
    }
    [Rpc(SendTo.Server)] private void SetPlayerControlledRpc(bool controlled)
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
    [Rpc(SendTo.Server)] public void InteractWithObjectRpc(ulong targetNetworkObjectId)
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
    private PlayerController GetOrCreatePlayerControllerForInteraction()
    {
        // First, try to find an existing PlayerController in the PlayerManager
        if (PlayerManager.LocalInstance != null)
        {
            PlayerController existingController = PlayerManager.LocalInstance.GetControlledCharacter();
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
        return playerArmature.position;
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

    public ThirdPersonController GetThirdPersonController()
    {
        return thirdPersonController;
    }

    public PlayerInput GetPlayerInput()
    {
        return playerInput;
    }

    public Transform GetPlayerCameraRoot()
    {
        return playerCameraRoot;
    }

    public void SpawnCharacterModel(int race, int gender)
    {
        if (characterModelSpawned)
        {
            // If the model is already there, we might be changing it later.
            // For now, we'll just log and return.
            Debug.Log("NetworkedPlayer: A character model is already spawned.");
            return;
        }
        
        // Find the CharacterModelManager instance to get the model prefab.
        CharacterModelManager modelManager = FindFirstObjectByType<CharacterModelManager>();
        if (modelManager == null)
        {
            Debug.LogError("NetworkedPlayer: Cannot find CharacterModelManager in the scene!");
            return;
        }

        GameObject characterModelPrefab = modelManager.GetCharacterModel(race, gender);
        if (characterModelPrefab == null)
        {
            Debug.LogError($"NetworkedPlayer: Could not get character model prefab for race {race} gender {gender}.");
            return;
        }
        
        if (playerArmature == null)
        {
            Debug.LogError("NetworkedPlayer: Could not find PlayerArmature to spawn character model");
            return;
        }
        
        try
        {                
            Debug.Log($"NetworkedPlayer: Spawning character model '{characterModelPrefab.name}' at PlayerArmature '{playerArmature.name}'");
            spawnedCharacterModel = Instantiate(characterModelPrefab, playerArmature);
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

    public void SetCharacterVisuals(int race, int gender)
    {
        if (IsServer)
        {
            networkRace.Value = race;
            networkGender.Value = gender;
        }
        else
        {
            Debug.LogWarning("SetCharacterVisuals should only be called on the server.");
        }
    }
    #endregion    
    
    public GameObject GetSpawnedCharacterModel()
    {
        return spawnedCharacterModel;
    }
} 