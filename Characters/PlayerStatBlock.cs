using UnityEngine;
using UnityEngine.TextCore.Text;

public class PlayerStatBlock : StatBlock
{
    [SerializeField] private EquipmentProfile equipment;
    [SerializeField] private Inventory inventory;

    private int encumbrance;
    private int characterID;
    private string characterTitle;
    private int currentZone;

    private int combatXp;
    private int craftingXp;
    private int arcaneXp;
    private int spiritXp;
    private int veilXp;

    #region Setup and Initialization
    public void SetUpCharacter(string newCharacterName, int newCharacterID, string title, int zoneID, int incomingSpecies, int face, int gender, int combatxp, int craftingxp, int arcaneexp, int spiritxp, int veilexp, int speciesStrength = 10, int speciesDexterity = 10, int speciesConstitution = 10, int speciesIntelligence = 10, int speciesSpirit = 10)
    {
        // Create a temporary SpeciesTemplate with the provided stats
        SpeciesTemplate tempSpecies = ScriptableObject.CreateInstance<SpeciesTemplate>();
        SetSpeciesNum(incomingSpecies);
        SetGender(gender);
        tempSpecies.strength = speciesStrength;
        tempSpecies.dexterity = speciesDexterity;
        tempSpecies.constitution = speciesConstitution;
        tempSpecies.intelligence = speciesIntelligence;
        tempSpecies.spirit = speciesSpirit;
        
        SetSpecies(tempSpecies);
        //TODO add face
        SetGender(gender);
        SetCharacterName(newCharacterName);
        characterID = newCharacterID;
        characterTitle = title;
        currentZone = zoneID;
        combatXp = combatxp;
        craftingXp = craftingxp;
        arcaneXp = arcaneexp;
        spiritXp = spiritxp;
        veilXp = veilexp;
        SetupStatBlock();
        SetUpInventory();
        SetUpEquipment();
        SetEncumbrance();
    }
    private void SetUpInventory() 
    {
        if (inventory != null)
        {
            inventory.SetupInventory();
        }
        else
        {
            Debug.LogWarning($"PlayerStatBlock: Inventory component is null on {gameObject.name}, skipping inventory setup");
        }
    }
    private void SetUpEquipment() 
    { 
        if (equipment != null && inventory != null)
        {
            equipment.SetupEquipmentProfile(inventory);
        }
        else
        {
            Debug.LogWarning($"PlayerStatBlock: Equipment ({equipment != null}) or Inventory ({inventory != null}) component is null on {gameObject.name}, skipping equipment setup");
        }
    }
    private void SetEncumbrance()
    {
        encumbrance = 0; // Default to 0 encumbrance
        
        if (equipment != null)
        {
            encumbrance += equipment.GetTotalWeight();
        }
        
        if (inventory != null)
        {
            encumbrance += inventory.GetTotalWeight();
        }
        
        // If both are null (server case), encumbrance remains 0
        if (equipment == null && inventory == null)
        {
            Debug.LogWarning($"PlayerStatBlock: Both Equipment and Inventory are null on {gameObject.name}, encumbrance set to 0");
        }
    }
    #endregion

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
        return encumbrance;
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
    public int GetFace()
    {
        // TODO: Return proper face when face system is implemented
        return 0; // Placeholder
    }

    #endregion

    #region Data Transfer Methods (for Multiplayer)
    public void SetCharacterData(int charID, string charName, string title, int race, int gender, int face)
    {
        characterID = charID;
        SetCharacterName(charName);
        characterTitle = title;
        SetCharacterGender(gender);
        // TODO: Implement race and face setting when those systems are ready
    }
    
    public void SetCharacterID(int charID)
    {
        characterID = charID;
    }
    
    public void SetExperience(int combatExp, int craftingExp, int arcaneExp, int spiritExp, int veilExp)
    {
        combatXp = combatExp;
        craftingXp = craftingExp;
        arcaneXp = arcaneExp;
        spiritXp = spiritExp;
        veilXp = veilExp;
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
}
