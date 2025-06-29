using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;
using Unity.Netcode;

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
            await EnsureInventoryTablesExistAsync();

            isInitialized = true;
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
        bool inventoryItemsTableOK = await EnsureTableExistsAsync(InventoryItemsTableName, GetInventoryItemsTableDefinition());
        bool invResourceItemsTableOK = await EnsureTableExistsAsync(InventoryResourceItemsTableName, GetInventoryResourceItemsTableDefinition());
        bool subComponentsTableOK = await EnsureTableExistsAsync(InventorySubComponentsTableName, GetInventorySubComponentsTableDefinition());
        bool accountInvItemsTableOK = await EnsureTableExistsAsync(AccountInventoryItemsTableName, GetAccountInvItemsTableDefinition());
        bool accountInvResourceItemsTableOK = await EnsureTableExistsAsync(AccountInventoryResourceItemsTableName, GetAccountInvResourceItemsTableDefinition());
        bool accountInvSubComponentsTableOK = await EnsureTableExistsAsync(AccountInventorySubComponentsTableName, GetAccountInvSubComponentsTableDefinition());
        bool ownedWorkbenchesTableOK = await EnsureTableExistsAsync(OwnedWorkbenchesTableName, GetOwnedWorkbenchesTableDefinition());

        if (!inventoryItemsTableOK || !invResourceItemsTableOK || !subComponentsTableOK || !ownedWorkbenchesTableOK)
        {
            throw new Exception("Failed to initialize required inventory database tables async.");
        }
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
            {"ResourceID", "INT"},
            {"Quantity", "INT"}
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
            {"ResourceID", "INT"},
            {"Quantity", "INT"}
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

        bool itemExists = await CheckIfInventoryItemExistsAsync(charId, itemId);
        if (itemExists)
        {
            LogWarning($"Item with ID {itemId} already exists in inventory for character {charId}. Cannot add duplicate.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"CharID", charId},
            {"ItemID", itemId},
            {"SlotID", slotId}
        };

        try
        {
            bool success = await SaveDataAsync(InventoryItemsTableName, values);
            if (success == false)
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
            if (success == false)
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
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving inventory resource items (slots) for character {charId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    public async Task<bool> AddInventoryResourceItemAsync(int charId, int resourceItemIdToAdd, int quantity)
    {
        if (charId <= 0 || resourceItemIdToAdd <= 0 || quantity <= 0)
        {
            LogError("Invalid parameters provided for AddInventoryResourceItemAsync.");
            return false;
        }

        bool resourceItemExists = await CheckIfInventoryResourceItemExistsAsync(charId, resourceItemIdToAdd);
        if (resourceItemExists)
        {
            return await UpdateResourceItemAsync(charId, resourceItemIdToAdd, quantity);
        }

        Dictionary<string, object> linkedItemsParams = new Dictionary<string, object> 
        { 
            { "@CharID", charId },
            { "@ResourceID", resourceItemIdToAdd },
            { "@Quantity", quantity }
        };
        try
        {
            bool success = await SaveDataAsync(InventoryResourceItemsTableName, linkedItemsParams);
            if (success == false)
            {
                LogWarning($"Failed to add resource item {resourceItemIdToAdd} for character {charId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding resourceItem to inventory", ex);
            return false;
        }
    }
    public async Task<bool> UpdateResourceItemAsync(int charId, int resourceItemIdToAdd, int quantity)
    {
        if (charId <= 0 || resourceItemIdToAdd <= 0 || quantity <= 0)
        {
            LogError("Invalid parameters provided for UpdateResourceItemAsync.");
            return false;
        }

        bool resourceItemExists = await CheckIfInventoryResourceItemExistsAsync(charId, resourceItemIdToAdd);
        if (!resourceItemExists)
        {
            return await AddInventoryResourceItemAsync(charId, resourceItemIdToAdd, quantity);
        }

        string whereCondition = "CharID = @CharID AND ResourceID = @ResourceID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@ResourceID", resourceItemIdToAdd}
        };

        Dictionary<string, object> valuesToUpdate = new Dictionary<string, object>
        {
            { "Quantity", quantity } 
        };

        try
        {
            bool success = await UpdateDataAsync(InventoryResourceItemsTableName, valuesToUpdate, whereCondition, whereParams);
            if (success == false)
            {
                LogWarning($"Failed to update resource item {resourceItemIdToAdd} quantity for character {charId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception updating resource item {resourceItemIdToAdd} in inventory for character {charId}", ex);
            return false;
        }
    }
    public async Task<bool> RemoveInventoryResourceItemAsync(int charId, int resourceId)
    {
        if (charId <= 0 || resourceId <= 0)
        {
            LogError("Invalid parameters provided for RemoveInventoryResourceItemAsync.");
            return false;
        }

        string whereCondition = "CharID = @CharID AND ResourceID = @ResourceItemID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@ResourceID", resourceId}
        };

        try
        {
            bool success = await DeleteDataAsync(InventoryResourceItemsTableName, whereCondition, whereParams);
            if (success == false)
            {
                LogWarning($"Failed to remove resource item {resourceId} from inventory for character {charId}");
            }
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

        bool subComponentExists = await CheckIfInventorySubComponentExistsAsync(charId, subComponentId);
        if (subComponentExists)
        {
            LogWarning($"SubComponent with ID {subComponentId} already exists in inventory for character {charId}. Cannot add duplicate.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"CharID", charId},
            {"SubComponentID", subComponentId}
        };

        try
        {
            bool success = await SaveDataAsync(InventorySubComponentsTableName, values);
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
            bool success = await SaveDataAsync(OwnedWorkbenchesTableName, values);
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
            if (success == false)
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
            if (success == false)
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
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving account inventory resource items for account {accountId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    public async Task<bool> AddAccountInventoryResourceItemAsync(int accountId, int resourceItemId, int quantity)
    {
        if (accountId <= 0 || resourceItemId <= 0 || quantity <= 0)
        {
            LogError("Invalid parameters provided for AddAccountInventoryResourceItemAsync.");
            return false;
        }

        bool resourceItemExists = await CheckIfAccountInventoryResourceItemExistsAsync(accountId, resourceItemId);
        if (resourceItemExists)
        {
            return await UpdateInventoryResourceItemAsync(accountId, resourceItemId, quantity);
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            { "AccountID", accountId },
            { "ResourceItemID", resourceItemId },
            { "@Quantity", quantity }
        };

        try
        {
            bool success = await SaveDataAsync(AccountInventoryResourceItemsTableName, values);
            if (success == false)
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
    public async Task<bool> UpdateInventoryResourceItemAsync(int accntId, int resourceItemIdToAdd, int quantity)
    {
        if (accntId <= 0 || resourceItemIdToAdd <= 0 || quantity <= 0)
        {
            return await AddAccountInventoryResourceItemAsync(accntId, resourceItemIdToAdd, quantity);
        }
        string whereCondition = "AccountID = @AccountID AND ResourceItemID = @ResourceItemID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@AccountID", accntId},
            {"@ResourceItemID", resourceItemIdToAdd}
        };
        Dictionary<string, object> valuesToUpdate = new Dictionary<string, object>
        {
            { "Quantity", quantity }
        };
        try
        {
            bool success = await UpdateDataAsync(AccountInventoryResourceItemsTableName, valuesToUpdate, whereCondition, whereParams);
            if (success == false)
            {
                LogWarning($"Failed to update resource item {resourceItemIdToAdd} quantity for account {accntId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception updating resource item {resourceItemIdToAdd} in inventory for account {accntId}", ex);
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
            if (success == false)
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
            if (success == false)
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
            if (success == false)
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
    private async Task<bool> CheckIfInventoryResourceItemExistsAsync(int charId, int resourceId)
    {
        string query = $"SELECT COUNT(*) FROM `{InventoryResourceItemsTableName}` WHERE CharID = @CharID AND ResourceItemID = @ResourceItemID";
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@ResourceID", resourceId}
        };

        try
        {
            object result = await DatabaseManager.Instance.ExecuteScalarAsync(query, parameters);
            int count = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
        catch (Exception ex)
        {
            LogError($"Error checking if inventory resource item exists (CharID: {charId}, ResourceItemID: {resourceId})", ex);
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
    private async Task<bool> CheckIfAccountInventoryResourceItemExistsAsync(int accountId, int resourceId)
    {
        string query = $"SELECT COUNT(*) FROM `{AccountInventoryResourceItemsTableName}` WHERE AccountID = @AccountID AND ResourceItemID = @ResourceItemID";
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            {"@AccountID", accountId},
            {"@ResourceItemID", resourceId}
        };

        try
        {
            object result = await DatabaseManager.Instance.ExecuteScalarAsync(query, parameters);
            int count = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
        catch (Exception ex)
        {
            LogError($"Error checking if account inventory resource item exists (AccountID: {accountId}, ResourceItemID: {resourceId})", ex);
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

[System.Serializable]
public struct ItemData : INetworkSerializable
{
    public int ItemID;
    public int ItemTemplateID;
    public string ItemName;
    public int ItemType;
    public int Durability;
    public int MaxDurability;
    public float Damage;
    public float Speed;
    public int DamageType;
    public int SlotType;
    public float SlashResist;
    public float ThrustResist;
    public float CrushResist;
    public float HeatResist;
    public float ShockResist;
    public float ColdResist;
    public float MindResist;
    public float CorruptResist;
    public int Icon;
    public string Colour;
    public int Weight;
    public int Model;
    public bool Stackable;
    public int StackSizeMax;
    public int Price;
    public int SlotID;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemID);
        serializer.SerializeValue(ref ItemTemplateID);
        serializer.SerializeValue(ref ItemName);
        serializer.SerializeValue(ref ItemType);
        serializer.SerializeValue(ref Durability);
        serializer.SerializeValue(ref MaxDurability);
        serializer.SerializeValue(ref Damage);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref DamageType);
        serializer.SerializeValue(ref SlotType);
        serializer.SerializeValue(ref SlashResist);
        serializer.SerializeValue(ref ThrustResist);
        serializer.SerializeValue(ref CrushResist);
        serializer.SerializeValue(ref HeatResist);
        serializer.SerializeValue(ref ShockResist);
        serializer.SerializeValue(ref ColdResist);
        serializer.SerializeValue(ref MindResist);
        serializer.SerializeValue(ref CorruptResist);
        serializer.SerializeValue(ref Icon);
        serializer.SerializeValue(ref Colour);
        serializer.SerializeValue(ref Weight);
        serializer.SerializeValue(ref Model);
        serializer.SerializeValue(ref Stackable);
        serializer.SerializeValue(ref StackSizeMax);
        serializer.SerializeValue(ref Price);
        serializer.SerializeValue(ref SlotID);
    }
}

[System.Serializable]
public struct ResourceItemData : INetworkSerializable
{
    public ResourceData ResourceData;
    public int ResourceSpawnID;
    public int CurrentStackSize;
    public int StackSizeMax;
    public float Weight;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ResourceData);
        serializer.SerializeValue(ref ResourceSpawnID);
        serializer.SerializeValue(ref CurrentStackSize);
        serializer.SerializeValue(ref StackSizeMax);
        serializer.SerializeValue(ref Weight);
    }
}

[System.Serializable]
public struct ResourceData : INetworkSerializable
{
    public int ResourceSpawnID;
    public string ResourceName;
    public int ResourceTemplateID;
    public int Type;
    public int SubType;
    public int Order;
    public int Family;
    public int Quality;
    public int Toughness;
    public int Strength;
    public int Density;
    public int Aura;
    public int Energy;
    public int Protein;
    public int Carbohydrate;
    public int Flavour;
    public int Weight;
    public int Value;
    public DateTime StartDate;
    public DateTime EndDate;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ResourceSpawnID);
        serializer.SerializeValue(ref ResourceName);
        serializer.SerializeValue(ref ResourceTemplateID);
        serializer.SerializeValue(ref Type);
        serializer.SerializeValue(ref SubType);
        serializer.SerializeValue(ref Order);
        serializer.SerializeValue(ref Family);
        serializer.SerializeValue(ref Quality);
        serializer.SerializeValue(ref Toughness);
        serializer.SerializeValue(ref Strength);
        serializer.SerializeValue(ref Density);
        serializer.SerializeValue(ref Aura);
        serializer.SerializeValue(ref Energy);
        serializer.SerializeValue(ref Protein);
        serializer.SerializeValue(ref Carbohydrate);
        serializer.SerializeValue(ref Flavour);
        serializer.SerializeValue(ref Weight);
        serializer.SerializeValue(ref Value);
        serializer.SerializeValue(ref StartDate);
        serializer.SerializeValue(ref EndDate);
    }
}

[System.Serializable]
public struct SubComponentData : INetworkSerializable
{
    public int SubComponentID;
    public string Name;
    public int SubComponentTemplateID;
    public int ComponentType;
    public int Quality;
    public int Toughness;
    public int Strength;
    public int Density;
    public int Aura;
    public int Energy;
    public int Protein;
    public int Carbohydrate;
    public int Flavour;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SubComponentID);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref SubComponentTemplateID);
        serializer.SerializeValue(ref ComponentType);
        serializer.SerializeValue(ref Quality);
        serializer.SerializeValue(ref Toughness);
        serializer.SerializeValue(ref Strength);
        serializer.SerializeValue(ref Density);
        serializer.SerializeValue(ref Aura);
        serializer.SerializeValue(ref Energy);
        serializer.SerializeValue(ref Protein);
        serializer.SerializeValue(ref Carbohydrate);
        serializer.SerializeValue(ref Flavour);
    }
}

[System.Serializable]
public struct WorkbenchData : INetworkSerializable
{
    public int WorkBenchType;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref WorkBenchType);
    }
}

[System.Serializable]
public struct AccountInventoryResult : INetworkSerializable
{
    public bool Success;
    public string ErrorMessage;
    public ItemData[] Items;
    public ResourceItemData[] ResourceItems;
    public SubComponentData[] SubComponents;
    public WorkbenchData[] Workbenches;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref ErrorMessage);
        serializer.SerializeValue(ref Items);
        serializer.SerializeValue(ref ResourceItems);
        serializer.SerializeValue(ref SubComponents);
        serializer.SerializeValue(ref Workbenches);
    }
}

[System.Serializable]
public struct CharacterInventoryResult : INetworkSerializable
{
    public bool Success;
    public string ErrorMessage;
    public ItemData[] Items;
    public ResourceItemData[] ResourceItems;
    public SubComponentData[] SubComponents;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref ErrorMessage);
        serializer.SerializeValue(ref Items);
        serializer.SerializeValue(ref ResourceItems);
        serializer.SerializeValue(ref SubComponents);
    }
}