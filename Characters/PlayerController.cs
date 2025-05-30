using System.Collections; // Required for Coroutines
using UnityEngine;

public class PlayerController : MonoBehaviour
{


    [SerializeField] private float interactionCooldownRate = 1.0f; // Cooldown time in seconds
    [SerializeField] private float interactionDistance = 5f; // Maximum distance to interact

    private Interactable currentInteractable; // Variable to store the interactable object
    private bool isOnGlobalCooldown = false; // Cooldown flag
    [SerializeField] private Camera mainCamera; // Reference to the main camera
    PlayerStatBlock playerCharacter; // Reference to the PlayerCharacter script

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
                float distanceToTarget = Vector3.Distance(transform.position, hit.transform.position);

                // Check if the interactable is within range
                if (distanceToTarget <= interactionDistance)
                {
                    // Store the interactable object
                    currentInteractable = interactable;
                    Debug.Log($"Interactable found within range: {interactable.name}");

                    // Call the interaction logic
                    currentInteractable.OnInteract(this);

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
        if (debugLog)
        {
            Debug.Log($"PlayerCharacter: SetCharacterPosition called - Current: {transform.position}, New: {newPosition}");
        }

        // Check for physics components that might interfere with positioning
        Rigidbody rb = GetComponent<Rigidbody>();
        CharacterController cc = GetComponent<CharacterController>();

        if (cc != null && cc.enabled)
        {
            // Use CharacterController.Move for proper positioning
            Vector3 offset = newPosition - transform.position;
            cc.Move(offset);

            if (debugLog)
            {
                Debug.Log($"PlayerCharacter: Used CharacterController.Move - Final position: {transform.position}");
            }
        }
        else if (rb != null && !rb.isKinematic)
        {
            // Use Rigidbody.MovePosition for physics-based positioning
            rb.MovePosition(newPosition);

            if (debugLog)
            {
                Debug.Log($"PlayerCharacter: Used Rigidbody.MovePosition - Final position: {transform.position}");
            }
        }
        else
        {
            // Direct transform positioning
            transform.position = newPosition;

            if (debugLog)
            {
                Debug.Log($"PlayerCharacter: Used direct transform.position - Final position: {transform.position}");
            }
        }

        // Verify the position was set correctly
        if (debugLog)
        {
            float distance = Vector3.Distance(transform.position, newPosition);
            if (distance > 0.1f)
            {
                Debug.LogWarning($"PlayerCharacter: Position mismatch! Expected: {newPosition}, Actual: {transform.position}, Distance: {distance}");
            }
            else
            {
                Debug.Log($"PlayerCharacter: Position set successfully within tolerance");
            }
        }
    }

    public Vector3 GetPosition()
    {
        return transform.position;
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
            Debug.LogError("PlayerCharacter not found.", this);
        }
        return playerCharacter;
    }
}
