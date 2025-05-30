using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;

/// <summary>
/// Helper class for PlayerManager to handle inventory-related operations
/// Uses PlayerManager's RPC methods for server communication
/// </summary>
public class InventoryDataHandler
{
    private PlayerManager playerManager;

    public InventoryDataHandler(PlayerManager manager)
    {
        playerManager = manager;
    }

    #region Public Interface
    public async Task LoadAllInventoriesAsync()
    {
        await LoadAllCharactersInventoryAsync();
        await LoadAccountInventoryAsync();
        await LoadOwnedWorkbenchesAsync();
    }
    #endregion

    #region Character Inventory Management
    private async Task LoadAllCharactersInventoryAsync()
    {
        if (playerManager.PlayerCharacters == null || playerManager.PlayerCharacters.Count == 0)
        {
            Debug.LogWarning("InventoryDataHandler: No characters to load inventory for.");
            return;
        }

        foreach (var character in playerManager.PlayerCharacters)
        {
            await LoadCharacterInventoryAsync(character);
        }
    }

    private async Task LoadCharacterInventoryAsync(PlayerStatBlock character)
    {
        int charId = character.GetCharacterID();
        Inventory inventory = character.GetInventory();
        EquipmentProfile equipment = character.GetEquipmentProfile();

        if (charId <= 0)
        {
            Debug.LogError($"InventoryDataHandler: Invalid character ID: {charId} for character {character.GetCharacterName()}");
            return;
        }
        if (inventory == null)
        {
            Debug.LogError($"InventoryDataHandler: Inventory component not found for character {character.GetCharacterName()} (ID: {charId}).");
            return;
        }
        if (equipment == null)
        {
            Debug.LogError($"InventoryDataHandler: EquipmentProfile component not found for character {character.GetCharacterName()} (ID: {charId}).");
            return;
        }

        Debug.Log($"InventoryDataHandler: Requesting character inventory from server for character: {character.GetCharacterName()} (ID: {charId})");
        
        // Use NetworkRequestManager for cleaner request handling
        CharacterInventoryResult result = await playerManager.requestManager.SendCharacterInventoryRequestAsync(charId);
        
        if (result.Success)
        {
            await ProcessCharacterInventoryResult(result, character, inventory, equipment);
        }
        else
        {
            Debug.LogError($"InventoryDataHandler: Character inventory request failed for character {character.GetCharacterName()}: {result.ErrorMessage}");
        }
    }

