using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // For CancellationToken
using System.Threading.Tasks;
using UnityEngine;

public class ItemManager : BaseManager
{
    private const string ItemTemplatesTableName = "ItemTemplates"; 
    private const string ItemInstancesTableName = "Items"; 

    [Header("Prefabs")]
    [SerializeField] private Item itemPrefab; 
    [SerializeField] private ItemTemplate itemTemplatePrefab; 

    [Header("Parent Transforms (Optional)")]
    [SerializeField] private Transform itemInstancesParent; 
    [SerializeField] private Transform itemTemplatesParent; 

    [Header("Runtime Data (Loaded)")]
    private List<ItemTemplate> loadedTemplates = new List<ItemTemplate>();
    private List<Item> loadedItemInstances = new List<Item>();
    private Dictionary<int, ItemTemplate> templatesById = new Dictionary<int, ItemTemplate>();
    private Dictionary<int, Item> itemsById = new Dictionary<int, Item>(); 

    #region Singleton
    public static ItemManager Instance;

    protected void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate ItemManager detected. Destroying self.");
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

    public async Task<Item> CreateItemInstance(ItemTemplate template)
    {
        if (template == null)
        {
            LogError("Cannot create item instance: Template is null");
            return null;
        }

        // Create new item instance from prefab
        GameObject itemObj = Instantiate(itemPrefab.gameObject);
        Item newItem = itemObj.GetComponent<Item>();
        if (newItem == null)
        {
            LogError("Failed to get Item component from prefab");
            Destroy(itemObj);
            return null;
        }

        // Copy all statistics from template to new item
        newItem.SetItem(
            0, // ItemID will be set by database
            template.ItemTemplateID,
            template.ItemName,
            (int)template.Type,
            template.MaxDurability, // Start with full durability
            template.MaxDurability,
            template.Damage,
            template.Speed,
            (int)template.DmgType,
            (int)template.Slot,
            template.SlashResist,
            template.ThrustResist,
            template.CrushResist,
            template.HeatResist,
            template.ShockResist,
            template.ColdResist,
            template.MindResist,
            template.CorruptResist,
            template.IconPath,
            template.ColourHex,
            (int)template.Weight,
            template.ModelPath,
            template.Bonus1,
            template.Bonus2,
            template.Bonus3,
            template.Bonus4,
            template.Bonus5,
            template.Bonus6,
            template.Bonus7,
            template.Bonus8,
            (int)template.Bonus1Type,
            (int)template.Bonus2Type,
            (int)template.Bonus3Type,
            (int)template.Bonus4Type,
            (int)template.Bonus5Type,
            (int)template.Bonus6Type,
            (int)template.Bonus7Type,
            (int)template.Bonus8Type,
            template.IsStackable,
            template.StackSizeMax,
            template.Price
        );

        // Set the template reference
        newItem.Template = template;

        // Parent the item if a parent transform is specified
        if (itemInstancesParent != null)
        {
            itemObj.transform.SetParent(itemInstancesParent, false);
        }

        // Save to database and get the new ItemID
        long newItemId = await SaveNewItemInstanceAsync(newItem);
        if (newItemId <= 0)
        {
            LogError("Failed to save new item instance to database");
            Destroy(itemObj);
            return null;
        }

        // Set the new ItemID
        newItem.SetItemID((int)newItemId);

        // Add to loaded instances and lookup dictionary
        loadedItemInstances.Add(newItem);
        itemsById[newItem.ItemID] = newItem;

        return newItem;
    }

    #region InitializeLoading
    protected override async Task InitializeAsync()
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
            LogInfo("Loading Item Templates...");
            List<ItemTemplate> templates = await LoadAllItemTemplatesAsync();
            if (templates == null) throw new Exception("Failed to load Item Templates.");
            loadedTemplates = templates;
            templatesById = loadedTemplates.ToDictionary(t => t.ItemTemplateID, t => t);
            LogInfo($"Loaded and indexed {loadedTemplates.Count} item templates.");
            // TODO: Optionally load sprites/models for templates here

            // 3. Load Instances
            LogInfo("Loading Item Instances...");
            List<Item> instances = await LoadAllItemInstancesAsync();
            if (instances == null) throw new Exception("Failed to load Item Instances.");
            loadedItemInstances = instances;
            itemsById = loadedItemInstances.ToDictionary(i => i.ItemID, i => i); // Assumes ItemID is unique instance ID
            LogInfo($"Loaded {loadedItemInstances.Count} item instances.");

            // 4. Link Instances to Templates
            LinkInstancesToTemplates();
            LogInfo("Linked item instances to templates.");

