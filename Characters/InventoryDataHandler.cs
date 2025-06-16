using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;

public class InventoryDataHandler
{
    private readonly PlayerManager playerManager;
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
            inventory.ClearInventory();
            equipment.ClearEquipmentProfile();

            // Load Inventory Items and Equip them if applicable
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
                    if (!inventory.AddItem(itemInstance))
                    {
                        Debug.LogWarning($"InventoryDataHandler: Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to inventory bag for character {charId}.");
                    }
                }
            }

            // Load Inventory Resource Items
            foreach (var resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = GetResourceItemById(resourceItemData.ResourceItemID);
                if (resourceItemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: ResourceItem instance with ID {resourceItemData.ResourceItemID} not found. Cannot load.");
                    continue;
                }

                if (!inventory.AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add ResourceItem instance (ID: {resourceItemData.ResourceItemID}) to inventory bag for character {charId}.");
                }
            }

            // Load Inventory SubComponents
            foreach (var subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = ItemManager.Instance.GetSubComponentInstanceByID(subCompData.SubComponentID);
                if (subComponentInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: SubComponent instance with ID {subCompData.SubComponentID} not found via ItemManager. Cannot load.");
                    continue;
                }

                if (!inventory.AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to inventory bag for character {charId}.");
                }
            }
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
            foreach (var itemData in result.Items)
            {
                Item itemInstance = ItemManager.Instance.GetItemInstanceByID(itemData.ItemID);
                if (itemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: Item with ID {itemData.ItemID} not found via ItemManager for account inventory. Cannot load.");
                    continue;
                }

                if (!playerManager.HomeInventory.AddItem(itemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to home inventory for account {playerManager.AccountID}.");
                }
            }

            // Load Account Inventory Resource Items
            foreach (var resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = GetResourceItemById(resourceItemData.ResourceItemID);
                if (resourceItemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: ResourceItem instance with ID {resourceItemData.ResourceItemID} not found. Cannot load.");
                    continue;
                }

                if (!playerManager.HomeInventory.AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add ResourceItem instance (ID: {resourceItemData.ResourceItemID}) to home inventory for account {playerManager.AccountID}.");
                }
            }

            // Load Account Inventory SubComponents
            foreach (var subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = ItemManager.Instance.GetSubComponentInstanceByID(subCompData.SubComponentID);
                if (subComponentInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: SubComponent instance with ID {subCompData.SubComponentID} not found via ItemManager. Cannot load.");
                    continue;
                }

                if (!playerManager.HomeInventory.AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to home inventory for account {playerManager.AccountID}.");
                }
            }

            // Load Owned Workbenches from account inventory
            await ProcessOwnedWorkbenches(result.Workbenches);
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