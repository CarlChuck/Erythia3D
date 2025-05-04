using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;

public class InventoryManager : BaseManager
{
    private const string InventoryItemsTableName = "InventoryItems";
    private const string ResourceItemsTableName = "ResourceItems";
    private const string InventoryResourceItemsTableName = "InventoryResourceItems";
    private const string InventorySubComponentsTableName = "InventorySubComponents";

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

    private void Start()
    {
        StartInitialization();
    }

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
        bool resourceItemsTableOK = await EnsureTableExistsAsync(ResourceItemsTableName, GetResourceItemsTableDefinition());
        bool invResourceItemsTableOK = await EnsureTableExistsAsync(InventoryResourceItemsTableName, GetInventoryResourceItemsTableDefinition());
        bool subComponentsTableOK = await EnsureTableExistsAsync(InventorySubComponentsTableName, GetInventorySubComponentsTableDefinition());

        if (!inventoryItemsTableOK || !resourceItemsTableOK || !invResourceItemsTableOK || !subComponentsTableOK)
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
    private Dictionary<string, string> GetResourceItemsTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"ResourceItemID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"CharID", "INT"},
            {"ResourceID", "INT"},
            {"Quantity", "INT DEFAULT 1"}
        };
    }
    private Dictionary<string, string> GetInventoryResourceItemsTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"InventoryResourceItemID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"CharID", "INT"},
            {"ResourceItemID", "INT"},
            {"SlotID", "INT"}
        };
    }
    private Dictionary<string, string> GetInventorySubComponentsTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"InventorySubComponentID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"CharID", "INT"},
            {"SubComponentID", "INT"},
            {"SlotID", "INT"}
        };
    }
    #endregion

    #region Resource Items Methods
    public async Task<List<Dictionary<string, object>>> GetCharacterResourceItemTotalsAsync(int charId)
    {
        if (charId <= 0)
        {
            LogError("Invalid CharID provided for GetCharacterResourceItemTotalsAsync.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{ResourceItemsTableName}` WHERE CharID = @CharID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@CharID", charId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            LogInfo($"Retrieved {results.Count} resource item totals for character {charId}");
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving resource item totals for character {charId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }
    private async Task<Dictionary<string, object>> GetResourceItemTotalAsync(int charId, int resourceId)
    {
        string query = $"SELECT * FROM `{ResourceItemsTableName}` WHERE CharID = @CharID AND ResourceID = @ResourceID LIMIT 1";
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@ResourceID", resourceId}
        };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            return results.Count > 0 ? results[0] : null;
        }
        catch (Exception ex)
        {
            LogError($"Error checking for existing resource item total (CharID: {charId}, ResourceID: {resourceId})", ex);
            return null;
        }
    }
    public async Task<bool> AddOrUpdateResourceItemTotalAsync(int charId, int resourceId, int quantityToAdd)
    {
        if (charId <= 0 || resourceId <= 0 || quantityToAdd == 0) // Allow negative quantityToAdd for removal
        {
            LogError("Invalid parameters provided for AddOrUpdateResourceItemTotalAsync.");
            return false;
        }

        try
        {
            Dictionary<string, object> existingItem = await GetResourceItemTotalAsync(charId, resourceId);

            if (existingItem != null)
            {
                // Item exists, update quantity
                if (existingItem.TryGetValue("Quantity", out object currentQuantityObj) && int.TryParse(currentQuantityObj.ToString(), out int currentQuantity))
                {
                    int newQuantity = currentQuantity + quantityToAdd;
                    if (newQuantity > 0)
                    {
                        // Update existing row
                        return await UpdateResourceItemTotalQuantityAsync(charId, resourceId, newQuantity);
                    }
                    else
                    {
                        // Quantity is zero or less, remove the row
                        return await RemoveResourceItemTotalAsync(charId, resourceId);
                    }
                }
                else
                {
                    LogError($"Could not parse existing quantity for CharID: {charId}, ResourceID: {resourceId}. Existing data: {currentQuantityObj}");
                    return false;
                }
            }
            else if (quantityToAdd > 0)
            {
                // Item does not exist, and we are adding quantity, insert new record
                Dictionary<string, object> values = new Dictionary<string, object>
                {
                    {"CharID", charId},
                    {"ResourceID", resourceId},
                    {"Quantity", quantityToAdd}
                };
                bool success = await SaveDataAsync(ResourceItemsTableName, values);
                LogInfo(success ? $"Added new resource total {resourceId} (quantity: {quantityToAdd}) for character {charId}" : $"Failed to add new resource total {resourceId} for character {charId}");
                return success;
            }
            else
            {
                // Item does not exist and quantityToAdd is not positive, nothing to do
                LogInfo($"Attempted to remove quantity for non-existent resource total (CharID: {charId}, ResourceID: {resourceId}). No action taken.");
                return true; // Operation is technically successful (state is as expected)
            }
        }
        catch (Exception ex)
        {
            LogError($"Exception during AddOrUpdateResourceItemTotalAsync (CharID: {charId}, ResourceID: {resourceId})", ex);
            return false;
        }
    }
    private async Task<bool> UpdateResourceItemTotalQuantityAsync(int charId, int resourceId, int newQuantity)
    {
        if (charId <= 0 || resourceId <= 0 || newQuantity <= 0) // Should only update to positive quantity
        {
            LogError("Invalid parameters provided for UpdateResourceItemTotalQuantityAsync.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object> { {"Quantity", newQuantity} };
        string whereCondition = "CharID = @CharID AND ResourceID = @ResourceID";
        Dictionary<string, object> whereParams = new Dictionary<string, object> { {"@CharID", charId}, {"@ResourceID", resourceId} };

        try
        {
            bool success = await UpdateDataAsync(ResourceItemsTableName, values, whereCondition, whereParams);
            LogInfo(success ? $"Updated resource total {resourceId} quantity to {newQuantity} for character {charId}" : $"Failed to update resource total {resourceId} quantity for character {charId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception updating resource item total quantity", ex);
            return false;
        }
    }
    public async Task<bool> RemoveResourceItemTotalAsync(int charId, int resourceId)
    {
        if (charId <= 0 || resourceId <= 0)
        {
            LogError("Invalid parameters provided for RemoveResourceItemTotalAsync.");
            return false;
        }

        string whereCondition = "CharID = @CharID AND ResourceID = @ResourceID";
        Dictionary<string, object> whereParams = new Dictionary<string, object> { {"@CharID", charId}, {"@ResourceID", resourceId} };

        try
        {
            bool success = await DeleteDataAsync(ResourceItemsTableName, whereCondition, whereParams);
            LogInfo(success ? $"Removed resource total {resourceId} for character {charId}" : $"Failed to remove resource total {resourceId} for character {charId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing resource item total", ex);
            return false;
        }
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

    #region Inventory Resource Items Methods
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
    public async Task<bool> AddInventoryResourceItemAsync(int charId, int resourceItemId, int slotId)
    {
        if (charId <= 0 || resourceItemId <= 0 || slotId < 0)
        {
            LogError("Invalid parameters provided for AddInventoryResourceItemAsync.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"CharID", charId},
            {"ResourceItemID", resourceItemId}, // Link to the specific ResourceItem instance ID
            {"SlotID", slotId}
        };

        try
        {
            bool success = await SaveDataAsync(InventoryResourceItemsTableName, values);
            LogInfo(success ? $"Added inventory resource item {resourceItemId} to slot {slotId} for character {charId}" : $"Failed to add inventory resource item {resourceItemId} to slot {slotId} for character {charId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding inventory resource item", ex);
            return false;
        }
    }
    public async Task<bool> RemoveInventoryResourceItemAsync(int charId, int slotId)
    {
        if (charId <= 0 || slotId < 0)
        {
            LogError("Invalid parameters provided for RemoveInventoryResourceItemAsync.");
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
            bool success = await DeleteDataAsync(InventoryResourceItemsTableName, whereCondition, whereParams);
            LogInfo(success ? $"Removed inventory resource item from slot {slotId} for character {charId}" : $"Failed to remove inventory resource item from slot {slotId} for character {charId}");
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
    public async Task<bool> AddInventorySubComponentAsync(int charId, int subComponentId, int slotId)
    {
        if (charId <= 0 || subComponentId <= 0 || slotId < 0)
        {
            LogError("Invalid parameters provided for AddInventorySubComponentAsync.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"CharID", charId},
            {"SubComponentID", subComponentId}, // Link to the specific SubComponent instance ID
            {"SlotID", slotId}
        };

        try
        {
            bool success = await SaveDataAsync(InventorySubComponentsTableName, values);
            LogInfo(success ? $"Added inventory subcomponent {subComponentId} to slot {slotId} for character {charId}" : $"Failed to add inventory subcomponent {subComponentId} to slot {slotId} for character {charId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding inventory subcomponent", ex);
            return false;
        }
    }
    public async Task<bool> RemoveInventorySubComponentAsync(int charId, int slotId)
    {
        if (charId <= 0 || slotId < 0)
        {
            LogError("Invalid parameters provided for RemoveInventorySubComponentAsync.");
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
            bool success = await DeleteDataAsync(InventorySubComponentsTableName, whereCondition, whereParams);
            LogInfo(success ? $"Removed inventory subcomponent from slot {slotId} for character {charId}" : $"Failed to remove inventory subcomponent from slot {slotId} for character {charId}");
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing inventory subcomponent", ex);
            return false;
        }
    }
    #endregion

    #region Helpers
    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
    #endregion
}