            // 5. Mark as Initialized and Notify (on main thread)
            await Task.Factory.StartNew(() => {
                isInitialized = true;
                LogInfo("ItemManager Initialization Complete. Invoking OnDataLoaded.");
                NotifyDataLoaded(); // Use the protected method from BaseManager
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

        }
        catch (Exception ex)
        {
            await Task.Factory.StartNew(() => {
                LogError("ItemManager Initialization Async Error", ex);
                isInitialized = false;
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
    private async Task EnsureItemTablesExistAsync()
    {
        LogInfo("Checking and initializing item data tables async...");
        bool templateTableOK = await EnsureTableExistsAsync(ItemTemplatesTableName, GetItemTemplateTableDefinition());
        bool instanceTableOK = await EnsureTableExistsAsync(ItemInstancesTableName, GetItemTableDefinition());

        if (!templateTableOK || !instanceTableOK)
        {
            throw new Exception("Failed to initialize required item database tables async.");
        }
        LogInfo("Item data tables checked/initialized async.");
    }
    public async Task<List<ItemTemplate>> LoadAllItemTemplatesAsync()
    {
        List<ItemTemplate> templates = new List<ItemTemplate>();
        if (itemTemplatePrefab == null) { LogError("ItemTemplate Prefab is not assigned!"); return null; }

        string query = $"SELECT * FROM `{ItemTemplatesTableName}`";
        LogInfo($"ItemManager executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null) { LogError($"Query failed for '{ItemTemplatesTableName}'."); return null; }
            if (results.Count == 0) { LogWarning($"No results for '{ItemTemplatesTableName}'."); return templates; }

            foreach (var rowData in results)
            {
                ItemTemplate template = Instantiate(itemTemplatePrefab.gameObject).GetComponent<ItemTemplate>();
                if (template == null) { LogError("Failed to get ItemTemplate component."); continue; }
                MapDictionaryToItemTemplate(template, rowData);
                templates.Add(template);
            }
            LogInfo($"Loaded {templates.Count} item template data rows.");
            return templates;
        }
        catch (Exception ex) { LogError($"Error loading item templates: {ex.Message}"); return null; }
    }
    public async Task<List<Item>> LoadAllItemInstancesAsync()
    {
        List<Item> instances = new List<Item>();
        if (itemPrefab == null) { LogError("Item Prefab is not assigned!"); return null; }

        string query = $"SELECT * FROM `{ItemInstancesTableName}`"; // Query the instances table
        LogInfo($"ItemManager executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null) { LogError($"Query failed for '{ItemInstancesTableName}'."); return null; }
            if (results.Count == 0) { LogWarning($"No results for '{ItemInstancesTableName}'."); return instances; }

            foreach (var rowData in results)
            {
                Item instance = Instantiate(itemPrefab.gameObject).GetComponent<Item>();
                if (instance == null) { LogError("Failed to get Item component."); continue; }
                MapDictionaryToItemInstance(instance, rowData); // Use instance mapping function
                instances.Add(instance);
            }
            LogInfo($"Loaded {instances.Count} item instance data rows.");
            return instances;
        }
        catch (Exception ex) { LogError($"Error loading item instances: {ex.Message}"); return null; }
    }
    private void PerformPostLoadActions()
    {
        LogInfo("Performing post-load actions (parenting objects)...");
        foreach (Item item in loadedItemInstances)
        {
            if (item != null && item.gameObject != null && itemInstancesParent != null)
            {
                item.gameObject.transform.SetParent(itemInstancesParent, false);
            }
            else { LogWarning("Skipping parenting for null/destroyed item or missing parent."); }
        }
        foreach (ItemTemplate template in loadedTemplates)
        {
            if (template != null && template.gameObject != null && itemTemplatesParent != null)
            {
                template.gameObject.transform.SetParent(itemTemplatesParent, false);
            }
            else { LogWarning("Skipping parenting for null/destroyed template or missing parent."); }
        }
        LogInfo("Post-load actions complete.");
    }

    #endregion

    #region TableDefinitions
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
    #endregion

    #region Getters
    public Item GetItemPrefab()
    { 
        return itemPrefab; 
    }
    public ItemTemplate GetItemTemplatePrefab()
    { 
        return itemTemplatePrefab; 
    }
    public List<ItemTemplate> GetAllItemTemplates()
    {
        if (!isInitialized)
        { 
            LogWarning("ItemManager accessed before initialization!"); 
        }
        return loadedTemplates;
    }
    public List<Item> GetAllItemInstances()
    {
        if (!isInitialized)
        { 
            LogWarning("ItemManager accessed before initialization!"); 
        }
        return loadedItemInstances;
    }
    public ItemTemplate GetItemTemplateById(int templateId)
    {
        if (!isInitialized) 
        { 
            LogWarning("ItemManager accessed before initialization!"); 
        }
        templatesById.TryGetValue(templateId, out ItemTemplate template);
        return template;
    }
    public Item GetItemInstanceById(int instanceId)
    {
        if (!isInitialized) 
        { 
            LogWarning("ItemManager accessed before initialization!"); 
        }
        itemsById.TryGetValue(instanceId, out Item item);
        return item;
    }
    #endregion

    #region SaveUpdateDelete
    public async Task<long> SaveNewItemInstanceAsync(Item itemInstance)
    {
        if (itemInstance == null || itemInstance.Template == null)
        {
            LogError("Cannot save null item instance or item without a template.");
            return -1;
        }
        LogInfo($"Attempting to save new Item Instance: {itemInstance.ItemName} (TemplateID: {itemInstance.ItemTemplateID})");

        // Prepare dictionary based on ItemInstances table columns
        Dictionary<string, object> values = new Dictionary<string, object> {
            {"ItemTemplateID", itemInstance.ItemTemplateID},
            {"ItemName", (object)itemInstance.ItemName ?? DBNull.Value},
            {"ItemType", itemInstance.Type},
            {"Durability", itemInstance.Durability},
            {"MaxDurability", itemInstance.MaxDurability},
            {"Damage", itemInstance.Damage},
            {"Speed", itemInstance.Speed},
            {"DamageType", itemInstance.DamageType},
            {"SlotType", itemInstance.Slot},
            {"SlashResist", itemInstance.SlashResist},
            {"ThrustResist", itemInstance.ThrustResist},
            {"CrushResist", itemInstance.CrushResist},
            {"HeatResist", itemInstance.HeatResist},
            {"ShockResist", itemInstance.ShockResist},
            {"ColdResist", itemInstance.ColdResist},
            {"MindResist", itemInstance.MindResist},
            {"CorruptResist", itemInstance.CorruptResist},
            {"Icon", itemInstance.Icon},
            {"Colour", itemInstance.ColourHex},
            {"Weight", itemInstance.Weight},
            {"Model", itemInstance.Model},
            {"Bonus1", itemInstance.Bonus1},
            {"Bonus2", itemInstance.Bonus2},
            {"Bonus3", itemInstance.Bonus3},
            {"Bonus4", itemInstance.Bonus4},
            {"Bonus5", itemInstance.Bonus5},
            {"Bonus6", itemInstance.Bonus6},
            {"Bonus7", itemInstance.Bonus7},
            {"Bonus8", itemInstance.Bonus8},
            {"Bonus1Type", itemInstance.Bonus1Type},
            {"Bonus2Type", itemInstance.Bonus2Type},
            {"Bonus3Type", itemInstance.Bonus3Type},
            {"Bonus4Type", itemInstance.Bonus4Type},
            {"Bonus5Type", itemInstance.Bonus5Type},
            {"Bonus6Type", itemInstance.Bonus6Type},
            {"Bonus7Type", itemInstance.Bonus7Type},
            {"Bonus8Type", itemInstance.Bonus8Type},
            {"Stackable", itemInstance.IsStackable},
            {"StackSizeMax", itemInstance.StackSizeMax},
            {"Price", itemInstance.Price}
        };

        try
        {
            bool success = await SaveDataAsync(ItemInstancesTableName, values);
            if (success)
            {
                long newId = await DatabaseManager.Instance.GetLastInsertIdAsync();
                if (newId > 0)
                {
                    itemInstance.SetItemID((int)newId); // Update object with DB ID
                    loadedItemInstances.Add(itemInstance); // Add to runtime list
                    itemsById[itemInstance.ItemID] = itemInstance; // Add to lookup
                    LogInfo($"Saved new Item Instance ID: {newId}");
                    return newId;
                }
                else
                {
                    LogError("Item Instance insert succeeded but failed to get last insert ID.");
                    return -1;
                }
            }
            else
            {
                LogError("Failed to insert Item Instance into database.");
                return -1;
            }
        }
        catch (Exception ex)
        {
            LogError($"Exception saving Item Instance", ex);
            return -1;
        }
    }
    public async Task<bool> UpdateItemInstanceAsync(Item itemInstance)
    {
        if (itemInstance == null || itemInstance.ItemID <= 0)
        {
            LogError("Cannot update item instance: Invalid item or ItemID.");
            return false;
        }
        LogInfo($"Attempting to update Item Instance ID: {itemInstance.ItemID}");

        // Prepare dictionary with fields that can be updated
        Dictionary<string, object> values = new Dictionary<string, object> {
            {"ItemName", (object)itemInstance.ItemName ?? DBNull.Value},
            {"Durability", itemInstance.Durability},
            {"MaxDurability", itemInstance.MaxDurability},
            {"Damage", itemInstance.Damage},
            {"Speed", itemInstance.Speed},
            {"DamageType", itemInstance.DamageType},
            {"SlotType", itemInstance.Slot},
            {"SlashResist", itemInstance.SlashResist},
            {"ThrustResist", itemInstance.ThrustResist},
            {"CrushResist", itemInstance.CrushResist},
            {"HeatResist", itemInstance.HeatResist},
            {"ShockResist", itemInstance.ShockResist},
            {"ColdResist", itemInstance.ColdResist},
            {"MindResist", itemInstance.MindResist},
            {"CorruptResist", itemInstance.CorruptResist},
            {"Icon", itemInstance.Icon},
            {"Colour", itemInstance.ColourHex},
            {"Weight", itemInstance.Weight},
            {"Model", itemInstance.Model},
            {"Bonus1", itemInstance.Bonus1},
            {"Bonus2", itemInstance.Bonus2},
            {"Bonus3", itemInstance.Bonus3},
            {"Bonus4", itemInstance.Bonus4},
            {"Bonus5", itemInstance.Bonus5},
            {"Bonus6", itemInstance.Bonus6},
            {"Bonus7", itemInstance.Bonus7},
            {"Bonus8", itemInstance.Bonus8},
            {"Bonus1Type", itemInstance.Bonus1Type},
            {"Bonus2Type", itemInstance.Bonus2Type},
            {"Bonus3Type", itemInstance.Bonus3Type},
            {"Bonus4Type", itemInstance.Bonus4Type},
            {"Bonus5Type", itemInstance.Bonus5Type},
            {"Bonus6Type", itemInstance.Bonus6Type},
            {"Bonus7Type", itemInstance.Bonus7Type},
            {"Bonus8Type", itemInstance.Bonus8Type},
            {"Stackable", itemInstance.IsStackable},
            {"StackSizeMax", itemInstance.StackSizeMax},
            {"Price", itemInstance.Price}
        };

        string whereCondition = "`ItemID` = @where_ItemID";
        Dictionary<string, object> whereParams = new Dictionary<string, object> {
            { "@where_ItemID", itemInstance.ItemID }
        };

        try
        {
            bool success = await UpdateDataAsync(ItemInstancesTableName, values, whereCondition, whereParams);
            if (success) 
            {
                LogInfo($"Updated Item Instance ID: {itemInstance.ItemID}");
                // Update the runtime data
                if (itemsById.ContainsKey(itemInstance.ItemID))
                {
                    itemsById[itemInstance.ItemID] = itemInstance;
                }
            }
            else 
            {
                LogWarning($"Failed to update Item Instance ID: {itemInstance.ItemID} (may not exist or no changes made).");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception updating Item Instance ID {itemInstance.ItemID}", ex);
            return false;
        }
    }
    public async Task<bool> DeleteItemInstanceAsync(int itemInstanceId)
    {
        if (itemInstanceId <= 0) 
        {
            LogError("Invalid ItemID provided for deletion.");
            return false;
        }
        LogInfo($"Attempting to delete Item Instance ID: {itemInstanceId}");

        string whereCondition = "`ItemID` = @where_ItemID";
        Dictionary<string, object> whereParams = new Dictionary<string, object> {
            { "@where_ItemID", itemInstanceId }
        };

        try
        {
            bool success = await DeleteDataAsync(ItemInstancesTableName, whereCondition, whereParams);
            if (success)
            {
                // Remove from runtime lists
                if (itemsById.TryGetValue(itemInstanceId, out Item itemToRemove))
                {
                    itemsById.Remove(itemInstanceId);
                    loadedItemInstances.Remove(itemToRemove);
                    LogInfo($"Deleted Item Instance ID: {itemInstanceId}");
                    // Destroy GameObject if it exists
                    if (itemToRemove != null && itemToRemove.gameObject != null)
                    {
                        Destroy(itemToRemove.gameObject);
                    }
                }
            }
            else
            {
                LogWarning($"Failed to delete Item Instance ID: {itemInstanceId} (may not exist).");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception deleting Item Instance ID {itemInstanceId}", ex);
            return false;
        }
    }
    #endregion

    #region Helpers
    private string DictToString(Dictionary<string, object> dict)
    {
        if (dict == null) return "null";
        return "{" + string.Join(", ", dict.Select(kv => kv.Key + "=" + (kv.Value ?? "NULL"))) + "}";
    }
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
            LogError($"Error mapping dictionary to ItemTemplate (ID: {data.GetValueOrDefault("ID", "N/A")}): {ex.Message} - Data: {DictToString(data)}");
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
            LogError($"Error mapping dictionary to Item Instance (ID: {data.GetValueOrDefault("ItemID", "N/A")}): {ex.Message} - Data: {DictToString(data)}");
        }
    }
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
                LogWarning($"Item instance ID {itemInstance.ItemID} has missing template reference (TemplateID: {itemInstance.ItemTemplateID}).");
            }
        }
    }
    protected override void OnDestroy()
    {
        OnDataLoaded -= PerformPostLoadActions;
        base.OnDestroy();
    }
    #endregion
}