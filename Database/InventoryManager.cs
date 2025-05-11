using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;
using System.Linq;

public class InventoryManager : BaseManager
{
    private const string InventoryItemsTableName = "InventoryItems";
    private const string ResourceItemsTableName = "ResourceItems";
    private const string InventoryResourceItemsTableName = "InventoryResourceItems";
    private const string InventorySubComponentsTableName = "InventorySubComponents";
    private const string OwnedWorkbenchesTableName = "OwnedWorkbenches";
    [SerializeField] private ResourceItem resourceItemPrefab; 
    [SerializeField] private Transform resourceItemParent;     
    private List<ResourceItem> resourceItems = new List<ResourceItem>();

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
    private void Start()
    {
        StartInitialization();
    }
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
        bool ownedWorkbenchesTableOK = await EnsureTableExistsAsync(OwnedWorkbenchesTableName, GetOwnedWorkbenchesTableDefinition());

        if (!inventoryItemsTableOK || !resourceItemsTableOK || !invResourceItemsTableOK || !subComponentsTableOK || !ownedWorkbenchesTableOK)
        {
            throw new Exception("Failed to initialize required inventory database tables async.");
        }
        LogInfo("Inventory data tables checked/initialized async.");

        // Load and instantiate ResourceItems after tables are confirmed
        await LoadAndInstantiateAllResourceItemsAsync();
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
    private Dictionary<string, string> GetOwnedWorkbenchesTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"OwnedWorkBenchID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"AccountID", "INT"},
            {"WorkBenchType", "INT"}
        };
    }
    private async Task LoadAndInstantiateAllResourceItemsAsync()
    {
        LogInfo("Loading and instantiating all ResourceItems from database...");

        if (resourceItemPrefab == null)
        {
            LogError("ResourceItem Prefab is not assigned in InventoryManager.");
            return;
        }
        if (resourceItemParent == null)
        {
            LogWarning("ResourceItem Parent is not assigned in InventoryManager. Instantiating at root.");
            // Optionally assign a default parent or handle this case as needed
        }
        if (ResourceManager.Instance == null)
        {
             LogError("ResourceManager instance not available. Cannot load Resource types.");
            return;
        }

        string query = $"SELECT * FROM `{ResourceItemsTableName}`";
        List<Dictionary<string, object>> results = await QueryDataAsync(query);

        resourceItems.Clear(); // Clear list before loading

        foreach (var row in results)
        {
            if (!row.TryGetValue("ResourceItemID", out object dbIdObj) || dbIdObj == DBNull.Value ||
                !row.TryGetValue("ResourceID", out object resourceIdObj) || resourceIdObj == DBNull.Value ||
                !row.TryGetValue("Quantity", out object quantityObj) || quantityObj == DBNull.Value)
            {
                LogWarning("Skipping ResourceItem row due to missing ID, ResourceID, or Quantity.");
                continue;
            }

            int resourceItemId = Convert.ToInt32(dbIdObj);
            int resourceId = Convert.ToInt32(resourceIdObj);
            int quantity = Convert.ToInt32(quantityObj);

            // Get the base Resource definition
            Resource resourceType = ResourceManager.Instance.GetResourceInstanceById(resourceId);
            if (resourceType == null)
            {
                LogWarning($"Resource type with ID {resourceId} not found in ResourceManager. Cannot instantiate ResourceItem ID: {resourceItemId}.");
                continue;
            }

            // Instantiate and initialize
            ResourceItem newInstance = Instantiate(resourceItemPrefab, resourceItemParent); // Parent can be null
            newInstance.Initialize(resourceType, quantity);
            newInstance.SetDatabaseID(resourceItemId); // << IMPORTANT: Assumes this method exists on ResourceItem

            resourceItems.Add(newInstance);
        }

        LogInfo($"Loaded and instantiated {resourceItems.Count} ResourceItems.");
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
    public ResourceItem GetResourceItemById(int resourceItemId)
    {
        if (resourceItems == null) return null;

        // Use Linq FirstOrDefault to find the item efficiently
        return resourceItems.FirstOrDefault(item => item != null && item.GetDatabaseID() == resourceItemId);
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
        bool itemExists = await CheckIfItemExistsAsync(charId, itemId);
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
    public async Task<bool> AddInventoryResourceItemAsync(int charId, int resourceItemId, int slotId)
    {
        if (charId <= 0 || resourceItemId <= 0 || slotId < 0)
        {
            LogError("Invalid parameters provided for AddInventoryResourceItemAsync.");
            return false;
        }

        // --- Check if resource item instance already exists for this character ---
        bool resourceExists = await CheckIfInventoryResourceItemExistsAsync(charId, resourceItemId);
        if (resourceExists)
        {
            LogWarning($"ResourceItem with ID {resourceItemId} already exists in inventory slots for character {charId}. Cannot add duplicate.");
            return false;
        }
        // --------

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

        // --- Check if subcomponent instance already exists for this character ---
        bool subComponentExists = await CheckIfInventorySubComponentExistsAsync(charId, subComponentId);
        if (subComponentExists)
        {
            LogWarning($"SubComponent with ID {subComponentId} already exists in inventory slots for character {charId}. Cannot add duplicate.");
            return false;
        }
        // --------

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

    #region Helpers
    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
    public void SetBagSpace(int newBagSpace)
    {
        // Implementation of SetBagSpace method
    }
    private async Task<bool> CheckIfItemExistsAsync(int charId, int itemId)
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
    #endregion
}