    private async Task ProcessCharacterInventoryResult(CharacterInventoryResult result, PlayerStatBlock character, Inventory inventory, EquipmentProfile equipment)
    {
        int charId = character.GetCharacterID();
        
        try
        {
            Debug.Log($"InventoryDataHandler: Processing character inventory result for character: {character.GetCharacterName()} (ID: {charId})");
            
            inventory.ClearInventory();
            equipment.ClearEquipmentProfile();

            // Load Inventory Items (Equipment and Bag Items)
            Debug.Log($"InventoryDataHandler: Processing {result.Items.Length} inventory items for CharID: {charId}...");
            foreach (var itemData in result.Items)
            {
                Item itemInstance = ItemManager.Instance.GetItemInstanceByID(itemData.ItemID);
                if (itemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: Item with ID {itemData.ItemID} not found via ItemManager. Cannot load.");
                    continue;
                }

                if (itemData.SlotID > 0)
                {
                    // Equip Item
                    EquipItem(itemData, itemInstance, equipment, inventory);
                }
                else
                {
                    // Add to Inventory Bag
                    Debug.Log($"InventoryDataHandler: Adding item {itemInstance.ItemName} (ID: {itemData.ItemID}) to inventory bag.");
                    if (!inventory.AddItem(itemInstance))
                    {
                        Debug.LogWarning($"InventoryDataHandler: Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to inventory bag for character {charId}.");
                    }
                }
            }

            // Load Inventory Resource Items
            Debug.Log($"InventoryDataHandler: Processing {result.ResourceItems.Length} resource items for CharID: {charId}...");
            foreach (var resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = GetResourceItemById(resourceItemData.ResourceItemID);
                if (resourceItemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: ResourceItem instance with ID {resourceItemData.ResourceItemID} not found. Cannot load.");
                    continue;
                }

                Debug.Log($"InventoryDataHandler: Adding resource item {resourceItemInstance.Resource?.ResourceName ?? "Unknown"} (Instance ID: {resourceItemData.ResourceItemID}) to inventory bag.");
                if (!inventory.AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add ResourceItem instance (ID: {resourceItemData.ResourceItemID}) to inventory bag for character {charId}.");
                }
            }

            // Load Inventory SubComponents
            Debug.Log($"InventoryDataHandler: Processing {result.SubComponents.Length} subcomponents for CharID: {charId}...");
            foreach (var subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = ItemManager.Instance.GetSubComponentInstanceByID(subCompData.SubComponentID);
                if (subComponentInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: SubComponent instance with ID {subCompData.SubComponentID} not found via ItemManager. Cannot load.");
                    continue;
                }

                Debug.Log($"InventoryDataHandler: Adding subcomponent {subComponentInstance.Name ?? "Unknown"} (Instance ID: {subCompData.SubComponentID}) to inventory bag.");
                if (!inventory.AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to inventory bag for character {charId}.");
                }
            }

            Debug.Log($"InventoryDataHandler: Finished processing inventory for character: {character.GetCharacterName()} (ID: {charId})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"InventoryDataHandler: Error processing character inventory result for character {charId}: {ex.Message}\n{ex.StackTrace}");
        }
    }
    #endregion

    #region Account Inventory Management
    private async Task LoadAccountInventoryAsync()
    {
        Debug.Log("InventoryDataHandler: Requesting account inventory from server...");
        
        // Use NetworkRequestManager for cleaner request handling
        AccountInventoryResult result = await playerManager.requestManager.SendAccountInventoryRequestAsync(playerManager.AccountID);
        
        if (result.Success)
        {
            await ProcessAccountInventoryResult(result);
        }
        else
        {
            Debug.LogError($"InventoryDataHandler: Account inventory request failed: {result.ErrorMessage}");
        }
    }

    private async Task ProcessAccountInventoryResult(AccountInventoryResult result)
    {
        try
        {
            playerManager.HomeInventory.ClearInventory();

            // Load Inventory Items
            Debug.Log($"InventoryDataHandler: Processing {result.Items.Length} account inventory items from server.");
            foreach (var itemData in result.Items)
            {
                Item itemInstance = ItemManager.Instance.GetItemInstanceByID(itemData.ItemID);
                if (itemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: Item with ID {itemData.ItemID} not found via ItemManager for account inventory. Cannot load.");
                    continue;
                }

                Debug.Log($"InventoryDataHandler: Adding item {itemInstance.ItemName} (ID: {itemData.ItemID}) to home inventory.");
                if (!playerManager.HomeInventory.AddItem(itemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to home inventory for account {playerManager.AccountID}.");
                }
            }

            // Load Account Inventory Resource Items
            Debug.Log($"InventoryDataHandler: Processing {result.ResourceItems.Length} account resource items from server.");
            foreach (var resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = GetResourceItemById(resourceItemData.ResourceItemID);
                if (resourceItemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: ResourceItem instance with ID {resourceItemData.ResourceItemID} not found. Cannot load.");
                    continue;
                }

                Debug.Log($"InventoryDataHandler: Adding resource item {resourceItemInstance.Resource?.ResourceName ?? "Unknown"} (Instance ID: {resourceItemData.ResourceItemID}) to home inventory.");
                if (!playerManager.HomeInventory.AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add ResourceItem instance (ID: {resourceItemData.ResourceItemID}) to home inventory for account {playerManager.AccountID}.");
                }
            }

            // Load Account Inventory SubComponents
            Debug.Log($"InventoryDataHandler: Processing {result.SubComponents.Length} account subcomponents from server.");
            foreach (var subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = ItemManager.Instance.GetSubComponentInstanceByID(subCompData.SubComponentID);
                if (subComponentInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: SubComponent instance with ID {subCompData.SubComponentID} not found via ItemManager. Cannot load.");
                    continue;
                }

                Debug.Log($"InventoryDataHandler: Adding subcomponent {subComponentInstance.Name ?? "Unknown"} (Instance ID: {subCompData.SubComponentID}) to home inventory.");
                if (!playerManager.HomeInventory.AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to home inventory for account {playerManager.AccountID}.");
                }
            }

            // Load Owned Workbenches from account inventory
            await ProcessOwnedWorkbenches(result.Workbenches);

            Debug.Log($"InventoryDataHandler: Finished loading account inventory for AccountID: {playerManager.AccountID}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"InventoryDataHandler: Error processing account inventory result: {ex.Message}\n{ex.StackTrace}");
        }
    }
    #endregion

    #region Workbench Management
    private async Task LoadOwnedWorkbenchesAsync()
    {
        Debug.Log("InventoryDataHandler: Requesting workbench list from server...");

        // Use NetworkRequestManager for cleaner request handling
        WorkbenchListResult result = await playerManager.requestManager.SendWorkbenchListRequestAsync(playerManager.AccountID);

        if (result.Success)
        {
            await ProcessOwnedWorkbenches(result.Workbenches);
        }
        else
        {
            Debug.LogError($"InventoryDataHandler: Workbench list request failed: {result.ErrorMessage}");
        }
    }

    private async Task ProcessOwnedWorkbenches(WorkbenchData[] workbenches)
    {
        Debug.Log($"InventoryDataHandler: Processing {workbenches.Length} owned workbenches from server.");
        playerManager.OwnedWorkbenches.Clear();

        foreach (var workbenchData in workbenches)
        {
            int workbenchType = workbenchData.WorkBenchType;

            WorkBench newWorkBenchInstance = GameObject.Instantiate(playerManager.WorkBenchPrefab, playerManager.WorkbenchParent);
            newWorkBenchInstance.SetWorkbenchType(workbenchType);

            if (WorkBenchManager.Instance != null)
            {
                WorkBench templateWorkBench = WorkBenchManager.Instance.GetWorkbenchByType(workbenchType);
                if (templateWorkBench != null)
                {
                    newWorkBenchInstance.InitializeRecipes(templateWorkBench.Recipes);
                    Debug.Log($"InventoryDataHandler: Initialized workbench type {workbenchType} with {templateWorkBench.Recipes.Count} recipes from WorkBenchManager.");
                }
                else
                {
                    Debug.LogWarning($"InventoryDataHandler: No template workbench found in WorkBenchManager for type {workbenchType}. Initializing with empty recipes.");
                    newWorkBenchInstance.InitializeRecipes(new List<Recipe>());
                }
            }
            else
            {
                newWorkBenchInstance.InitializeRecipes(new List<Recipe>());
            }

            playerManager.OwnedWorkbenches.Add(newWorkBenchInstance);
        }
        Debug.Log($"InventoryDataHandler: Finished processing {playerManager.OwnedWorkbenches.Count} owned workbenches.");
    }
    #endregion

    #region Helper Methods
    private void EquipItem(InventoryItemData itemData, Item itemInstance, EquipmentProfile equipment, Inventory inventory)
    {
        ItemType slotType = MapSlotIdToItemType(itemData.SlotID);
        if (slotType != ItemType.Other)
        {
            int slotIndex = GetSlotIndexForType(itemData.SlotID);
            EquipmentSlot targetSlot = equipment.GetSlotForItemType(slotType, slotIndex);
            if (targetSlot != null)
            {
                Debug.Log($"InventoryDataHandler: Equipping item {itemInstance.ItemName} (ID: {itemData.ItemID}) to SlotID: {itemData.SlotID} (Type: {slotType}, Index: {slotIndex})");
                equipment.EquipItemToSlot(itemInstance, targetSlot);
            }
            else
            {
                Debug.LogWarning($"InventoryDataHandler: Could not find EquipmentSlot for SlotID: {itemData.SlotID} (Type: {slotType}, Index: {slotIndex}). Cannot equip {itemInstance.ItemName}.");
                inventory.AddItem(itemInstance); // Put in bag as fallback
            }
        }
        else
        {
            Debug.LogWarning($"InventoryDataHandler: Invalid SlotID {itemData.SlotID} found for ItemID {itemData.ItemID}. Cannot equip.");
            inventory.AddItem(itemInstance); // Put in bag as fallback
        }
    }

    private ItemType MapSlotIdToItemType(int slotId)
    {
        switch (slotId)
        {
            case 1: return ItemType.Helm;
            case 2: return ItemType.Cuirass;
            case 3: return ItemType.Greaves;
            case 4: return ItemType.Vambraces;
            case 5: return ItemType.Finger; // First finger slot
            case 6: return ItemType.Finger; // Second finger slot
            case 7: return ItemType.PrimaryHand;
            case 8: return ItemType.SecondaryHand;
            case 9: return ItemType.MiningTool;
            case 10: return ItemType.WoodTool;
            case 11: return ItemType.HarvestingTool;
            case 12: return ItemType.Hauberk;
            case 13: return ItemType.Trousers;
            case 14: return ItemType.Sleeves;
            case 15: return ItemType.Coif;
            case 16: return ItemType.Neck;
            case 17: return ItemType.Waist;
            case 18: return ItemType.Back;
            case 19: return ItemType.Boots;
            case 20: return ItemType.Ear; // First ear slot
            case 21: return ItemType.Ear; // Second ear slot
            default: return ItemType.Other;
        }
    }

    private int GetSlotIndexForType(int slotId)
    {
        switch (slotId)
        {
            case 5: return 0; // First finger slot
            case 6: return 1; // Second finger slot
            case 20: return 0; // First ear slot
            case 21: return 1; // Second ear slot
            default: return 0; // All other slots use index 0
        }
    }

    private ResourceItem GetResourceItemById(int resourceItemId)
    {
        // TODO: Implement proper ResourceItem lookup
        ResourceItem resourceItem = null;
        return resourceItem;
    }
    #endregion
} 