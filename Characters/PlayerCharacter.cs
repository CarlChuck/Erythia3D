using UnityEngine;
using UnityEngine.TextCore.Text;
using System.Collections; // Required for Coroutines

public class PlayerCharacter : StatBlock
{
    [SerializeField] private EquipmentProfile equipment;
    [SerializeField] private Inventory inventory;
    [SerializeField] private float interactionCooldownRate = 1.0f; // Cooldown time in seconds
    [SerializeField] private float interactionDistance = 5f; // Maximum distance to interact

    private GameObject characterModel;
    private int encumberence;
    private int characterID;
    private string characterTitle;
    private int currentZone;

    private int combatXp;
    private int craftingXp;
    private int arcaneXp;
    private int spiritXp;
    private int veilXp;

    // Used for setting up the character's spawn location
    private float yLoc;
    private float xLoc;
    private float zLoc;

    private Interactable currentInteractable; // Variable to store the interactable object
    private bool isOnGlobalCooldown = false; // Cooldown flag

    [SerializeField] private Camera mainCamera; // Cache the main camera

    #region Setup and Initialization
    public void AddModel(GameObject playerModel)
    {
        characterModel = playerModel;
        characterModel.transform.SetParent(transform);
        characterModel.SetActive(false);
    }
    public void ActivateModel(bool isActive)
    {
        if (characterModel != null)
        {
            characterModel.SetActive(isActive);
        }
    }
    public void SetUpCharacter(string newCharacterName, int newCharacterID, string title, int zoneID, int race, int face, int gender, int combatxp, int craftingxp, int arcaneexp, int spiritxp, int veilexp, Camera newCamera, int newXLoc = 0, int newYLoc = 0, int newZLoc = 0)
    {
        SetSpecies(CharactersManager.Instance.GetSpeciesByID(race));
        //TODO add face
        SetGender(gender);
        SetCharacterName(newCharacterName);
        characterID = newCharacterID;
        characterTitle = title;
        currentZone = zoneID;
        if (newYLoc != 0)
        {
            yLoc = (newYLoc / 100);
        }
        if (newXLoc != 0)
        {
            xLoc = (newXLoc / 100);
        }
        if (newZLoc != 0)
        {
            zLoc = (newZLoc / 100);
        }
        combatXp = combatxp;
        craftingXp = craftingxp;
        arcaneXp = arcaneexp;
        spiritXp = spiritxp;
        veilXp = veilexp;
        SetupStatBlock();
        SetUpInventory();
        SetUpEquipment();
        SetEncumberence();
        SetCamera(newCamera);
    }
    private void SetUpInventory() 
    {
        inventory.SetupInventory();
    }
    private void SetUpEquipment() 
    { 
        equipment.SetupEquipmentProfile(inventory);
    }
    private void SetEncumberence()
    {
        encumberence = equipment.GetTotalWeight();
        encumberence += inventory.GetTotalWeight();
    }
    private void SetCamera(Camera cameraToSet) 
    {
        mainCamera = cameraToSet;
    }
    public void SetupCharacterWithNewLoc(int newXLoc = 0, int newYLoc = 0, int newZLoc = 0)
    {
        if (newYLoc != 0)
        {
            yLoc = (newYLoc / 100);
        }
        if (newXLoc != 0)
        {
            xLoc = (newXLoc / 100);
        }
        if (newZLoc != 0)
        {
            zLoc = (newZLoc / 100);
        }
        transform.position = new Vector3(xLoc, yLoc, zLoc);
    }
    #endregion

    public void InteractWithTarget()
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
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

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
    public void OnDefaultHarvestHit(){
        //Fire the default harvest hit event
        //TODO Animation for Hitting with Rock
    }


    #region Getters
    public EquipmentProfile GetEquipmentProfile()
    {
        return equipment;
    }
    public int GetCharacterID()    
    {
        return characterID;
    }
    public Vector3 GetPosition()
    {
        return transform.position;
    }
    public int GetCurrentZone()
    {
        return currentZone;
    }
    public int GetEncumberence()
    {
        return encumberence;
    }
    public string GetTitle()
    {
        return characterTitle;
    }
    public Inventory GetInventory()
    {
        return inventory;
    }
    public int GetCombatExp()
    {
        return combatXp;
    }
    public int GetCraftingExp()
    {
        return craftingXp;
    }
    public int GetArcaneExp()
    {
        return arcaneXp;
    }
    public int GetSpiritExp()
    {
        return spiritXp;
    }
    public int GetVeilExp()
    {
        return veilXp;
    }

    #endregion

