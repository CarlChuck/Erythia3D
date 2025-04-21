using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;

public class InventoryManager : BaseManager
{
    private const string InventoryTableName = "Inventory";
    private const string ResourceItemsTableName = "ResourceItems";

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
        bool inventoryTableOK = await EnsureTableExistsAsync(InventoryTableName, GetInventoryTableDefinition());
        bool resourceItemsTableOK = await EnsureTableExistsAsync(ResourceItemsTableName, GetResourceItemsTableDefinition());

        if (!inventoryTableOK || !resourceItemsTableOK)
        {
            throw new Exception("Failed to initialize required inventory database tables async.");
        }
        LogInfo("Inventory data tables checked/initialized async.");
    }
    private Dictionary<string, string> GetInventoryTableDefinition()
    {
        return new Dictionary<string, string> 
        {
            {"InventoryID", "INT AUTO_INCREMENT PRIMARY KEY"},
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
    #endregion

    #region Inventory Methods
    public async Task<List<Dictionary<string, object>>> GetCharacterInventoryAsync(int charId)
    {
        if (charId <= 0)
        {
            LogError("Invalid CharID provided.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{InventoryTableName}` WHERE CharID = @CharID";
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
            bool success = await SaveDataAsync(InventoryTableName, values);
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
            bool success = await DeleteDataAsync(InventoryTableName, whereCondition, whereParams);
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

    #region Resource Items Methods
    public async Task<List<Dictionary<string, object>>> GetCharacterResourceItemsAsync(int charId)
    {
        if (charId <= 0)
        {
            LogError("Invalid CharID provided.");
            return new List<Dictionary<string, object>>();
        }

        string query = $"SELECT * FROM `{ResourceItemsTableName}` WHERE CharID = @CharID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@CharID", charId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            LogInfo($"Retrieved {results.Count} resource items for character {charId}");
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving resource items for character {charId}", ex);
            return new List<Dictionary<string, object>>();
        }
    }

    public async Task<bool> AddResourceItemAsync(int charId, int resourceId, int quantity = 1)
    {
        if (charId <= 0 || resourceId <= 0 || quantity <= 0)
        {
            LogError("Invalid parameters provided for AddResourceItem.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"CharID", charId},
            {"ResourceID", resourceId},
            {"Quantity", quantity}
        };

        try
        {
            bool success = await SaveDataAsync(ResourceItemsTableName, values);
            if (success)
            {
                LogInfo($"Added resource {resourceId} (quantity: {quantity}) for character {charId}");
            }
            else
            {
                LogWarning($"Failed to add resource {resourceId} for character {charId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception adding resource item", ex);
            return false;
        }
    }

    public async Task<bool> UpdateResourceItemQuantityAsync(int charId, int resourceId, int newQuantity)
    {
        if (charId <= 0 || resourceId <= 0 || newQuantity < 0)
        {
            LogError("Invalid parameters provided for UpdateResourceItemQuantity.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"Quantity", newQuantity}
        };

        string whereCondition = "CharID = @CharID AND ResourceID = @ResourceID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@ResourceID", resourceId}
        };

        try
        {
            bool success = await UpdateDataAsync(ResourceItemsTableName, values, whereCondition, whereParams);
            if (success)
            {
                LogInfo($"Updated resource {resourceId} quantity to {newQuantity} for character {charId}");
            }
            else
            {
                LogWarning($"Failed to update resource {resourceId} quantity for character {charId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception updating resource item quantity", ex);
            return false;
        }
    }

    public async Task<bool> RemoveResourceItemAsync(int charId, int resourceId)
    {
        if (charId <= 0 || resourceId <= 0)
        {
            LogError("Invalid parameters provided for RemoveResourceItem.");
            return false;
        }

        string whereCondition = "CharID = @CharID AND ResourceID = @ResourceID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            {"@CharID", charId},
            {"@ResourceID", resourceId}
        };

        try
        {
            bool success = await DeleteDataAsync(ResourceItemsTableName, whereCondition, whereParams);
            if (success)
            {
                LogInfo($"Removed resource {resourceId} for character {charId}");
            }
            else
            {
                LogWarning($"Failed to remove resource {resourceId} for character {charId}");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception removing resource item", ex);
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