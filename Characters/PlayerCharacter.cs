using UnityEngine;
using UnityEngine.TextCore.Text;

public class PlayerCharacter : StatBlock
{
    [SerializeField] private EquipmentProfile equipment;
    [SerializeField] private Inventory inventory;
    private GameObject characterModel;
    private int encumberence;
    private int characterID;
    private string characterTitle;
    private int currentZone; 
    
    private Interactable currentInteractable; // Variable to store the interactable object


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
    public void SetUpCharacter(string newCharacterName, int newCharacterID, string title, int zoneID, int race, int face, int gender, int combatxp, int craftingxp, int arcaneexp, int spiritxp, int veilexp)
    {
        SetSpecies(CharactersManager.Instance.GetSpeciesByID(race));
        SetGender(gender);
        SetCharacterName(newCharacterName);
        characterID = newCharacterID;
        characterTitle = title;
        currentZone = zoneID;
        SetupStatBlock();
        SetUpInventory();
        SetUpEquipment();
        SetEncumberence();
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

    #endregion

    public void InteractWithTarget()
    {
        // Define the ray starting point and direction
        Vector3 rayOrigin = transform.position + Vector3.up; // Slightly above the player's position
        Vector3 rayDirection = transform.forward;

        // Perform the raycast
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, 5f))
        {
            // Check if the hit object has an Interactable component
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                // Store the interactable object
                currentInteractable = interactable;
                Debug.Log($"Interactable found: {interactable.name}");
                currentInteractable.OnInteract(this); // Call the OnInteract method on the interactable object
            }
            else
            {
                Debug.Log("No interactable object found.");
                currentInteractable = null;
            }
        }
        else
        {
            Debug.Log("No object detected in front of the player.");
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
    public int GetCurrentZone()
    {
        return currentZone;
    }
    public int GetEncumberence()
    {
        return encumberence;
    }
    public string GetFamilyName()
    {
        return characterTitle;
    }
    public Inventory GetInventory()
    {
        return inventory;
    }
    #endregion

    #region Item Pickup
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

        if (!inventory.AddItem(item))
        {
            Debug.LogWarning($"Failed to add item {item.ItemName} to inventory");
        }
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
                // Update the existing stack
                existingItem.UpdateStackSize(stackToAdd: amountToTransfer);
                
                // Update the new item's stack size
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
    #endregion
}