    #region Items
    public void OnPickup(object obj)
    {
        if (obj is Item item)
        {
            OnPickupItem(item);
        }
        else if (obj is ResourceItem resourceItem)
        {
            OnPickupResourceItem(resourceItem);
        }
        else if (obj is SubComponent subComponent)
        {
            OnPickupSubComponent(subComponent);
        }
        else
        {
            Debug.LogWarning("Attempted to pick up an unsupported object type.");
        }
    }
    public void OnPickupItem(Item item)
    {
        if (item == null)
        {
            Debug.LogWarning("Attempted to pick up null item");
            return;
        }
        if (inventory == null)
        {
            Debug.LogError("No inventory component found on character");
            return;
        }
        inventory.AddItem(item);        
    }
    public void OnPickupSubComponent(SubComponent subComponent)
    {
        if (subComponent == null)
        {
            Debug.LogWarning("Attempted to pick up null subcomponent");
            return;
        }
        if (inventory == null)
        {
            Debug.LogError("No inventory component found on character");
            return;
        }
        inventory.AddSubComponent(subComponent);
    }
    public void OnPickupResourceItem(ResourceItem resourceItem)
    {
        if (resourceItem == null)
        {
            Debug.LogWarning("Attempted to pick up null resource item");
            return;
        }

        Debug.Log($"Attempting to pick up resource item: {resourceItem.Resource?.ResourceName ?? "Unknown Resource"} with stack size {resourceItem.CurrentStackSize}");

        if (inventory == null)
        {
            Debug.LogError("No inventory component found on character");
            return;
        }

        // Check if we already have this resource type
        ResourceItem existingItem = inventory.GetResourceItemByResource(resourceItem.Resource);
        if (existingItem != null)
        {
            Debug.Log($"Found existing stack of {resourceItem.Resource.ResourceName} with {existingItem.CurrentStackSize}/{existingItem.StackSizeMax} items");
            
            // Calculate how much we can add to the existing stack
            int spaceAvailable = existingItem.StackSizeMax - existingItem.CurrentStackSize;
            int amountToTransfer = Mathf.Min(spaceAvailable, resourceItem.CurrentStackSize);

            if (amountToTransfer > 0)
            {
                Debug.Log($"Transferring {amountToTransfer} items to existing stack");
                // Update the existing stack VIA the Inventory manager to trigger UI update
                inventory.UpdateResourceQuantity(existingItem, amountToAdd: amountToTransfer);
                
                // Update the picked-up item's stack size (this one doesn't need UI event)
                resourceItem.UpdateStackSize(stackToRemove: amountToTransfer);

                // If the new item's stack is now empty, destroy it
                if (resourceItem.CurrentStackSize <= 0)
                {
                    Debug.Log("Resource item stack depleted, destroying object");
                    Destroy(resourceItem.gameObject);
                    return;
                }
            }
            else
            {
                Debug.Log("No space available in existing stack");
            }
        }

        // If we still have items to add (either no existing stack or existing stack is full)
        if (resourceItem.CurrentStackSize > 0)
        {
            Debug.Log($"Adding new resource item to inventory: {resourceItem.Resource.ResourceName} with {resourceItem.CurrentStackSize} items");
            if (!inventory.AddResourceItem(resourceItem))
            {
                Debug.LogWarning($"Failed to add resource item {resourceItem.Resource.ResourceName} to inventory");
            }
            else
            {
                Debug.Log($"Successfully added resource item to inventory");
            }
        }
    }
    public void OnRemoveItem(Item item)
    {
        if (item == null)
        {
            Debug.LogWarning("Attempted to remove null item");
            return;
        }
        if (inventory == null)
        {
            Debug.LogError("No inventory component found on character");
            return;
        }
        inventory.RemoveItem(item);
    }
    public void OnRemoveSubComponent(SubComponent subComponent)
    {
        if (subComponent == null)
        {
            Debug.LogWarning("Attempted to remove null subcomponent");
            return;
        }
        if (inventory == null)
        {
            Debug.LogError("No inventory component found on character");
            return;
        }
        inventory.RemoveSubComponent(subComponent);
    }
    public void OnRemoveResourceItem(ResourceItem resourceItem)
    {
        if (resourceItem == null)
        {
            Debug.LogWarning("Attempted to remove null resource item");
            return;
        }
        if (inventory == null)
        {
            Debug.LogError("No inventory component found on character");
            return;
        }
        inventory.RemoveResourceItem(resourceItem);
    }
    public void OnTransferItem(Item item, Inventory targetInventory)
    {
        if (item == null)
        {
            Debug.LogWarning("Attempted to transfer null item");
            return;
        }
        if (targetInventory == null)
        {
            Debug.LogError("No inventory component to transfer to");
            return;
        }
        inventory.RemoveItem(item);
        targetInventory.AddItem(item);
    }
    public void OnTransferSubComponent(SubComponent subComponent, Inventory targetInventory)
    {
        if (subComponent == null)
        {
            Debug.LogWarning("Attempted to transfer null subcomponent");
            return;
        }
        if (targetInventory == null)
        {
            Debug.LogError("No inventory component to transfer to");
            return;
        }
        inventory.RemoveSubComponent(subComponent);
        targetInventory.AddSubComponent(subComponent);
    }
    public void OnTransferResourceItem(ResourceItem resourceItem, Inventory targetInventory) 
    {
        if (resourceItem == null)
        {
            Debug.LogWarning("Attempted to transfer null resource item");
            return;
        }
        if (targetInventory == null)
        {
            Debug.LogError("No inventory component to transfer to");
            return;
        }
        inventory.RemoveResourceItem(resourceItem);
        targetInventory.AddResourceItem(resourceItem);
    }
    #endregion

    #region Helpers
    private IEnumerator InteractionCooldownCoroutine()
    {
        isOnGlobalCooldown = true; // Set cooldown active
        Debug.Log($"Interaction cooldown started ({interactionCooldownRate}s).");
        yield return new WaitForSeconds(interactionCooldownRate); // Wait for the specified duration
        isOnGlobalCooldown = false; // Reset cooldown flag
        Debug.Log("Interaction cooldown finished.");
    }
    #endregion
}
