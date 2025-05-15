using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ItemManager : BaseManager
{
    private const string ItemInstancesTableName = "Items";
    private const string ItemTemplatesTableName = "ItemTemplates";
    private const string SubComponentsTableName = "SubComponents";
    private const string SubComponentTemplatesTableName = "SubComponentTemplates";

    [Header("Prefabs")]
    [SerializeField] private Item itemPrefab; 
    [SerializeField] private ItemTemplate itemTemplatePrefab;
    [SerializeField] private SubComponent subComponentPrefab;
    [SerializeField] private SubComponentTemplate subComponentTemplatePrefab;

    [Header("Parent Transforms")]
    [SerializeField] private Transform itemsParent; 
    [SerializeField] private Transform itemTemplatesParent; 
    [SerializeField] private Transform subComponentTemplatesParent;
    [SerializeField] private Transform subComponentsParent;

    [Header("Runtime Data")]
    private List<ItemTemplate> itemTemplates = new List<ItemTemplate>();
    private List<Item> items = new List<Item>();
    private List<SubComponentTemplate> subComponentTemplates = new List<SubComponentTemplate>();
    private List<SubComponent> subComponents = new List<SubComponent>();
    private Dictionary<int, ItemTemplate> templatesById = new Dictionary<int, ItemTemplate>();
    private Dictionary<int, Item> itemsById = new Dictionary<int, Item>(); 
    private Dictionary<int, SubComponentTemplate> subComponentTemplatesById = new Dictionary<int, SubComponentTemplate>();
    private Dictionary<int, SubComponent> subComponentsById = new Dictionary<int, SubComponent>();

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

    #region InitializeLoading
    protected override async Task InitializeAsync()
    {
        itemTemplates.Clear();
        items.Clear();
        templatesById.Clear();
        itemsById.Clear();
        subComponentTemplatesById.Clear();
        subComponentsById.Clear();

        try
        {
            await EnsureItemTablesExistAsync();

            LogInfo("Loading Item Templates...");
            List<ItemTemplate> newItemTemplates = await LoadAllItemTemplatesAsync();
            if (newItemTemplates == null)
            { 
                throw new Exception("Failed to load Item Templates."); 
            }
            itemTemplates = newItemTemplates; 
            templatesById = newItemTemplates.ToDictionary(t => t.ItemTemplateID, t => t);
            LogInfo($"Loaded and indexed {templatesById.Count} item templates.");

            LogInfo("Loading Item Instances...");
            List<Item> itemInstances = await LoadAllItemInstancesAsync();
            if (itemInstances == null) 
            { 
                throw new Exception("Failed to load Item Instances."); 
            }
            items = itemInstances;
            itemsById = itemInstances.ToDictionary(i => i.ItemID, i => i);
            LogInfo($"Loaded {itemsById.Count} item instances.");
            LinkItemsToTemplates();

            LogInfo("Loading SubComponent Templates...");
            List<SubComponentTemplate> newSubCompTemplates = await LoadSubComponentTemplatesAsync(); // Get list
            if (newSubCompTemplates == null) 
            { 
                throw new Exception("Failed to load SubComponent Templates."); 
            }
            subComponentTemplates = newSubCompTemplates;
            subComponentTemplatesById = newSubCompTemplates.ToDictionary(t => t.ComponentTemplateID, t => t);
            LogInfo($"Loaded and indexed {subComponentTemplatesById.Count} sub-component templates.");

            LogInfo("Loading SubComponent Instances...");
            List<SubComponent> subCompInstances = await LoadAllSubComponentsAsync(); // Get list
            if (subCompInstances == null) 
            { 
                throw new Exception("Failed to load SubComponent Instances."); 
            }
            subComponents = subCompInstances;
            subComponentsById = subCompInstances.ToDictionary(i => i.SubComponentID, i => i);
            LogInfo($"Loaded {subComponentsById.Count} sub-component instances.");
            LinkSubComponentsToTemplates();

            // 5. Mark as Initialized and Notify (on main thread)
            await Task.Factory.StartNew(() => {
                isInitialized = true;
                LogInfo("ItemManager Initialization Complete. Invoking OnDataLoaded.");
                NotifyDataLoaded();
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
        LogInfo("Checking and initializing item & sub-component data tables async...");
        bool templateTableOK = await EnsureTableExistsAsync(ItemTemplatesTableName, GetItemTemplateTableDefinition());
        bool instanceTableOK = await EnsureTableExistsAsync(ItemInstancesTableName, GetItemTableDefinition());
        bool subCompTemplateTableOK = await EnsureTableExistsAsync(SubComponentTemplatesTableName, GetSubComponentTemplateTableDefinition());
        bool subCompInstanceTableOK = await EnsureTableExistsAsync(SubComponentsTableName, GetSubComponentTableDefinition());

        if (!templateTableOK || !instanceTableOK || !subCompTemplateTableOK || !subCompInstanceTableOK)
        {
            throw new Exception("Failed to initialize required item/sub-component database tables async.");
        }
        LogInfo("Item & sub-component data tables checked/initialized async.");
    }
    public async Task<List<ItemTemplate>> LoadAllItemTemplatesAsync()
    {
        List<ItemTemplate> templates = new List<ItemTemplate>();

        string query = $"SELECT * FROM `{ItemTemplatesTableName}`";
        LogInfo($"ItemManager executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null) 
            { 
                LogError($"Query failed for '{ItemTemplatesTableName}'."); 
                return null; 
            }
            if (results.Count == 0) 
            { 
                LogWarning($"No results for '{ItemTemplatesTableName}'."); 
                return templates; 
            }

            foreach (var rowData in results)
            {
                ItemTemplate template = Instantiate(itemTemplatePrefab.gameObject, itemTemplatesParent).GetComponent<ItemTemplate>();
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

        string query = $"SELECT * FROM `{ItemInstancesTableName}`"; // Query the instances table
        LogInfo($"ItemManager executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null) 
            { 
                LogError($"Query failed for '{ItemInstancesTableName}'."); 
                return null; 
            }
            if (results.Count == 0) 
            { 
                LogWarning($"No results for '{ItemInstancesTableName}'."); 
                return instances; 
            }

            foreach (var rowData in results)
            {
                Item instance = Instantiate(itemPrefab.gameObject, itemsParent).GetComponent<Item>();
                MapDictionaryToItem(instance, rowData);
                instances.Add(instance);
            }
            LogInfo($"Loaded {instances.Count} item instance data rows.");
            return instances;
        }
        catch (Exception ex) 
        { 
            LogError($"Error loading item instances: {ex.Message}"); 
            return null; 
        }
    }
    private async Task<List<SubComponentTemplate>> LoadSubComponentTemplatesAsync()
    {
        List<SubComponentTemplate> loadedList = new List<SubComponentTemplate>();

        string query = $"SELECT * FROM {SubComponentTemplatesTableName}";
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null)
            {
                Debug.LogError("Failed to load sub-component templates from database.");
                return loadedList; // Return empty list on error
            }
            if (results.Count == 0)
            {
                LogWarning($"No results for '{ItemInstancesTableName}'.");
                return loadedList;
            }

            foreach (var row in results)
            {
                SubComponentTemplate template = Instantiate(subComponentTemplatePrefab.gameObject, subComponentTemplatesParent).GetComponent<SubComponentTemplate>();
                MapDictionaryToSubComponentTemplate(template, row);
                loadedList.Add(template);
            }
            LogInfo($"Finished processing sub-component templates. Returning {loadedList.Count} templates.");
            return loadedList;
        }
        catch (Exception ex)
        {
            LogError($"Error loading subComponentTemplates: {ex.Message}");
            return null;
        }
    }
    private async Task<List<SubComponent>> LoadAllSubComponentsAsync()
    {
        List<SubComponent> loadedList = new List<SubComponent>();

        string query = $"SELECT * FROM {SubComponentsTableName}"; 
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null)
            {
                Debug.LogError("Failed to load sub-component templates from database.");
                return loadedList; // Return empty list on error
            }
            if (results.Count == 0)
            {
                LogWarning($"No results for '{ItemInstancesTableName}'.");
                return loadedList;
            }

            foreach (var row in results)
            {
                SubComponent instance = Instantiate(subComponentPrefab.gameObject, subComponentsParent).GetComponent<SubComponent>();
                MapDictionaryToSubComponent(instance, row);
                loadedList.Add(instance);
            }
            Debug.Log($"Finished processing sub-component instances. Returning {loadedList.Count} instances.");
            return loadedList; // Return the populated list
        }
        catch (Exception ex)
        {
            LogError($"Error loading subComponents: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region TableDefinitions
    private Dictionary<string, string> GetItemTemplateTableDefinition()
    {
        return new Dictionary<string, string> {
             {"ItemTemplateID", "INT AUTO_INCREMENT PRIMARY KEY"}, 
             {"Name", "VARCHAR(255) NOT NULL"},
             {"ItemType", "INT"}, 
             {"ExamineText", "TEXT"}, 
             {"MaxDurability", "INT DEFAULT 100"},
             {"Damage", "FLOAT DEFAULT 0"}, 
             {"Speed", "FLOAT DEFAULT 1.0"}, 
             {"DamageType", "INT DEFAULT 0"}, 
             {"SlotType", "INT DEFAULT 0"}, 
             {"SlashResist", "FLOAT DEFAULT 0"}, {"ThrustResist", "FLOAT DEFAULT 0"}, {"CrushResist", "FLOAT DEFAULT 0"},
             {"HeatResist", "FLOAT DEFAULT 0"}, {"ShockResist", "FLOAT DEFAULT 0"}, {"ColdResist", "FLOAT DEFAULT 0"},
             {"MindResist", "FLOAT DEFAULT 0"}, {"CorruptResist", "FLOAT DEFAULT 0"},
             {"Icon", "INT"}, 
             {"Colour", "VARCHAR(7) DEFAULT '#FFFFFF'"}, 
             {"Weight", "INT DEFAULT 1"},
             {"Model", "INT"}, 
             {"Stackable", "TINYINT(1) DEFAULT 0"}, 
             {"StackSizeMax", "INT DEFAULT 1"},
             {"Price", "INT DEFAULT 0"}
        };
    }
    private Dictionary<string, string> GetItemTableDefinition()
    {
        return new Dictionary<string, string> {
             {"ItemID", "INT AUTO_INCREMENT PRIMARY KEY"}, 
             {"ItemTemplateID", "INT NOT NULL"}, 
             {"ItemName", "VARCHAR(255) NULL"},
             {"ItemType", "INT"}, 
             {"Durability", "INT"}, 
             {"MaxDurability", "INT DEFAULT 100"},
             {"Damage", "FLOAT NULL"}, 
             {"Speed", "FLOAT NULL"},
             {"DamageType", "INT DEFAULT 0"}, 
             {"SlotType", "INT DEFAULT 0"}, 
             {"SlashResist", "FLOAT NULL"}, {"ThrustResist", "FLOAT NULL"}, {"CrushResist", "FLOAT NULL"},
             {"HeatResist", "FLOAT NULL"}, {"ShockResist", "FLOAT NULL"}, {"ColdResist", "FLOAT NULL"},
             {"MindResist", "FLOAT NULL"}, {"CorruptResist", "FLOAT NULL"},
             {"Icon", "INT"}, 
             {"Colour", "VARCHAR(7) DEFAULT '#FFFFFF'"}, 
             {"Weight", "INT DEFAULT 1"},
             {"Model", "INT"}, 
             {"Stackable", "TINYINT(1) DEFAULT 0"}, 
             {"StackSizeMax", "INT DEFAULT 1"},
             {"Price", "INT DEFAULT 0"}
        };
    }
    private Dictionary<string, string> GetSubComponentTemplateTableDefinition()
    {
        return new Dictionary<string, string>
        {
            { "ComponentTemplateID", "INT AUTO_INCREMENT PRIMARY KEY" },
            { "Name", "VARCHAR(255) NOT NULL" },
            { "ComponentType", "INT" },
            { "Icon", "INT" },
            { "Colour", "VARCHAR(7) DEFAULT '#FFFFFF'" },
            { "Weight", "INT DEFAULT 0" },
            { "Stackable", "TINYINT(1) DEFAULT 0" },
            { "StackSizeMax", "INT DEFAULT 1" },
            { "Price", "INT DEFAULT 0" }
        };
    }
    private Dictionary<string, string> GetSubComponentTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"SubComponentID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"Name", "VARCHAR(255) NULL"},
            {"SubComponentTemplateID", "INT NOT NULL"},
            {"ComponentType", "INT DEFAULT 0"},
            {"Quality", "INT DEFAULT 0"},
            {"Toughness", "INT DEFAULT 0"},
            {"Strength", "INT DEFAULT 0"},
            {"Density", "INT DEFAULT 0"},
            {"Aura", "INT DEFAULT 0"},
            {"Energy", "INT DEFAULT 0"},
            {"Protein", "INT DEFAULT 0"},
            {"Carbohydrate", "INT DEFAULT 0"},
            {"Flavour", "INT DEFAULT 0"}
        };
    }
    #endregion

    #region GetInstances
    public ItemTemplate GetItemTemplateById(int templateId)
    {
        if (!isInitialized)
        {
            LogWarning("ItemManager accessed before initialization!");
        }
        templatesById.TryGetValue(templateId, out ItemTemplate template);
        return template;
    }
    public Item GetItemInstanceByID(int instanceId)
    {
        if (!isInitialized) 
        { 
            LogWarning("ItemManager accessed before initialization!"); 
        }
        itemsById.TryGetValue(instanceId, out Item item);
        return item;
    }
    public SubComponentTemplate GetSubComponentTemplateByID(int templateID)
    {
        if (!isInitialized)
        {
            LogWarning("ItemManager accessed before initialization!");
        }
        subComponentTemplatesById.TryGetValue(templateID, out SubComponentTemplate template);
        return template;
    }
    public SubComponent GetSubComponentInstanceByID(int instanceId)
    {
        if (!isInitialized)
        {
            LogWarning("ItemManager accessed before initialization!");
        }
        subComponentsById.TryGetValue(instanceId, out SubComponent instance);
        return instance;
    }
    #endregion

    #region Item
    public async Task<long> SaveNewItemAsync(Item itemInstance)
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
            {"WeaponType", itemInstance.WeaponType},
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
                    items.Add(itemInstance); // Add to runtime list
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
    public async Task<bool> UpdateItemAsync(Item itemInstance)
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
            {"WeaponType", itemInstance.WeaponType},
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
    public async Task<bool> DeleteItemAsync(int itemInstanceId)
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
                    items.Remove(itemToRemove);
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

    #region SubComponent
    public async Task<long> SaveNewSubComponentAsync(SubComponent subComponentInstance)
    {
        if (subComponentInstance == null || subComponentInstance.Template == null)
        {
            LogError("Cannot save null sub-component instance or instance without a template.");
            return -1;
        }
        // Use template name if instance name is null/empty for logging
        string logName = string.IsNullOrEmpty(subComponentInstance.Name) ? subComponentInstance.Template.Name : subComponentInstance.Name;
        LogInfo($"Attempting to save new SubComponent Instance: {logName} (TemplateID: {subComponentInstance.SubComponentTemplateID})");

        // Prepare dictionary based on SubComponents table columns
        Dictionary<string, object> values = new Dictionary<string, object> {
            {"SubComponentTemplateID", subComponentInstance.SubComponentTemplateID},
            {"Name", string.IsNullOrEmpty(subComponentInstance.Name) ? (object)DBNull.Value : subComponentInstance.Name}, // Save DBNull if name is empty
            {"ComponentType", subComponentInstance.ComponentType}, // Use instance type
            {"Quality", subComponentInstance.Quality},
            {"Toughness", subComponentInstance.Toughness},
            {"Strength", subComponentInstance.Strength},
            {"Density", subComponentInstance.Density},
            {"Aura", subComponentInstance.Aura},
            {"Energy", subComponentInstance.Energy},
            {"Protein", subComponentInstance.Protein},
            {"Carbohydrate", subComponentInstance.Carbohydrate},
            {"Flavour", subComponentInstance.Flavour}
        };

        try
        {
            bool success = await SaveDataAsync(SubComponentsTableName, values);
            if (success)
            {
                long newId = await DatabaseManager.Instance.GetLastInsertIdAsync();
                if (newId > 0)
                {
                    subComponentInstance.SetSubComponentID((int)newId); // Update object with DB ID
                    subComponents.Add(subComponentInstance); // Add to runtime list
                    subComponentsById[subComponentInstance.SubComponentID] = subComponentInstance; // Add to lookup
                    LogInfo($"Saved new SubComponent Instance ID: {newId}");
                    return newId;
                }
                else
                {
                    LogError("SubComponent Instance insert succeeded but failed to get last insert ID.");
                    return -1;
                }
            }
            else
            {
                LogError("Failed to insert SubComponent Instance into database.");
                return -1;
            }
        }
        catch (Exception ex)
        {
            LogError($"Exception saving SubComponent Instance", ex);
            return -1;
        }
    }
    public async Task<bool> DeleteSubComponentAsync(int subComponentInstanceId)
    {
        if (subComponentInstanceId <= 0)
        {
            LogError("Invalid SubComponentID provided for deletion.");
            return false;
        }
        LogInfo($"Attempting to delete SubComponent Instance ID: {subComponentInstanceId}");

        string whereCondition = "`SubComponentID` = @where_SubComponentID"; // Use backticks for safety
        Dictionary<string, object> whereParams = new Dictionary<string, object> {
            { "@where_SubComponentID", subComponentInstanceId }
        };

        try
        {
            // Call the base class delete method
            bool success = await DeleteDataAsync(SubComponentsTableName, whereCondition, whereParams);

            if (success)
            {
                // Remove from runtime dictionary
                if (subComponentsById.TryGetValue(subComponentInstanceId, out SubComponent componentToRemove))
                {
                    subComponentsById.Remove(subComponentInstanceId);
                    subComponents.Remove(componentToRemove);
                    LogInfo($"Deleted SubComponent Instance ID: {subComponentInstanceId}");

                    // Destroy GameObject if it exists
                    if (componentToRemove != null && componentToRemove.gameObject != null)
                    {
                        Destroy(componentToRemove.gameObject);
                        LogInfo($"Destroyed GameObject for SubComponent Instance ID: {subComponentInstanceId}");
                    }
                }
                else
                {
                    // Row deleted from DB, but wasn't found in runtime dictionary (potentially inconsistent state, but log it)
                    LogWarning($"SubComponent Instance ID: {subComponentInstanceId} deleted from DB, but not found in runtime dictionary.");
                }
            }
            else
            {
                // Deletion failed (likely row didn't exist)
                LogWarning($"Failed to delete SubComponent Instance ID: {subComponentInstanceId} (may not exist).");
            }
            return success; // Return success/failure status from DB operation
        }
        catch (Exception ex)
        {
            LogError($"Exception deleting SubComponent Instance ID {subComponentInstanceId}", ex);
            return false; // Return false on exception
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
            template.SetItemTemplate(
                SafeConvert.ToInt32(data, "ItemTemplateID"), 
                SafeConvert.ToString(data, "Name"),
                SafeConvert.ToInt32(data, "ItemType"),
                SafeConvert.ToString(data, "ExamineText"),
                SafeConvert.ToInt32(data, "MaxDurability", 100), 
                SafeConvert.ToSingle(data, "Damage"), 
                SafeConvert.ToSingle(data, "Speed", 1.0f),
                SafeConvert.ToInt32(data, "WeaponType"),
                SafeConvert.ToInt32(data, "SlotType"),
                SafeConvert.ToSingle(data, "SlashResist"), 
                SafeConvert.ToSingle(data, "ThrustResist"), 
                SafeConvert.ToSingle(data, "CrushResist"),
                SafeConvert.ToSingle(data, "HeatResist"), 
                SafeConvert.ToSingle(data, "ShockResist"), 
                SafeConvert.ToSingle(data, "ColdResist"),
                SafeConvert.ToSingle(data, "MindResist"), 
                SafeConvert.ToSingle(data, "CorruptResist"),
                SafeConvert.ToInt32(data, "Icon"),
                SafeConvert.ToString(data, "Colour", "#FFFFFF"),
                SafeConvert.ToInt32(data, "Weight"),
                SafeConvert.ToInt32(data, "Model"),
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
    private void MapDictionaryToItem(Item item, Dictionary<string, object> data)
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
                SafeConvert.ToInt32(data, "MaxDurability", 100), 
                SafeConvert.ToSingle(data, "Damage"), 
                SafeConvert.ToSingle(data, "Speed", 1.0f),
                SafeConvert.ToInt32(data, "WeaponType"),
                SafeConvert.ToInt32(data, "SlotType"),
                SafeConvert.ToSingle(data, "SlashResist"), 
                SafeConvert.ToSingle(data, "ThrustResist"), 
                SafeConvert.ToSingle(data, "CrushResist"),
                SafeConvert.ToSingle(data, "HeatResist"), 
                SafeConvert.ToSingle(data, "ShockResist"), 
                SafeConvert.ToSingle(data, "ColdResist"),
                SafeConvert.ToSingle(data, "MindResist"), 
                SafeConvert.ToSingle(data, "CorruptResist"),
                SafeConvert.ToInt32(data, "Icon"), // Use ToInt32
                SafeConvert.ToString(data, "Colour", "#FFFFFF"),
                SafeConvert.ToInt32(data, "Weight"),
                SafeConvert.ToInt32(data, "Model"), // Use ToInt32
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
    private void MapDictionaryToSubComponentTemplate(SubComponentTemplate subComponentTemplate, Dictionary<string, object> data)
    {
        try
        {
            subComponentTemplate.SetSubComponentTemplate(
                SafeConvert.ToInt32(data, "ComponentTemplateID"),
                SafeConvert.ToString(data, "Name"),
                SafeConvert.ToInt32(data, "ComponentType"),
                SafeConvert.ToInt32(data, "Icon"),
                SafeConvert.ToString(data, "Colour", "#FFFFFF"),
                SafeConvert.ToInt32(data, "Weight", 0),
                SafeConvert.ToBoolean(data, "Stackable", false),
                SafeConvert.ToInt32(data, "StackSizeMax", 1),
                SafeConvert.ToInt32(data, "Price", 0)
            );
        }
        catch (Exception ex)
        {
            LogError($"Error mapping dictionary to SubComponentTemplate (ID: {data.GetValueOrDefault("ComponentTemplateID", "N/A")}): {ex.Message} - Data: {DictToString(data)}");
        }
    }
    private void MapDictionaryToSubComponent(SubComponent subComponent, Dictionary<string, object> data)
    {
        try
        {
            subComponent.SetSubComponent(
                SafeConvert.ToInt32(data, "SubComponentID"),
                SafeConvert.ToString(data, "Name"),
                SafeConvert.ToInt32(data, "SubComponentTemplateID"),
                SafeConvert.ToInt32(data, "ComponentType", 0),
                SafeConvert.ToInt32(data, "Quality", 0),
                SafeConvert.ToInt32(data, "Toughness", 0),
                SafeConvert.ToInt32(data, "Strength", 0),
                SafeConvert.ToInt32(data, "Density", 0),
                SafeConvert.ToInt32(data, "Aura", 0),
                SafeConvert.ToInt32(data, "Energy", 0),
                SafeConvert.ToInt32(data, "Protein", 0),
                SafeConvert.ToInt32(data, "Carbohydrate", 0),
                SafeConvert.ToInt32(data, "Flavour", 0)
            );
        }
        catch (Exception ex)
        {
            LogError($"Error mapping dictionary to SubComponent Instance (ID: {data.GetValueOrDefault("SubComponentID", "N/A")}): {ex.Message} - Data: {DictToString(data)}");
        }
    }
    private void LinkItemsToTemplates()
    {
        foreach (var itemInstance in items)
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
    private void LinkSubComponentsToTemplates()
    {
        foreach (var subComponent in subComponents)
        {
            if (subComponentTemplatesById.TryGetValue(subComponent.SubComponentTemplateID, out SubComponentTemplate template))
            {
                subComponent.Template = template;
            }
            else
            {
                LogWarning($"SubComponent instance ID {subComponent.SubComponentID} has missing template reference (TemplateID: {subComponent.SubComponentTemplateID}).");
            }
        }
        LogInfo("Linked sub-component instances to templates.");
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
    #endregion
}