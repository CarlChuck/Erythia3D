using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;
using System.Linq;

public class InventoryManager : BaseManager
{
    private const string InventoryItemsTableName = "InventoryItems";
    private const string InventoryResourceItemsTableName = "InventoryResourceItems";
    private const string InventorySubComponentsTableName = "InventorySubComponents";
    private const string AccountInventoryItemsTableName = "AccountInvItems";
    private const string AccountInventoryResourceItemsTableName = "AccountInvResourceItems";
    private const string AccountInventorySubComponentsTableName = "AccountInvSubComponents";
    private const string OwnedWorkbenchesTableName = "OwnedWorkbenches";

    #region Singleton
    public static InventoryManager Instance;

    protected void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate InventoryManager detected. Destroying self.");
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    #region Initialization
    protected override async Task InitializeAsync()
    {
        try
        {
            // 1. Ensure Tables Exist
            await EnsureInventoryTablesExistAsync();

            // 2. Mark as Initialized
            isInitialized = true;
            LogInfo("InventoryManager Initialization Complete.");
            NotifyDataLoaded();
        }
        catch (Exception ex)
        {
            LogError("InventoryManager Initialization Failed", ex);
            isInitialized = false;
        }
    }
    private async Task EnsureInventoryTablesExistAsync()
    {
        LogInfo("Checking and initializing inventory data tables async...");
        bool inventoryItemsTableOK = await EnsureTableExistsAsync(InventoryItemsTableName, GetInventoryItemsTableDefinition());
        bool invResourceItemsTableOK = await EnsureTableExistsAsync(InventoryResourceItemsTableName, GetInventoryResourceItemsTableDefinition());
        bool subComponentsTableOK = await EnsureTableExistsAsync(InventorySubComponentsTableName, GetInventorySubComponentsTableDefinition());
        bool accountInvItemsTableOK = await EnsureTableExistsAsync(AccountInventoryItemsTableName, GetAccountInvItemsTableDefinition());
        bool accountInvResourceItemsTableOK = await EnsureTableExistsAsync(AccountInventoryResourceItemsTableName, GetAccountInvResourceItemsTableDefinition());
        bool accountInvsubComponentsTableOK = await EnsureTableExistsAsync(AccountInventorySubComponentsTableName, GetAccountInvSubComponentsTableDefinition());
        bool ownedWorkbenchesTableOK = await EnsureTableExistsAsync(OwnedWorkbenchesTableName, GetOwnedWorkbenchesTableDefinition());

        if (!inventoryItemsTableOK || !invResourceItemsTableOK || !subComponentsTableOK || !ownedWorkbenchesTableOK)
        {
            throw new Exception("Failed to initialize required inventory database tables async.");
        }
        LogInfo("Inventory data tables checked/initialized async.");
    }
    private Dictionary<string, string> GetInventoryItemsTableDefinition()
    {
        return new Dictionary<string, string> 
        {
            {"InventoryItemID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"CharID", "INT"},
            {"ItemID", "INT"},
            {"SlotID", "INT"}
        };
    }
    private Dictionary<string, string> GetInventoryResourceItemsTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"InventoryResourceItemID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"CharID", "INT"},
            {"ResourceItemID", "INT"}
        };
    }
    private Dictionary<string, string> GetInventorySubComponentsTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"InventorySubComponentID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"CharID", "INT"},
            {"SubComponentID", "INT"}
        };
    }
    private Dictionary<string, string> GetAccountInvItemsTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"AccountInvItemID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"AccountID", "INT"},
            {"ItemID", "INT"}
        };
    }
    private Dictionary<string, string> GetAccountInvResourceItemsTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"AccountInvResourceItemID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"AccountID", "INT"},
            {"ResourceItemID", "INT"}
        };
    }
    private Dictionary<string, string> GetAccountInvSubComponentsTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"AccountInvSubComponentID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"AccountID", "INT"},
            {"SubComponentID", "INT"}
        };
    }
    private Dictionary<string, string> GetOwnedWorkbenchesTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"OwnedWorkBenchID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"AccountID", "INT"},
            {"WorkBenchType", "INT"}
        };
    }    
    #endregion



    #region Inventory Items Methods
    public async Task<List<Dictionary<string, object>>> GetCharacterInventoryItemsAsync(int charId)
    {
        if (charId <= 0)
        {
            LogError("Invalid CharID provided.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{InventoryItemsTableName}` WHERE CharID = @CharID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@CharID", charId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            LogInfo($"Retrieved {results.Count} inventory items for character {charId}");
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving inventory for character {charId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    public async Task<bool> AddItemToInventoryAsync(int charId, int itemId, int slotId)
    {
        if (charId <= 0 || itemId <= 0 || slotId < 0)
        {
            LogError("Invalid parameters provided for AddItemToInventory.");
            return false;
        }

        // --- Check if item already exists for this character --- 
        bool itemExists = await CheckIfInventoryItemExistsAsync(charId, itemId);
        if (itemExists)
        {
            LogWarning($"Item with ID {itemId} already exists in inventory for character {charId}. Cannot add duplicate.");
            return false;
        }
        // -------

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"CharID", charId},
            {"ItemID", itemId},
            {"SlotID", slotId}
        };

        try
        {
            bool success = await SaveDataAsync(InventoryItemsTableName, values);
            if (success)
            {
                LogInfo($"Added item {itemId} to slot {slotId} for character {charId}");
            }
            else
            {
                LogWarning($"Failed to add item {itemId} to slot {slotId} for character {charId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding item to inventory", ex);
            return false;
        }
    }
    public async Task<bool> RemoveItemFromInventoryAsync(int charId, int slotId)
    {
        if (charId <= 0 || slotId < 0)
        {
            LogError("Invalid parameters provided for RemoveItemFromInventory.");
            return false;
        }

        string whereCondition = "CharID = @CharID AND SlotID = @SlotID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@SlotID", slotId}
        };

        try
        {
            bool success = await DeleteDataAsync(InventoryItemsTableName, whereCondition, whereParams);
            if (success)
            {
                LogInfo($"Removed item from slot {slotId} for character {charId}");
            }
            else
            {
                LogWarning($"Failed to remove item from slot {slotId} for character {charId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing item from inventory", ex);
            return false;
        }
    }
    #endregion

    #region Inventory ResourceItems Methods
    public async Task<List<Dictionary<string, object>>> GetCharacterInventoryResourceItemsAsync(int charId)
    {
        if (charId <= 0)
        {
            LogError("Invalid CharID provided for GetCharacterInventoryResourceItemsAsync.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{InventoryResourceItemsTableName}` WHERE CharID = @CharID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@CharID", charId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            LogInfo($"Retrieved {results.Count} inventory resource items (slots) for character {charId}");
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving inventory resource items (slots) for character {charId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    public async Task<bool> AddInventoryResourceItemAsync(int charId, int resourceItemIdToAdd)
    {
        if (charId <= 0 || resourceItemIdToAdd <= 0)
        {
            LogError("Invalid parameters provided for AddInventoryResourceItemAsync.");
            return false;
        }

        ResourceItem itemToAdd = ItemManager.Instance.GetResourceItemById(resourceItemIdToAdd);
        if (itemToAdd == null)
        {
            LogError($"ResourceItem to add (ID: {resourceItemIdToAdd}) not found in cache. Cannot process.");
            return false;
        }

        // Get all ResourceItemIDs currently linked in InventoryResourceItems for this character
        string linkedItemsQuery = $"SELECT ResourceItemID FROM `{InventoryResourceItemsTableName}` WHERE CharID = @CharID";
        Dictionary<string, object> linkedItemsParams = new Dictionary<string, object> { { "@CharID", charId } };
        List<Dictionary<string, object>> linkedItemsResult = await QueryDataAsync(linkedItemsQuery, linkedItemsParams);

        List<int> currentlyLinkedIds = new List<int>();
        if (linkedItemsResult != null)
        {
            foreach (var row in linkedItemsResult)
            {
                if (row.TryGetValue("ResourceItemID", out object idObj) && idObj != DBNull.Value)
                {
                    currentlyLinkedIds.Add(Convert.ToInt32(idObj));
                }
            }
        }

        // Check for an existing *different* stack of the same resource type that is already linked
        foreach (int linkedStackId in currentlyLinkedIds)
        {
            if (linkedStackId == resourceItemIdToAdd) continue; // Don't compare with itself

            ResourceItem existingSlottedItem = ItemManager.Instance.GetResourceItemById(linkedStackId);
            if (existingSlottedItem != null && existingSlottedItem.Resource.ResourceSpawnID == itemToAdd.Resource.ResourceSpawnID) // Same resource type
            {
                LogInfo($"Found existing slotted stack (ID: {linkedStackId}) of same type (TypeID: {itemToAdd.Resource.ResourceSpawnID}) for CharID: {charId}. Merging quantities.");

                int newStackSize = itemToAdd.CurrentStackSize + existingSlottedItem.CurrentStackSize;

                // Update the existing slotted item's quantity in DB
                bool updateSuccess = await ItemManager.Instance.UpdateResourceItemTotalQuantityAsync(charId, existingSlottedItem.Resource.ResourceSpawnID, newStackSize);
                if (updateSuccess)
                {
                    // Update C# object for the existing slotted item
                    existingSlottedItem.SetStackSize(newStackSize);
                    LogInfo($"Updated quantity of existing stack ID {linkedStackId} to {newStackSize}.");

                    // Delete the itemToAdd stack from DB and cache as it's now merged
                    await ItemManager.Instance.DeleteResourceItemStackAsync(resourceItemIdToAdd); // Helper to delete from ResourceItems table and cache
                    LogInfo($"Original stack ID {resourceItemIdToAdd} merged and deleted.");
                    return true; // Merge successful
                }
                else
                {
                    LogError($"Failed to update quantity for existing stack ID {linkedStackId}. Merge aborted for stack {resourceItemIdToAdd}.");
                    return false; // DB update failed
                }
            }
        }

        // If no merge occurred, link the itemToAdd if not already linked
        bool alreadyLinked = await CheckIfInventoryResourceItemExistsAsync(charId, resourceItemIdToAdd);
        if (!alreadyLinked)
        {
            LogInfo($"No merge candidate found or itemToAdd is unique type for linked items. Linking stack ID {resourceItemIdToAdd} for CharID {charId}.");
            Dictionary<string, object> values = new Dictionary<string, object>
            {
                {"CharID", charId},
                {"ResourceItemID", resourceItemIdToAdd}
            };
            try
            {
                bool success = await SaveDataAsync(InventoryResourceItemsTableName, values);
                LogInfo(success ? $"Linked resource item {resourceItemIdToAdd} to inventory for character {charId}" : $"Failed to link resource item {resourceItemIdToAdd} for character {charId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Exception linking inventory resource item {resourceItemIdToAdd}", ex);
                return false;
            }
        }
        else
        {
            LogInfo($"ResourceItem stack ID {resourceItemIdToAdd} is already linked for CharID {charId}. No action taken.");
            return true; // Already linked, consider it a success
        }
    }
    public async Task<bool> RemoveInventoryResourceItemAsync(int charId, int resourceItemId)
    {
        if (charId <= 0 || resourceItemId <= 0)
        {
            LogError("Invalid parameters provided for RemoveInventoryResourceItemAsync.");
            return false;
        }

        string whereCondition = "CharID = @CharID AND ResourceItemID = @ResourceItemID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@ResourceItemID", resourceItemId}
        };

        try
        {
            bool success = await DeleteDataAsync(InventoryResourceItemsTableName, whereCondition, whereParams);
            LogInfo(success ? $"Removed inventory resource item {resourceItemId} for character {charId}" : $"Failed to remove inventory resource item {resourceItemId} for character {charId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing inventory resource item", ex);
            return false;
        }
    }
    #endregion

    #region Inventory SubComponents Methods
    public async Task<List<Dictionary<string, object>>> GetCharacterInventorySubComponentsAsync(int charId)
    {
        if (charId <= 0)
        {
            LogError("Invalid CharID provided for GetCharacterInventorySubComponentsAsync.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{InventorySubComponentsTableName}` WHERE CharID = @CharID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@CharID", charId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            LogInfo($"Retrieved {results.Count} inventory subcomponents (slots) for character {charId}");
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving inventory subcomponents (slots) for character {charId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    public async Task<bool> AddInventorySubComponentAsync(int charId, int subComponentId)
    {
        if (charId <= 0 || subComponentId <= 0)
        {
            LogError("Invalid parameters provided for AddInventorySubComponentAsync.");
            return false;
        }

        // --- Check if subcomponent instance already exists for this character ---
        bool subComponentExists = await CheckIfInventorySubComponentExistsAsync(charId, subComponentId);
        if (subComponentExists)
        {
            LogWarning($"SubComponent with ID {subComponentId} already exists in inventory for character {charId}. Cannot add duplicate.");
            return false;
        }
        // --------

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"CharID", charId},
            {"SubComponentID", subComponentId} // Link to the specific SubComponent instance ID
        };

        try
        {
            bool success = await SaveDataAsync(InventorySubComponentsTableName, values);
            LogInfo(success ? $"Added inventory subcomponent {subComponentId} for character {charId}" : $"Failed to add inventory subcomponent {subComponentId} for character {charId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding inventory subcomponent", ex);
            return false;
        }
    }
    public async Task<bool> RemoveInventorySubComponentAsync(int charId, int subComponentId)
    {
        if (charId <= 0 || subComponentId <= 0)
        {
            LogError("Invalid parameters provided for RemoveInventorySubComponentAsync.");
            return false;
        }

        string whereCondition = "CharID = @CharID AND SubComponentID = @SubComponentID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@SubComponentID", subComponentId}
        };

        try
        {
            bool success = await DeleteDataAsync(InventorySubComponentsTableName, whereCondition, whereParams);
            LogInfo(success ? $"Removed inventory subcomponent {subComponentId} for character {charId}" : $"Failed to remove inventory subcomponent {subComponentId} for character {charId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing inventory subcomponent", ex);
            return false;
        }
    }
    #endregion

    #region Owned Workbenches Methods
    public async Task<List<Dictionary<string, object>>> GetAccountOwnedWorkbenchesAsync(int accountId)
    {
        if (accountId <= 0)
        {
            LogError("Invalid AccountID provided for GetAccountOwnedWorkbenchesAsync.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{OwnedWorkbenchesTableName}` WHERE AccountID = @AccountID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@AccountID", accountId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            LogInfo($"Retrieved {results.Count} owned workbenches for account {accountId}");
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving owned workbenches for account {accountId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    public async Task<bool> AddOwnedWorkbenchAsync(int accountId, int workbenchType)
    {
        if (accountId <= 0 || workbenchType <= 0) // Assuming workbenchType should be positive
        {
            LogError("Invalid parameters provided for AddOwnedWorkbenchAsync.");
            return false;
        }

        bool typeExists = await CheckIfAccountHasWorkbenchTypeAsync(accountId, workbenchType);
        if (typeExists) 
        { 
            LogWarning($"Account {accountId} already has a workbench of type {workbenchType}. Cannot add duplicate.");
            return false; 
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"AccountID", accountId},
            {"WorkBenchType", workbenchType}
        };

        try
        {
            bool success = await SaveDataAsync(OwnedWorkbenchesTableName, values); // This will generate OwnedWorkBenchID
            LogInfo(success ? $"Added new workbench (type: {workbenchType}) for account {accountId}" : $"Failed to add new workbench for account {accountId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding owned workbench for account {accountId}, type {workbenchType}", ex);
            return false;
        }
    }
    public async Task<bool> RemoveOwnedWorkbenchAsync(int accountId, int workbenchType)
    {
        if (accountId <= 0 || workbenchType <= 0)
        {
            LogError("Invalid AccountID or WorkBenchType provided for RemoveOwnedWorkbenchAsync.");
            return false;
        }

        string whereCondition = "AccountID = @AccountID AND WorkBenchType = @WorkBenchType";
        Dictionary<string, object> whereParams = new Dictionary<string, object> 
        {
            { "@AccountID", accountId },
            { "@WorkBenchType", workbenchType }
        };

        try
        {
            bool success = await DeleteDataAsync(OwnedWorkbenchesTableName, whereCondition, whereParams);
            LogInfo(success ? $"Removed owned workbench type {workbenchType} for account {accountId}" : $"Failed to remove owned workbench type {workbenchType} for account {accountId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing owned workbench type {workbenchType} for account {accountId}", ex);
            return false;
        }
    }
    #endregion

    #region Account Inventory Items Methods
    public async Task<List<Dictionary<string, object>>> GetAccountInventoryItemsAsync(int accountId)
    {
        if (accountId <= 0)
        {
            LogError("Invalid AccountID provided for GetAccountInventoryItemsAsync.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{AccountInventoryItemsTableName}` WHERE AccountID = @AccountID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@AccountID", accountId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            LogInfo($"Retrieved {results.Count} account inventory items for account {accountId}");
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving account inventory items for account {accountId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    public async Task<bool> AddAccountInventoryItemAsync(int accountId, int itemId)
    {
        if (accountId <= 0 || itemId <= 0)
        {
            LogError("Invalid parameters provided for AddAccountInventoryItemAsync.");
            return false;
        }

        bool itemExists = await CheckIfAccountItemExistsAsync(accountId, itemId);
        if (itemExists)
        {
            LogWarning($"Item with ID {itemId} already exists in account inventory for account {accountId}. Cannot add duplicate.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"AccountID", accountId},
            {"ItemID", itemId}
        };

        try
        {
            bool success = await SaveDataAsync(AccountInventoryItemsTableName, values);
            if (success)
            {
                LogInfo($"Added item {itemId} to account inventory for account {accountId}");
            }
            else
            {
                LogWarning($"Failed to add item {itemId} to account inventory for account {accountId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding item to account inventory", ex);
            return false;
        }
    }
    public async Task<bool> RemoveAccountInventoryItemAsync(int accountId, int itemId)
    {
        if (accountId <= 0 || itemId <= 0)
        {
            LogError("Invalid parameters provided for RemoveAccountInventoryItemAsync.");
            return false;
        }

        string whereCondition = "AccountID = @AccountID AND ItemID = @ItemID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@AccountID", accountId},
            {"@ItemID", itemId}
        };

        try
        {
            bool success = await DeleteDataAsync(AccountInventoryItemsTableName, whereCondition, whereParams);
            if (success)
            {
                LogInfo($"Removed item {itemId} from account inventory for account {accountId}");
            }
            else
            {
                LogWarning($"Failed to remove item {itemId} from account inventory for account {accountId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing item from account inventory", ex);
            return false;
        }
    }
    #endregion

    #region Account Inventory ResourceItems Methods
    public async Task<List<Dictionary<string, object>>> GetAccountInventoryResourceItemsAsync(int accountId)
    {
        if (accountId <= 0)
        {
            LogError("Invalid AccountID provided for GetAccountInventoryResourceItemsAsync.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{AccountInventoryResourceItemsTableName}` WHERE AccountID = @AccountID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@AccountID", accountId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            LogInfo($"Retrieved {results.Count} account inventory resource items for account {accountId}");
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving account inventory resource items for account {accountId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    public async Task<bool> AddAccountInventoryResourceItemAsync(int accountId, int resourceItemId)
    {
        if (accountId <= 0 || resourceItemId <= 0)
        {
            LogError("Invalid parameters provided for AddAccountInventoryResourceItemAsync.");
            return false;
        }

        bool resourceItemExists = await CheckIfAccountInventoryResourceItemExistsAsync(accountId, resourceItemId);
        if (resourceItemExists)
        {
            LogWarning($"ResourceItem with ID {resourceItemId} already exists in account inventory for account {accountId}. Cannot add duplicate.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"AccountID", accountId},
            {"ResourceItemID", resourceItemId}
        };

        try
        {
            bool success = await SaveDataAsync(AccountInventoryResourceItemsTableName, values);
            if (success)
            {
                LogInfo($"Added resource item {resourceItemId} to account inventory for account {accountId}");
            }
            else
            {
                LogWarning($"Failed to add resource item {resourceItemId} to account inventory for account {accountId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding resource item to account inventory", ex);
            return false;
        }
    }
    public async Task<bool> RemoveAccountInventoryResourceItemAsync(int accountId, int resourceItemId)
    {
        if (accountId <= 0 || resourceItemId <= 0)
        {
            LogError("Invalid parameters provided for RemoveAccountInventoryResourceItemAsync.");
            return false;
        }

        string whereCondition = "AccountID = @AccountID AND ResourceItemID = @ResourceItemID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@AccountID", accountId},
            {"@ResourceItemID", resourceItemId}
        };

        try
        {
            bool success = await DeleteDataAsync(AccountInventoryResourceItemsTableName, whereCondition, whereParams);
            if (success)
            {
                LogInfo($"Removed resource item {resourceItemId} from account inventory for account {accountId}");
            }
            else
            {
                LogWarning($"Failed to remove resource item {resourceItemId} from account inventory for account {accountId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing resource item from account inventory", ex);
            return false;
        }
    }
    #endregion

    #region Account Inventory SubComponents Methods
    public async Task<List<Dictionary<string, object>>> GetAccountInventorySubComponentsAsync(int accountId)
    {
        if (accountId <= 0)
        {
            LogError("Invalid AccountID provided for GetAccountInventorySubComponentsAsync.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{AccountInventorySubComponentsTableName}` WHERE AccountID = @AccountID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@AccountID", accountId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            LogInfo($"Retrieved {results.Count} account inventory subcomponents for account {accountId}");
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving account inventory subcomponents for account {accountId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    public async Task<bool> AddAccountInventorySubComponentAsync(int accountId, int subComponentId)
    {
        if (accountId <= 0 || subComponentId <= 0)
        {
            LogError("Invalid parameters provided for AddAccountInventorySubComponentAsync.");
            return false;
        }

        bool subComponentExists = await CheckIfAccountInventorySubComponentExistsAsync(accountId, subComponentId);
        if (subComponentExists)
        {
            LogWarning($"SubComponent with ID {subComponentId} already exists in account inventory for account {accountId}. Cannot add duplicate.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"AccountID", accountId},
            {"SubComponentID", subComponentId}
        };

        try
        {
            bool success = await SaveDataAsync(AccountInventorySubComponentsTableName, values);
            if (success)
            {
                LogInfo($"Added subcomponent {subComponentId} to account inventory for account {accountId}");
            }
            else
            {
                LogWarning($"Failed to add subcomponent {subComponentId} to account inventory for account {accountId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding subcomponent to account inventory", ex);
            return false;
        }
    }
    public async Task<bool> RemoveAccountInventorySubComponentAsync(int accountId, int subComponentId)
    {
        if (accountId <= 0 || subComponentId <= 0)
        {
            LogError("Invalid parameters provided for RemoveAccountInventorySubComponentAsync.");
            return false;
        }

        string whereCondition = "AccountID = @AccountID AND SubComponentID = @SubComponentID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@AccountID", accountId},
            {"@SubComponentID", subComponentId}
        };

        try
        {
            bool success = await DeleteDataAsync(AccountInventorySubComponentsTableName, whereCondition, whereParams);
            if (success)
            {
                LogInfo($"Removed subcomponent {subComponentId} from account inventory for account {accountId}");
            }
            else
            {
                LogWarning($"Failed to remove subcomponent {subComponentId} from account inventory for account {accountId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing subcomponent from account inventory", ex);
            return false;
        }
    }
    #endregion

    #region Helpers
    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
    public void SetBagSpace(int newBagSpace)
    {
        // Implementation of SetBagSpace method
    }
    private async Task<bool> CheckIfInventoryItemExistsAsync(int charId, int itemId)
    {
        string query = $"SELECT COUNT(*) FROM `{InventoryItemsTableName}` WHERE CharID = @CharID AND ItemID = @ItemID";
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@ItemID", itemId}
        };

        try
        {
            object result = await DatabaseManager.Instance.ExecuteScalarAsync(query, parameters); 
            int count = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
        catch (Exception ex)
        {
            LogError($"Error checking if item exists (CharID: {charId}, ItemID: {itemId})", ex);
            return true; // Assume it exists on error to prevent potential duplicates
        }
    }
    private async Task<bool> CheckIfInventoryResourceItemExistsAsync(int charId, int resourceItemId)
    {
        string query = $"SELECT COUNT(*) FROM `{InventoryResourceItemsTableName}` WHERE CharID = @CharID AND ResourceItemID = @ResourceItemID";
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@ResourceItemID", resourceItemId}
        };

        try
        {
            object result = await DatabaseManager.Instance.ExecuteScalarAsync(query, parameters);
            int count = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
        catch (Exception ex)
        {
            LogError($"Error checking if inventory resource item exists (CharID: {charId}, ResourceItemID: {resourceItemId})", ex);
            return true; // Assume it exists on error to prevent potential duplicates
        }
    }
    private async Task<bool> CheckIfInventorySubComponentExistsAsync(int charId, int subComponentId)
    {
        string query = $"SELECT COUNT(*) FROM `{InventorySubComponentsTableName}` WHERE CharID = @CharID AND SubComponentID = @SubComponentID";
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@SubComponentID", subComponentId}
        };

        try
        {
            object result = await DatabaseManager.Instance.ExecuteScalarAsync(query, parameters);
            int count = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
        catch (Exception ex)
        {
            LogError($"Error checking if inventory subcomponent exists (CharID: {charId}, SubComponentID: {subComponentId})", ex);
            return true; // Assume it exists on error to prevent potential duplicates
        }
    }
    private async Task<bool> CheckIfAccountHasWorkbenchTypeAsync(int accountId, int workbenchType)
    {
        if (accountId <= 0 || workbenchType <= 0)
        {
            LogError("Invalid AccountID or WorkBenchType provided for CheckIfAccountHasWorkbenchTypeAsync.");
            return false; 
        }

        string query = $"SELECT COUNT(*) FROM `{OwnedWorkbenchesTableName}` WHERE AccountID = @AccountID AND WorkBenchType = @WorkBenchType";
        Dictionary<string, object> parameters = new Dictionary<string, object> 
        {
            { "@AccountID", accountId },
            { "@WorkBenchType", workbenchType }
        };

        try
        {
            object result = await DatabaseManager.Instance.ExecuteScalarAsync(query, parameters);
            int count = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
        catch (Exception ex)
        {
            LogError($"Error checking if account {accountId} has workbench type {workbenchType}", ex);
            return true; // Assume it exists on error to prevent potential issues, or false depending on strictness
        }
    }
    private async Task<bool> CheckIfAccountItemExistsAsync(int accountId, int itemId)
    {
        string query = $"SELECT COUNT(*) FROM `{AccountInventoryItemsTableName}` WHERE AccountID = @AccountID AND ItemID = @ItemID";
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            {"@AccountID", accountId},
            {"@ItemID", itemId}
        };

        try
        {
            object result = await DatabaseManager.Instance.ExecuteScalarAsync(query, parameters);
            int count = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
        catch (Exception ex)
        {
            LogError($"Error checking if account item exists (AccountID: {accountId}, ItemID: {itemId})", ex);
            return true; // Assume it exists on error to prevent potential duplicates
        }
    }
    private async Task<bool> CheckIfAccountInventoryResourceItemExistsAsync(int accountId, int resourceItemId)
    {
        string query = $"SELECT COUNT(*) FROM `{AccountInventoryResourceItemsTableName}` WHERE AccountID = @AccountID AND ResourceItemID = @ResourceItemID";
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            {"@AccountID", accountId},
            {"@ResourceItemID", resourceItemId}
        };

        try
        {
            object result = await DatabaseManager.Instance.ExecuteScalarAsync(query, parameters);
            int count = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
        catch (Exception ex)
        {
            LogError($"Error checking if account inventory resource item exists (AccountID: {accountId}, ResourceItemID: {resourceItemId})", ex);
            return true; // Assume it exists on error to prevent potential duplicates
        }
    }
    private async Task<bool> CheckIfAccountInventorySubComponentExistsAsync(int accountId, int subComponentId)
    {
        string query = $"SELECT COUNT(*) FROM `{AccountInventorySubComponentsTableName}` WHERE AccountID = @AccountID AND SubComponentID = @SubComponentID";
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            {"@AccountID", accountId},
            {"@SubComponentID", subComponentId}
        };

        try
        {
            object result = await DatabaseManager.Instance.ExecuteScalarAsync(query, parameters);
            int count = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
        catch (Exception ex)
        {
            LogError($"Error checking if account inventory subcomponent exists (AccountID: {accountId}, SubComponentID: {subComponentId})", ex);
            return true; // Assume it exists on error to prevent potential duplicates
        }
    }
    
    #endregion
}