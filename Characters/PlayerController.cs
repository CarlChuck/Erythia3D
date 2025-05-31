using System.Collections; // Required for Coroutines
using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float interactionCooldownRate = 1.0f; // Cooldown time in seconds
    [SerializeField] private float interactionDistance = 5f; // Maximum distance to interact

    private Interactable currentInteractable; // Variable to store the interactable object
    private bool isOnGlobalCooldown = false; // Cooldown flag
    
    [Header("Local Components (Owner Only)")]
    [SerializeField] private Camera mainCamera; // Reference to the main camera
    [SerializeField] private GameObject playerFollowCam; // Cinemachine follow camera
    [SerializeField] private GameObject localPlayerComponents; // Parent for local-only components
    
    [Header("Networked Components")]
    [SerializeField] private GameObject playerArmature; // The networked player model/controller
    
    PlayerStatBlock playerCharacter; // Reference to the PlayerCharacter script

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Only enable local components for the owner
        SetupLocalComponents();
        
        if (IsOwner)
        {
            Debug.Log($"PlayerController: Network spawned for local player (Owner)");
            
            // Enable local-only components
            if (localPlayerComponents != null)
            {
                localPlayerComponents.SetActive(true);
            }
            
            // Setup camera references
            SetupCameras();
        }
        else
        {
            Debug.Log($"PlayerController: Network spawned for remote player");
            
            // Disable local-only components for remote players
            if (localPlayerComponents != null)
            {
                localPlayerComponents.SetActive(false);
            }
        }
    }

    private void SetupLocalComponents()
    {
        // Find local components if not assigned
        if (localPlayerComponents == null)
        {
            localPlayerComponents = transform.Find("LocalPlayerComponents")?.gameObject;
        }
        
        if (mainCamera == null && localPlayerComponents != null)
        {
            mainCamera = localPlayerComponents.GetComponentInChildren<Camera>();
        }
        
        if (playerFollowCam == null && localPlayerComponents != null)
        {
            // Find Cinemachine camera in local components
            playerFollowCam = localPlayerComponents.transform.Find("PlayerFollowCamera")?.gameObject;
        }
        
        if (playerArmature == null)
        {
            playerArmature = transform.Find("PlayerArmature")?.gameObject;
        }
    }

    private void SetupCameras()
    {
        if (!IsOwner) return;
        
        // Setup main camera
        if (mainCamera != null)
        {
            mainCamera.enabled = true;
            Debug.Log("PlayerController: Main camera enabled for local player");
        }
        
        // Setup follow camera
        if (playerFollowCam != null)
        {
            playerFollowCam.SetActive(true);
            Debug.Log("PlayerController: Follow camera enabled for local player");
        }
    }

    public void InteractWithTarget(Vector2 mousePosition)
    {
        // 1. Check if already on cooldown
        if (isOnGlobalCooldown)
        {
            Debug.Log("Interaction on cooldown.");
            return;
        }

        if (mainCamera == null)
        {
            Debug.LogError("Main camera reference is missing.", this);
            return; // Cannot interact without a camera
        }

        // Create a ray from the camera going through the mouse position
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        // Perform the raycast
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) // Raycast infinitely for now, distance check later
        {
            // Check if the hit object has an Interactable component
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                // Calculate distance between player and the interactable object
                Vector3 playerPosition = networkedPlayer != null ? networkedPlayer.GetPosition() : transform.position;
                float distanceToTarget = Vector3.Distance(playerPosition, hit.transform.position);

                // Check if the interactable is within range
                if (distanceToTarget <= interactionDistance)
                {
                    // Store the interactable object
                    currentInteractable = interactable;
                    Debug.Log($"Interactable found within range: {interactable.name}");

                    // For new architecture, delegate to networked player
                    if (networkedPlayer != null)
                    {
                        // Get NetworkObject from the hit object
                        NetworkObject targetNetworkObject = hit.collider.gameObject.GetComponent<NetworkObject>();
                        if (targetNetworkObject != null)
                        {
                            networkedPlayer.InteractWithObjectServerRpc(targetNetworkObject.NetworkObjectId);
                        }
                        else
                        {
                            Debug.LogWarning($"PlayerController: Target {hit.collider.gameObject.name} does not have NetworkObject component");
                        }
                    }
                    else
                    {
                        // Fallback for single prefab approach
                        InteractWithObjectServerRpc(hit.collider.gameObject.GetComponent<NetworkObject>().NetworkObjectId);
                    }

                    // Start the cooldown
                    StartCoroutine(InteractionCooldownCoroutine());
                }
                else
                {
                    Debug.Log($"Interactable '{interactable.name}' found, but it's too far away ({distanceToTarget:F1}m > {interactionDistance}m).");
                    currentInteractable = null;
                }
            }
            else
            {
                Debug.Log("Object hit, but it's not interactable.");
                currentInteractable = null;
            }
        }
        else
        {
            Debug.Log("No object detected under the mouse cursor.");
            currentInteractable = null;
        }
    }

    [ServerRpc]
    private void InteractWithObjectServerRpc(ulong interactableNetworkId)
    {
        // Find the interactable object on the server
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(interactableNetworkId, out NetworkObject networkObject))
        {
            Interactable interactable = networkObject.GetComponent<Interactable>();
            if (interactable != null)
            {
                // Call the interaction logic on the server
                interactable.OnInteract(this);
            }
        }
    }

    public void OnMiningHit()
    {
        //Fire the mining hit event
        //TODO Animation for Mining
    }
    public void OnWoodCuttingHit()
    {
        //Fire the woodcutting hit event
        //TODO Animation for Woodcutting
    }
    public void OnHarvestHit()
    {
        //Fire the harvesting hit event
        //TODO Animation for Harvesting
    }
    public void OnDefaultHarvestHit()
    {
        //Fire the default harvest hit event
        //TODO Animation for Hitting with Rock
    }

    #region Position Management
    public void SetCharacterPosition(Vector3 newPosition, bool debugLog = false)
    {
        // Only the server or owner should set position directly
        if (!IsServer && !IsOwner)
        {
            return;
        }

        if (debugLog)
        {
            Debug.Log($"PlayerController: SetCharacterPosition called - Current: {transform.position}, New: {newPosition}");
        }

        // Use the playerArmature for physics positioning if available
        Transform targetTransform = playerArmature != null ? playerArmature.transform : transform;

        // Check for physics components that might interfere with positioning
        Rigidbody rb = targetTransform.GetComponent<Rigidbody>();
        CharacterController cc = targetTransform.GetComponent<CharacterController>();

        if (cc != null && cc.enabled)
        {
            // Use CharacterController.Move for proper positioning
            Vector3 offset = newPosition - targetTransform.position;
            cc.Move(offset);

            if (debugLog)
            {
                Debug.Log($"PlayerController: Used CharacterController.Move - Final position: {targetTransform.position}");
            }
        }
        else if (rb != null && !rb.isKinematic)
        {
            // Use Rigidbody.MovePosition for physics-based positioning
            rb.MovePosition(newPosition);

            if (debugLog)
            {
                Debug.Log($"PlayerController: Used Rigidbody.MovePosition - Final position: {targetTransform.position}");
            }
        }
        else
        {
            // Direct transform positioning
            targetTransform.position = newPosition;

            if (debugLog)
            {
                Debug.Log($"PlayerController: Used direct transform.position - Final position: {targetTransform.position}");
            }
        }

        // Verify the position was set correctly
        if (debugLog)
        {
            float distance = Vector3.Distance(targetTransform.position, newPosition);
            if (distance > 0.1f)
            {
                Debug.LogWarning($"PlayerController: Position mismatch! Expected: {newPosition}, Actual: {targetTransform.position}, Distance: {distance}");
            }
            else
            {
                Debug.Log($"PlayerController: Position set successfully within tolerance");
            }
        }
    }
    public Vector3 GetPosition()
    {
        // Return position from the armature if available, otherwise root transform
        return playerArmature != null ? playerArmature.transform.position : transform.position;
    }
    #endregion

    private IEnumerator InteractionCooldownCoroutine()
    {
        isOnGlobalCooldown = true;
        Debug.Log($"Interaction cooldown started ({interactionCooldownRate}s).");
        yield return new WaitForSeconds(interactionCooldownRate);
        isOnGlobalCooldown = false;
        Debug.Log("Interaction cooldown finished.");
    }

    public PlayerStatBlock GetPlayerCharacter()
    {
        if (playerCharacter == null)
        {
            // Try to find it in the armature first
            if (playerArmature != null)
            {
                playerCharacter = playerArmature.GetComponent<PlayerStatBlock>();
            }
            
            // Fallback to searching in this GameObject
            if (playerCharacter == null)
            {
                playerCharacter = GetComponent<PlayerStatBlock>();
            }
            
            if (playerCharacter == null)
            {
                Debug.LogError("PlayerCharacter not found.", this);
            }
        }
        return playerCharacter;
    }

    /// <summary>
    /// Enable/disable local player components (cameras, input, etc.)
    /// </summary>
    public void SetLocalComponentsActive(bool isActive)
    {
        if (!IsOwner) return;
        
        if (localPlayerComponents != null)
        {
            localPlayerComponents.SetActive(isActive);
        }
    }

    /// <summary>
    /// Legacy reference to networked player (for backwards compatibility)
    /// Note: This is now handled by NetworkedPlayer architecture
    /// </summary>
    private NetworkedPlayer networkedPlayer;
    
    public void SetNetworkedPlayer(NetworkedPlayer networkedController)
    {
        networkedPlayer = networkedController;
        Debug.Log("PlayerController: Linked to networked player (legacy)");
    }
    
    public NetworkedPlayer GetNetworkedPlayer()
    {
        return networkedPlayer;
    }
}
