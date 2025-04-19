using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // For CancellationToken
using System.Threading.Tasks;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    // --- Table Names ---
    private const string ItemTemplatesTableName = "ItemTemplates"; // Adjust if different
    private const string ItemInstancesTableName = "Items"; // Adjust if different

    [Header("Prefabs")]
    [SerializeField] private Item itemPrefab; // Prefab with Item component
    [SerializeField] private ItemTemplate itemTemplatePrefab; // Prefab with ItemTemplate component

    [Header("Parent Transforms (Optional)")]
    [SerializeField] private Transform itemInstancesParent; // For organizing instantiated items
    [SerializeField] private Transform itemTemplatesParent; // For organizing templates

    [Header("Runtime Data (Loaded)")]
    private List<ItemTemplate> loadedTemplates = new List<ItemTemplate>();
    private List<Item> loadedItemInstances = new List<Item>();
    private Dictionary<int, ItemTemplate> templatesById = new Dictionary<int, ItemTemplate>();
    private Dictionary<int, Item> itemsById = new Dictionary<int, Item>(); // Lookup for instances

    // --- Initialization State ---
    public bool isInitialized { get; private set; } = false;
    private Task initializationTask;
    public event Action OnDataLoaded; // Event when initialization is complete

    #region Singleton
    public static ItemManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate ItemManager instance detected. Destroying self.");
            Destroy(gameObject);
            return;
        }
    }
    #endregion


    private void Start()
    {
        // Kick off the asynchronous initialization
        StartInitialization();
        // Subscribe to completion event for post-load actions if needed
        OnDataLoaded += PerformPostLoadActions;
    }
    private void OnDestroy()
    {
        // Unsubscribe
        OnDataLoaded -= PerformPostLoadActions;
        if (Instance == this) { Instance = null; }
    }
    private void PerformPostLoadActions()
    {
        Debug.Log("ItemManager Post-Load Actions Started...");
        // Parent instantiated objects if desired
        ParentInstantiatedObjects(loadedTemplates.Select(t => t?.gameObject), itemTemplatesParent);
        ParentInstantiatedObjects(loadedItemInstances.Select(i => i?.gameObject), itemInstancesParent);
        Debug.Log("ItemManager Post-Load Actions Complete.");
    }
    private void ParentInstantiatedObjects(IEnumerable<GameObject> objectsToParent, Transform parent)
    {
        if (parent == null)
        {
            Debug.LogWarning("Cannot parent objects, parent transform is null.");
            return;
        }
        foreach (var obj in objectsToParent)
        {
            if (obj != null)
            {
                obj.transform.SetParent(parent, false);
            }
        }
    }


    // --- Public Initialization Trigger ---
    public void StartInitialization()
    {
        if (initializationTask == null || initializationTask.IsCompleted)
        {
            Debug.Log("Starting ItemManager Initialization...");
            isInitialized = false;
            initializationTask = InitializeAsync();

            initializationTask.ContinueWith(t => {
                if (t.IsFaulted)
                {
                    Debug.LogError($"ItemManager Initialization Failed: {t.Exception}");
                    isInitialized = false;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        else { Debug.LogWarning("ItemManager initialization already in progress."); }
    }

    // --- Core Async Initialization ---
    private async Task InitializeAsync()
    {
        loadedTemplates.Clear();
        loadedItemInstances.Clear();
        templatesById.Clear();
        itemsById.Clear();

        try
        {
            // 1. Ensure Tables Exist
            await EnsureItemTablesExistAsync();

            // 2. Load Templates
            Debug.Log("Loading Item Templates...");
            List<ItemTemplate> templates = await LoadAllItemTemplatesAsync();
            if (templates == null) throw new Exception("Failed to load Item Templates.");
            loadedTemplates = templates;
            templatesById = loadedTemplates.ToDictionary(t => t.ItemTemplateID, t => t);
            Debug.Log($"Loaded and indexed {loadedTemplates.Count} item templates.");
            // TODO: Optionally load sprites/models for templates here

            // 3. Load Instances
            Debug.Log("Loading Item Instances...");
            List<Item> instances = await LoadAllItemInstancesAsync();
            if (instances == null) throw new Exception("Failed to load Item Instances.");
            loadedItemInstances = instances;
            itemsById = loadedItemInstances.ToDictionary(i => i.ItemID, i => i); // Assumes ItemID is unique instance ID
            Debug.Log($"Loaded {loadedItemInstances.Count} item instances.");

            // 4. Link Instances to Templates
            LinkInstancesToTemplates();
            Debug.Log("Linked item instances to templates.");

            // 5. Mark as Initialized and Notify (on main thread)
            await Task.Factory.StartNew(() => {
                isInitialized = true;
                Debug.Log("ItemManager Initialization Complete. Invoking OnDataLoaded.");
                OnDataLoaded?.Invoke();
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

        }
        catch (Exception ex)
        {
            await Task.Factory.StartNew(() => {
                Debug.LogError($"ItemManager Initialization Async Error: {ex.Message}\n{ex.StackTrace}");
                isInitialized = false;
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    // --- Linking Helper ---
    private void LinkInstancesToTemplates()
    {
        foreach (var itemInstance in loadedItemInstances)
        {
            if (templatesById.TryGetValue(itemInstance.ItemTemplateID, out ItemTemplate template))
            {
                itemInstance.Template = template;
            }
            else
            {
                Debug.LogWarning($"Item instance ID {itemInstance.ItemID} has missing template reference (TemplateID: {itemInstance.ItemTemplateID}).");
            }
        }
    }

    // --- Table Initialization ---
    private async Task EnsureItemTablesExistAsync()
    {
        Debug.Log("Checking and initializing item data tables async...");
        bool templateTableOK = await EnsureTableExistsAsync(ItemTemplatesTableName, GetItemTemplateTableDefinition());
        bool instanceTableOK = await EnsureTableExistsAsync(ItemInstancesTableName, GetItemTableDefinition());

        if (!templateTableOK || !instanceTableOK)
        {
            throw new Exception("Failed to initialize required item database tables async.");
        }
        Debug.Log("Item data tables checked/initialized async.");
    }

    private async Task<bool> EnsureTableExistsAsync(string tableName, Dictionary<string, string> columns)
    {
        // Re-use logic from ResourceManager or implement similarly using DatabaseManager async methods
        try
        {
            bool exists = await DatabaseManager.Instance.TableExistsAsync(tableName);
            if (!exists)
            {
                Debug.Log($"Table '{tableName}' does not exist. Attempting to create async...");
                bool created = await DatabaseManager.Instance.CreateTableIfNotExistsAsync(tableName, columns);
                if (created) { Debug.Log($"Successfully created table '{tableName}' async."); return true; }
                else { Debug.LogError($"Failed to create table '{tableName}' async."); return false; }
            }
            else { Debug.Log($"Table '{tableName}' already exists."); return true; }
        }
        catch (Exception ex) { Debug.LogError($"Error checking/creating table async '{tableName}': {ex.Message}"); return false; }
    }

    // --- Table Definitions ---
    private Dictionary<string, string> GetItemTemplateTableDefinition()
    {
        // Based on ItemType columns from image
        return new Dictionary<string, string> {
             {"ItemTemplateID", "INT AUTO_INCREMENT PRIMARY KEY"}, // Renamed to ID for consistency? Or keep ItemTemplateID? Let's use ID.
             {"Name", "VARCHAR(255) NOT NULL"},
             {"ItemType", "INT"}, // Maps to ItemType Enum
             {"ExamineText", "TEXT"}, // Use TEXT for potentially longer descriptions
             {"MaxDurability", "INT DEFAULT 100"},
             {"Damage", "FLOAT DEFAULT 0"}, // Use FLOAT
             {"Speed", "FLOAT DEFAULT 1.0"}, // Use FLOAT
             {"DamageType", "INT DEFAULT 0"}, // Maps to ItemDamageType Enum
             {"SlotType", "INT DEFAULT 0"}, // Maps to ItemSlotType Enum
             {"SlashResist", "FLOAT DEFAULT 0"}, {"ThrustResist", "FLOAT DEFAULT 0"}, {"CrushResist", "FLOAT DEFAULT 0"},
             {"HeatResist", "FLOAT DEFAULT 0"}, {"ShockResist", "FLOAT DEFAULT 0"}, {"ColdResist", "FLOAT DEFAULT 0"},
             {"MindResist", "FLOAT DEFAULT 0"}, {"CorruptResist", "FLOAT DEFAULT 0"},
             {"Icon", "VARCHAR(255)"}, // Path to icon asset
             {"Colour", "VARCHAR(7) DEFAULT '#FFFFFF'"}, // Hex color string
             {"Weight", "FLOAT DEFAULT 1.0"}, // Use FLOAT
             {"Model", "VARCHAR(255)"}, // Path to model asset
             {"Bonus1", "INT DEFAULT 0"}, {"Bonus2", "INT DEFAULT 0"}, {"Bonus3", "INT DEFAULT 0"}, {"Bonus4", "INT DEFAULT 0"},
             {"Bonus5", "INT DEFAULT 0"}, {"Bonus6", "INT DEFAULT 0"}, {"Bonus7", "INT DEFAULT 0"}, {"Bonus8", "INT DEFAULT 0"},
             {"Bonus1Type", "INT DEFAULT 0"}, {"Bonus2Type", "INT DEFAULT 0"}, {"Bonus3Type", "INT DEFAULT 0"}, {"Bonus4Type", "INT DEFAULT 0"},
             {"Bonus5Type", "INT DEFAULT 0"}, {"Bonus6Type", "INT DEFAULT 0"}, {"Bonus7Type", "INT DEFAULT 0"}, {"Bonus8Type", "INT DEFAULT 0"},
             {"Stackable", "TINYINT(1) DEFAULT 0"}, // Boolean (0 or 1)
             {"StackSizeMax", "INT DEFAULT 1"},
             {"Price", "INT DEFAULT 0"}
        };
    }

    private Dictionary<string, string> GetItemTableDefinition()
    {
        // Based on Item columns from image + necessary additions
        return new Dictionary<string, string> {
             {"ItemID", "INT AUTO_INCREMENT PRIMARY KEY"}, // Unique instance ID
             {"ItemTemplateID", "INT NOT NULL"}, // Foreign Key
             {"ItemName", "VARCHAR(255) NULL"},
             {"ItemType", "INT"}, // Maps to ItemType Enum
             {"Durability", "INT"}, // Current Durability
             {"MaxDurability", "INT DEFAULT 100"},
             {"Damage", "FLOAT NULL"}, 
             {"Speed", "FLOAT NULL"},
             {"DamageType", "INT DEFAULT 0"}, // Maps to ItemDamageType Enum
             {"SlotType", "INT DEFAULT 0"}, // Maps to ItemSlotType Enum
             {"SlashResist", "FLOAT NULL"}, {"ThrustResist", "FLOAT NULL"}, {"CrushResist", "FLOAT NULL"},
             {"HeatResist", "FLOAT NULL"}, {"ShockResist", "FLOAT NULL"}, {"ColdResist", "FLOAT NULL"},
             {"MindResist", "FLOAT NULL"}, {"CorruptResist", "FLOAT NULL"},
             {"Icon", "VARCHAR(255)"}, // Path to icon asset
             {"Colour", "VARCHAR(7) DEFAULT '#FFFFFF'"}, // Hex color string
             {"Weight", "FLOAT DEFAULT 1.0"}, // Use FLOAT
             {"Model", "VARCHAR(255)"}, // Path to model asset
             {"Bonus1", "INT NULL"}, {"Bonus2", "INT NULL"}, {"Bonus3", "INT NULL"}, {"Bonus4", "INT NULL"},
             {"Bonus5", "INT NULL"}, {"Bonus6", "INT NULL"}, {"Bonus7", "INT NULL"}, {"Bonus8", "INT NULL"},
             {"Bonus1Type", "INT DEFAULT 0"}, {"Bonus2Type", "INT DEFAULT 0"}, {"Bonus3Type", "INT DEFAULT 0"}, {"Bonus4Type", "INT DEFAULT 0"},
             {"Bonus5Type", "INT DEFAULT 0"}, {"Bonus6Type", "INT DEFAULT 0"}, {"Bonus7Type", "INT DEFAULT 0"}, {"Bonus8Type", "INT DEFAULT 0"},
             {"Stackable", "TINYINT(1) DEFAULT 0"}, // Boolean (0 or 1)
             {"StackSizeMax", "INT DEFAULT 1"},
             {"Price", "INT DEFAULT 0"}
        };
    }

    // --- Data Loading ---
    public async Task<List<ItemTemplate>> LoadAllItemTemplatesAsync()
    {
        List<ItemTemplate> templates = new List<ItemTemplate>();
        if (itemTemplatePrefab == null) { Debug.LogError("ItemTemplate Prefab is not assigned!"); return null; }

        string query = $"SELECT * FROM `{ItemTemplatesTableName}`";
        Debug.Log($"ItemManager executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null) { Debug.LogError($"Query failed for '{ItemTemplatesTableName}'."); return null; }
            if (results.Count == 0) { Debug.LogWarning($"No results for '{ItemTemplatesTableName}'."); return templates; }

            foreach (var rowData in results)
            {
                ItemTemplate template = Instantiate(itemTemplatePrefab.gameObject).GetComponent<ItemTemplate>();
                if (template == null) { Debug.LogError("Failed to get ItemTemplate component."); continue; }
                MapDictionaryToItemTemplate(template, rowData);
                templates.Add(template);
            }
            Debug.Log($"Loaded {templates.Count} item template data rows.");
            return templates;
        }
        catch (Exception ex) { Debug.LogError($"Error loading item templates: {ex.Message}"); return null; }
    }

    public async Task<List<Item>> LoadAllItemInstancesAsync()
    {
        List<Item> instances = new List<Item>();
        if (itemPrefab == null) { Debug.LogError("Item Prefab is not assigned!"); return null; }

        string query = $"SELECT * FROM `{ItemInstancesTableName}`"; // Query the instances table
        Debug.Log($"ItemManager executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null) { Debug.LogError($"Query failed for '{ItemInstancesTableName}'."); return null; }
            if (results.Count == 0) { Debug.LogWarning($"No results for '{ItemInstancesTableName}'."); return instances; }

            foreach (var rowData in results)
            {
                Item instance = Instantiate(itemPrefab.gameObject).GetComponent<Item>();
                if (instance == null) { Debug.LogError("Failed to get Item component."); continue; }
                MapDictionaryToItemInstance(instance, rowData); // Use instance mapping function
                instances.Add(instance);
            }
            Debug.Log($"Loaded {instances.Count} item instance data rows.");
            return instances;
        }
        catch (Exception ex) { Debug.LogError($"Error loading item instances: {ex.Message}"); return null; }
    }

    // --- Mapping Helpers ---
    private void MapDictionaryToItemTemplate(ItemTemplate template, Dictionary<string, object> data)
    {
        try
        {
            // Use SafeConvert helper
            template.SetItemTemplate(
                SafeConvert.ToInt32(data, "ItemTemplateID"), // Match definition ID column name
                SafeConvert.ToString(data, "Name"),
                SafeConvert.ToInt32(data, "ItemType"),
                SafeConvert.ToString(data, "ExamineText"),
                SafeConvert.ToInt32(data, "MaxDurability", 100), // Provide default
                SafeConvert.ToSingle(data, "Damage"), // Use ToSingle for float
                SafeConvert.ToSingle(data, "Speed", 1.0f),
                SafeConvert.ToInt32(data, "DamageType"),
                SafeConvert.ToInt32(data, "SlotType"),
                SafeConvert.ToSingle(data, "SlashResist"), SafeConvert.ToSingle(data, "ThrustResist"), SafeConvert.ToSingle(data, "CrushResist"),
                SafeConvert.ToSingle(data, "HeatResist"), SafeConvert.ToSingle(data, "ShockResist"), SafeConvert.ToSingle(data, "ColdResist"),
                SafeConvert.ToSingle(data, "MindResist"), SafeConvert.ToSingle(data, "CorruptResist"),
                SafeConvert.ToInt32(data, "Icon"),
                SafeConvert.ToString(data, "Colour", "#FFFFFF"),
                SafeConvert.ToInt32(data, "Weight"),
                SafeConvert.ToInt32(data, "Model"),
                SafeConvert.ToInt32(data, "Bonus1"), SafeConvert.ToInt32(data, "Bonus2"), SafeConvert.ToInt32(data, "Bonus3"), SafeConvert.ToInt32(data, "Bonus4"),
                SafeConvert.ToInt32(data, "Bonus5"), SafeConvert.ToInt32(data, "Bonus6"), SafeConvert.ToInt32(data, "Bonus7"), SafeConvert.ToInt32(data, "Bonus8"),
                SafeConvert.ToInt32(data, "Bonus1Type"), SafeConvert.ToInt32(data, "Bonus2Type"), SafeConvert.ToInt32(data, "Bonus3Type"), SafeConvert.ToInt32(data, "Bonus4Type"),
                SafeConvert.ToInt32(data, "Bonus5Type"), SafeConvert.ToInt32(data, "Bonus6Type"), SafeConvert.ToInt32(data, "Bonus7Type"), SafeConvert.ToInt32(data, "Bonus8Type"),
                SafeConvert.ToBoolean(data, "Stackable"), 
                SafeConvert.ToInt32(data, "StackSizeMax", 1),
                SafeConvert.ToInt32(data, "Price")
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error mapping dictionary to ItemTemplate (ID: {data.GetValueOrDefault("ID", "N/A")}): {ex.Message} - Data: {DictToString(data)}");
        }
    }

    private void MapDictionaryToItemInstance(Item item, Dictionary<string, object> data)
    {
        try
        {
            // Use SafeConvert helper
            item.SetItem(
                SafeConvert.ToInt32(data, "ItemID"),
                SafeConvert.ToInt32(data, "ItemTemplateID"),
                SafeConvert.ToString(data, "ItemName"),
                SafeConvert.ToInt32(data, "ItemType"),
                SafeConvert.ToInt32(data, "Durability"),
                SafeConvert.ToInt32(data, "MaxDurability", 100), // Provide default
                SafeConvert.ToSingle(data, "Damage"), // Use ToSingle for float
                SafeConvert.ToSingle(data, "Speed", 1.0f),
                SafeConvert.ToInt32(data, "DamageType"),
                SafeConvert.ToInt32(data, "SlotType"),
                SafeConvert.ToSingle(data, "SlashResist"), SafeConvert.ToSingle(data, "ThrustResist"), SafeConvert.ToSingle(data, "CrushResist"),
                SafeConvert.ToSingle(data, "HeatResist"), SafeConvert.ToSingle(data, "ShockResist"), SafeConvert.ToSingle(data, "ColdResist"),
                SafeConvert.ToSingle(data, "MindResist"), SafeConvert.ToSingle(data, "CorruptResist"),
                SafeConvert.ToInt32(data, "Icon"),
                SafeConvert.ToString(data, "Colour", "#FFFFFF"),
                SafeConvert.ToInt32(data, "Weight"),
                SafeConvert.ToInt32(data, "Model"),
                SafeConvert.ToInt32(data, "Bonus1"), SafeConvert.ToInt32(data, "Bonus2"), SafeConvert.ToInt32(data, "Bonus3"), SafeConvert.ToInt32(data, "Bonus4"),
                SafeConvert.ToInt32(data, "Bonus5"), SafeConvert.ToInt32(data, "Bonus6"), SafeConvert.ToInt32(data, "Bonus7"), SafeConvert.ToInt32(data, "Bonus8"),
                SafeConvert.ToInt32(data, "Bonus1Type"), SafeConvert.ToInt32(data, "Bonus2Type"), SafeConvert.ToInt32(data, "Bonus3Type"), SafeConvert.ToInt32(data, "Bonus4Type"),
                SafeConvert.ToInt32(data, "Bonus5Type"), SafeConvert.ToInt32(data, "Bonus6Type"), SafeConvert.ToInt32(data, "Bonus7Type"), SafeConvert.ToInt32(data, "Bonus8Type"),
                SafeConvert.ToBoolean(data, "Stackable"),
                SafeConvert.ToInt32(data, "StackSizeMax", 1),
                SafeConvert.ToInt32(data, "Price")
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error mapping dictionary to Item Instance (ID: {data.GetValueOrDefault("ItemID", "N/A")}): {ex.Message} - Data: {DictToString(data)}");
        }
    }

    // --- Getters ---
    public Item GetItemPrefab() => itemPrefab;
    public ItemTemplate GetItemTemplatePrefab() => itemTemplatePrefab;

    public List<ItemTemplate> GetAllItemTemplates()
    {
        if (!isInitialized) Debug.LogWarning("ItemManager accessed before initialization!");
        return loadedTemplates;
    }
    public List<Item> GetAllItemInstances()
    {
        if (!isInitialized) Debug.LogWarning("ItemManager accessed before initialization!");
        return loadedItemInstances;
    }
    public ItemTemplate GetTemplateById(int templateId)
    {
        if (!isInitialized) Debug.LogWarning("ItemManager accessed before initialization!");
        templatesById.TryGetValue(templateId, out ItemTemplate template);
        return template;
    }
    public Item GetItemInstanceById(int instanceId)
    {
        if (!isInitialized) Debug.LogWarning("ItemManager accessed before initialization!");
        itemsById.TryGetValue(instanceId, out Item item);
        return item;
    }

    // --- Saving / Updating / Deleting (Example Signatures - Implement using DatabaseManager async methods) ---
    /*
    public async Task<long> SaveNewItemInstanceAsync(Item itemInstance)
    {
        if (itemInstance == null || itemInstance.Template == null)
        {
            Debug.LogError("Cannot save null item instance or item without a template.");
            return -1;
        }
        Debug.Log($"Attempting to save new Item Instance: {itemInstance.GetItemName()} (TemplateID: {itemInstance.ItemTemplateID})");

        // Prepare dictionary based on ItemInstances table columns
        Dictionary<string, object> values = new Dictionary<string, object> {
             {"ItemTemplateID", itemInstance.ItemTemplateID},
             {"InstanceName", (object)itemInstance.InstanceName ?? DBNull.Value}, // Use DBNull for optional fields
             {"Durability", itemInstance.CurrentDurability},
             // Add optional instance-specific overrides if they exist on the Item object
             // {"Quality", itemInstance.InstanceQuality ?? (object)DBNull.Value }, // Example
             {"OwnerID", itemInstance.OwnerID > 0 ? (object)itemInstance.OwnerID : DBNull.Value},
             {"ContainerID", itemInstance.ContainerID > 0 ? (object)itemInstance.ContainerID : DBNull.Value},
             {"SlotIndex", itemInstance.SlotIndex >= 0 ? (object)itemInstance.SlotIndex : DBNull.Value }
         };

        try
        {
            bool success = await DatabaseManager.Instance.InsertDataAsync(ItemInstancesTableName, values);
            if (success)
            {
                long newId = await DatabaseManager.Instance.GetLastInsertIdAsync();
                if (newId > 0)
                {
                    itemInstance.SetItemID((int)newId); // Update object with DB ID
                    loadedItemInstances.Add(itemInstance); // Add to runtime list
                    itemsById[itemInstance.ItemID] = itemInstance; // Add to lookup
                    Debug.Log($"Saved new Item Instance ID: {newId}");
                    // TODO: Consider invoking an OnInventoryChanged or similar event
                    return newId;
                }
                else
                {
                    Debug.LogError("Item Instance insert succeeded but failed to get last insert ID.");
                    return -1;
                }
            }
            else
            {
                Debug.LogError("Failed to insert Item Instance into database.");
                return -1;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception saving Item Instance: {ex.Message}");
            return -1;
        }
    }

    public async Task<bool> UpdateItemInstanceAsync(Item itemInstance)
    {
        if (itemInstance == null || itemInstance.ItemID <= 0)
        {
            Debug.LogError("Cannot update item instance: Invalid item or ItemID.");
            return false;
        }
        Debug.Log($"Attempting to update Item Instance ID: {itemInstance.ItemID}");

        // Prepare dictionary with fields that can be updated
        Dictionary<string, object> values = new Dictionary<string, object> {
             // Only include fields that are expected to change for an instance
             {"InstanceName", (object)itemInstance.InstanceName ?? DBNull.Value},
             {"Durability", itemInstance.CurrentDurability},
             // {"Quality", itemInstance.InstanceQuality ?? (object)DBNull.Value }, // Example
             {"OwnerID", itemInstance.OwnerID > 0 ? (object)itemInstance.OwnerID : DBNull.Value},
             {"ContainerID", itemInstance.ContainerID > 0 ? (object)itemInstance.ContainerID : DBNull.Value},
             {"SlotIndex", itemInstance.SlotIndex >= 0 ? (object)itemInstance.SlotIndex : DBNull.Value }
         };

        string whereCondition = "`ItemID` = @where_ItemID";
        Dictionary<string, object> whereParams = new Dictionary<string, object> {
             { "@where_ItemID", itemInstance.ItemID }
         };

        try
        {
            bool success = await DatabaseManager.Instance.UpdateDataAsync(ItemInstancesTableName, values, whereCondition, whereParams);
            if (success) Debug.Log($"Updated Item Instance ID: {itemInstance.ItemID}");
            else Debug.LogWarning($"Failed to update Item Instance ID: {itemInstance.ItemID} (may not exist or no changes made).");
            // TODO: Consider invoking an OnInventoryChanged or similar event
            return success;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception updating Item Instance ID {itemInstance.ItemID}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteItemInstanceAsync(int itemInstanceId)
    {
        if (itemInstanceId <= 0) return false;
        Debug.Log($"Attempting to delete Item Instance ID: {itemInstanceId}");

        string whereCondition = "`ItemID` = @where_ItemID";
        Dictionary<string, object> whereParams = new Dictionary<string, object> {
             { "@where_ItemID", itemInstanceId }
         };

        try
        {
            bool success = await DatabaseManager.Instance.DeleteDataAsync(ItemInstancesTableName, whereCondition, whereParams);
            if (success)
            {
                // Remove from runtime lists
                if (itemsById.TryGetValue(itemInstanceId, out Item itemToRemove))
                {
                    itemsById.Remove(itemInstanceId);
                    loadedItemInstances.Remove(itemToRemove);
                    Debug.Log($"Deleted Item Instance ID: {itemInstanceId}");
                    // Destroy GameObject if it exists? Handle carefully.
                    if (itemToRemove != null && itemToRemove.gameObject != null)
                    {
                        // Destroy(itemToRemove.gameObject); // uncomment if appropriate
                    }
                }
                // TODO: Consider invoking an OnInventoryChanged or similar event
            }
            else
            {
                Debug.LogWarning($"Failed to delete Item Instance ID: {itemInstanceId} (may not exist).");
            }
            return success;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception deleting Item Instance ID {itemInstanceId}: {ex.Message}");
            return false;
        }
    }
    */

    // --- Utility for safe dictionary conversion ---
    public static class SafeConvert
    {
        public static int ToInt32(Dictionary<string, object> data, string key, int defaultValue = 0) =>
            data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToInt32(value) : defaultValue;

        public static float ToSingle(Dictionary<string, object> data, string key, float defaultValue = 0f) =>
            data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToSingle(value) : defaultValue;

        public static string ToString(Dictionary<string, object> data, string key, string defaultValue = "") =>
            data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToString(value) : defaultValue;

        public static bool ToBoolean(Dictionary<string, object> data, string key, bool defaultValue = false) =>
             data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToBoolean(value) : defaultValue;

        public static DateTime ToDateTime(Dictionary<string, object> data, string key, DateTime defaultValue = default) =>
            data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToDateTime(value) : defaultValue;
    }
    // Helper for Debugging Dictionaries
    private string DictToString(Dictionary<string, object> dict)
    {
        if (dict == null) return "null";
        return "{" + string.Join(", ", dict.Select(kv => kv.Key + "=" + (kv.Value ?? "NULL"))) + "}";
    }

}