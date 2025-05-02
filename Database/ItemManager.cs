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
    private const string SubComponentTemplatesTableName = "SubComponentTemplates";
    private const string SubComponentsTableName = "SubComponents";

    [Header("Prefabs")]
    [SerializeField] private Item itemPrefab; 
    [SerializeField] private ItemTemplate itemTemplatePrefab; 
    [SerializeField] private SubComponentTemplate subComponentTemplatePrefab;
    [SerializeField] private SubComponent subComponentPrefab;

    [Header("Parent Transforms (Optional)")]
    [SerializeField] private Transform itemInstancesParent; 
    [SerializeField] private Transform itemTemplatesParent; 
    [SerializeField] private Transform subComponentTemplatesParent;
    [SerializeField] private Transform subComponentsParent;

    [Header("Runtime Data (Loaded)")]
    private List<ItemTemplate> loadedTemplates = new List<ItemTemplate>();
    private List<Item> loadedItemInstances = new List<Item>();
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

    private void Start()
    {
        // Kick off the asynchronous initialization
        StartInitialization();
        // Subscribe to completion event for post-load actions if needed
        OnDataLoaded += PerformPostLoadActions;
    }
    public async Task<Item> CreateItemInstanceAsync(ItemTemplate template)
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
    public async Task<SubComponent> CreateSubComponentInstanceAsync(SubComponentTemplate template, int quality = 0, int toughness = 0, int strength = 0, int density = 0, int aura = 0, int energy = 0, int protein = 0, int carbohydrate = 0, int flavour = 0)
    {
        if (template == null)
        {
            LogError("Cannot create sub-component instance: Template is null");
            return null;
        }
        if (subComponentPrefab == null)
        {
             LogError("Cannot create sub-component instance: SubComponent Prefab is null");
             return null;
        }

        // 1. Instantiate from prefab
        SubComponent newSubComponent = Instantiate(subComponentPrefab);
        if (newSubComponent == null)
        {
            LogError("Failed to instantiate SubComponent from prefab");
            return null;
        }

        // 2. Initialize with template data and PASSED instance values
        newSubComponent.Initialize(
            0, // ID will be set by DB
            template.Name, 
            template.ComponentTemplateID,
            template.ComponentType, // Copy type from template (could also be an argument if needed)
            quality,      // Use passed argument
            toughness,    // Use passed argument
            strength,     // Use passed argument
            density,      // Use passed argument
            aura,         // Use passed argument
            energy,       // Use passed argument
            protein,      // Use passed argument
            carbohydrate, // Use passed argument
            flavour       // Use passed argument
        );

        // 3. Set template reference
        newSubComponent.Template = template;
        newSubComponent.gameObject.name = $"SubCompInst_NEW_{template.Name}";

        // 4. Parent the instance if parent transform is specified
        if (subComponentsParent != null)
        {
            newSubComponent.transform.SetParent(subComponentsParent, false);
        }

        // 5. Save to database and get the new ID
        // SaveNewSubComponentAsync already reads the initialized values from the instance
        long newId = await SaveNewSubComponentAsync(newSubComponent);
        if (newId <= 0)
        {
            LogError("Failed to save new sub-component instance to database during creation.");
            Destroy(newSubComponent.gameObject); // Clean up instantiated object
            return null;
        }

        // ID is already set within SaveNewSubComponentAsync
        // Rename with proper ID and final name
        string finalName = string.IsNullOrEmpty(newSubComponent.Name) ? template.Name : newSubComponent.Name;
        newSubComponent.gameObject.name = $"SubCompInst_{newSubComponent.SubComponentID}_{finalName}";

        LogInfo($"Successfully created and saved SubComponent Instance ID: {newSubComponent.SubComponentID} from Template ID: {template.ComponentTemplateID}");
        return newSubComponent;
    }

    #region InitializeLoading
    protected override async Task InitializeAsync()
    {
        // Clear runtime data first
        loadedTemplates.Clear();
        loadedItemInstances.Clear();
        templatesById.Clear();
        itemsById.Clear();
        subComponentTemplatesById.Clear();
        subComponentsById.Clear();

        try
        {
            // 1. Ensure Tables Exist
            await EnsureItemTablesExistAsync();

            // 2. Load Templates
            LogInfo("Loading Item Templates...");
            List<ItemTemplate> itemTemplates = await LoadAllItemTemplatesAsync();
            if (itemTemplates == null) throw new Exception("Failed to load Item Templates.");
            // Populate dictionary and list from returned data
            loadedTemplates = itemTemplates; // Store the list
            templatesById = itemTemplates.ToDictionary(t => t.ItemTemplateID, t => t);
            LogInfo($"Loaded and indexed {templatesById.Count} item templates.");

            LogInfo("Loading SubComponent Templates...");
            List<SubComponentTemplate> subCompTemplates = await LoadSubComponentTemplatesAsync(); // Get list
            if (subCompTemplates == null) throw new Exception("Failed to load SubComponent Templates.");
            // Populate dictionary from returned data
            subComponentTemplatesById = subCompTemplates.ToDictionary(t => t.ComponentTemplateID, t => t);
            LogInfo($"Loaded and indexed {subComponentTemplatesById.Count} sub-component templates.");

            // 3. Load Instances
            LogInfo("Loading Item Instances...");
            List<Item> itemInstances = await LoadAllItemInstancesAsync();
            if (itemInstances == null) throw new Exception("Failed to load Item Instances.");
            // Populate dictionary and list from returned data
            loadedItemInstances = itemInstances; // Store the list
            itemsById = itemInstances.ToDictionary(i => i.ItemID, i => i);
            LogInfo($"Loaded {itemsById.Count} item instances.");

            LogInfo("Loading SubComponent Instances...");
            List<SubComponent> subCompInstances = await LoadAllSubComponentsAsync(); // Get list
            if (subCompInstances == null) throw new Exception("Failed to load SubComponent Instances.");
            // Populate dictionary from returned data
            subComponentsById = subCompInstances.ToDictionary(i => i.SubComponentID, i => i);
            LogInfo($"Loaded {subComponentsById.Count} sub-component instances.");

            // 4. Link Instances to Templates
            LinkInstancesToTemplates();
            LinkSubComponentsToTemplates();
            LogInfo("Linked item instances and sub-component instances to templates.");

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
    private async Task<List<SubComponentTemplate>> LoadSubComponentTemplatesAsync()
    {
        List<SubComponentTemplate> loadedList = new List<SubComponentTemplate>(); // Create local list
        if (subComponentTemplatePrefab == null)
        {
            Debug.LogError("SubComponentTemplate Prefab is not assigned in ItemManager.");
            return loadedList; // Return empty list on error
        }

        string query = $"SELECT * FROM {SubComponentTemplatesTableName}";
        var results = await QueryDataAsync(query);

        if (results == null)
        {
            Debug.LogError("Failed to load sub-component templates from database.");
            return loadedList; // Return empty list on error
        }
         Debug.Log($"Loaded {results.Count} sub-component template rows from DB.");

        foreach (var row in results)
        {
            int templateID = SafeConvert.ToInt32(row, "ComponentTemplateID");
            string name = SafeConvert.ToString(row, "Name");
            int componentType = SafeConvert.ToInt32(row, "ComponentType");
            int icon = SafeConvert.ToInt32(row, "Icon");
            string colour = SafeConvert.ToString(row, "Colour");
            int weight = SafeConvert.ToInt32(row, "Weight");
            bool stackable = SafeConvert.ToBoolean(row, "Stackable");
            int stackSizeMax = SafeConvert.ToInt32(row, "StackSizeMax");
            int price = SafeConvert.ToInt32(row, "Price");

            SubComponentTemplate template = Instantiate(subComponentTemplatePrefab);

            if (template != null)
            {
                template.Initialize(templateID, name, componentType, icon, colour, weight, stackable, stackSizeMax, price);
                template.gameObject.name = $"SubComp_{templateID}_{name}";
                loadedList.Add(template); // Add to the local list
                // Removed: Populating dictionary and parenting here
            }
            else
            {
                Debug.LogError($"Failed to instantiate SubComponentTemplate component for ID {templateID}");
            }
        }
         Debug.Log($"Finished processing sub-component templates. Returning {loadedList.Count} templates.");
         return loadedList; // Return the populated list
    }
    private async Task<List<SubComponent>> LoadAllSubComponentsAsync()
    {
        List<SubComponent> loadedList = new List<SubComponent>(); // Create local list
        if (subComponentPrefab == null)
        {
            Debug.LogError("SubComponent Prefab is not assigned in ItemManager.");
            return loadedList; // Return empty list on error
        }

        string query = $"SELECT * FROM {SubComponentsTableName}";
        var results = await QueryDataAsync(query);

        if (results == null)
        {
            Debug.LogError("Failed to load sub-component instances from database.");
            return loadedList; // Return empty list on error
        }
        Debug.Log($"Loaded {results.Count} sub-component instance rows from DB.");

        foreach (var row in results)
        {
            int id = SafeConvert.ToInt32(row, "SubComponentID");
            string name = SafeConvert.ToString(row, "Name");
            int templateId = SafeConvert.ToInt32(row, "SubComponentTemplateID");
            int componentType = SafeConvert.ToInt32(row, "ComponentType");
            int quality = SafeConvert.ToInt32(row, "Quality");
            int toughness = SafeConvert.ToInt32(row, "Toughness");
            int strength = SafeConvert.ToInt32(row, "Strength");
            int density = SafeConvert.ToInt32(row, "Density");
            int aura = SafeConvert.ToInt32(row, "Aura");
            int energy = SafeConvert.ToInt32(row, "Energy");
            int protein = SafeConvert.ToInt32(row, "Protein");
            int carbohydrate = SafeConvert.ToInt32(row, "Carbohydrate");
            int flavour = SafeConvert.ToInt32(row, "Flavour");

            SubComponent instance = Instantiate(subComponentPrefab);

            if (instance != null)
            {
                instance.Initialize(id, name, templateId, componentType, quality, toughness, strength, density, aura, energy, protein, carbohydrate, flavour);
                instance.gameObject.name = $"SubCompInst_{id}_{name ?? "Unnamed"}";
                loadedList.Add(instance); // Add to the local list
                // Removed: Populating dictionary and parenting here
            }
            else
            {
                Debug.LogError($"Failed to instantiate SubComponent component for instance ID {id}");
            }
        }
        Debug.Log($"Finished processing sub-component instances. Returning {loadedList.Count} instances.");
        return loadedList; // Return the populated list
    }
    private void PerformPostLoadActions()
    {
        LogInfo("Performing post-load actions (parenting objects)...");
        // Parent Items
        foreach (Item item in loadedItemInstances) // Use the list
        {
            if (item != null && item.gameObject != null && itemInstancesParent != null && item.transform.parent == null)
            {
                item.transform.SetParent(itemInstancesParent, false);
            }
        }
        // Parent ItemTemplates
        foreach (ItemTemplate template in loadedTemplates) // Use the list
        {
            if (template != null && template.gameObject != null && itemTemplatesParent != null && template.transform.parent == null)
            {
                template.transform.SetParent(itemTemplatesParent, false);
            }
        }
        // Parent SubComponentTemplates
         foreach (var kvp in subComponentTemplatesById) // Iterate dictionary values
        {
            SubComponentTemplate template = kvp.Value;
            if (template != null && template.gameObject != null && subComponentTemplatesParent != null && template.transform.parent == null)
            {
                template.transform.SetParent(subComponentTemplatesParent, false);
            }
        }
        // Parent SubComponent Instances
         foreach (var kvp in subComponentsById) // Iterate dictionary values
        {
            SubComponent instance = kvp.Value;
             if (instance != null && instance.gameObject != null && subComponentsParent != null && instance.transform.parent == null)
            {
                instance.transform.SetParent(subComponentsParent, false);
            }
        }
        LogInfo("Post-load actions complete.");
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
             {"Bonus1", "INT DEFAULT 0"}, {"Bonus2", "INT DEFAULT 0"}, {"Bonus3", "INT DEFAULT 0"}, {"Bonus4", "INT DEFAULT 0"},
             {"Bonus5", "INT DEFAULT 0"}, {"Bonus6", "INT DEFAULT 0"}, {"Bonus7", "INT DEFAULT 0"}, {"Bonus8", "INT DEFAULT 0"},
             {"Bonus1Type", "INT DEFAULT 0"}, {"Bonus2Type", "INT DEFAULT 0"}, {"Bonus3Type", "INT DEFAULT 0"}, {"Bonus4Type", "INT DEFAULT 0"},
             {"Bonus5Type", "INT DEFAULT 0"}, {"Bonus6Type", "INT DEFAULT 0"}, {"Bonus7Type", "INT DEFAULT 0"}, {"Bonus8Type", "INT DEFAULT 0"},
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
             {"Bonus1", "INT NULL"}, {"Bonus2", "INT NULL"}, {"Bonus3", "INT NULL"}, {"Bonus4", "INT NULL"},
             {"Bonus5", "INT NULL"}, {"Bonus6", "INT NULL"}, {"Bonus7", "INT NULL"}, {"Bonus8", "INT NULL"},
             {"Bonus1Type", "INT DEFAULT 0"}, {"Bonus2Type", "INT DEFAULT 0"}, {"Bonus3Type", "INT DEFAULT 0"}, {"Bonus4Type", "INT DEFAULT 0"},
             {"Bonus5Type", "INT DEFAULT 0"}, {"Bonus6Type", "INT DEFAULT 0"}, {"Bonus7Type", "INT DEFAULT 0"}, {"Bonus8Type", "INT DEFAULT 0"},
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

    #region Getters
    public Item GetItemPrefab()
    { 
        return itemPrefab; 
    }
    public ItemTemplate GetItemTemplatePrefab()
    { 
        return itemTemplatePrefab; 
    }
    public SubComponentTemplate GetSubComponentTemplatePrefab()
    {
        return subComponentTemplatePrefab;
    }
    public SubComponent GetSubComponentPrefab()
    {
        return subComponentPrefab;
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
            // Use SafeConvert helper
            template.SetItemTemplate(
                SafeConvert.ToInt32(data, "ItemTemplateID"), 
                SafeConvert.ToString(data, "Name"),
                SafeConvert.ToInt32(data, "ItemType"),
                SafeConvert.ToString(data, "ExamineText"),
                SafeConvert.ToInt32(data, "MaxDurability", 100), 
                SafeConvert.ToSingle(data, "Damage"), 
                SafeConvert.ToSingle(data, "Speed", 1.0f),
                SafeConvert.ToInt32(data, "DamageType"),
                SafeConvert.ToInt32(data, "SlotType"),
                SafeConvert.ToSingle(data, "SlashResist"), SafeConvert.ToSingle(data, "ThrustResist"), SafeConvert.ToSingle(data, "CrushResist"),
                SafeConvert.ToSingle(data, "HeatResist"), SafeConvert.ToSingle(data, "ShockResist"), SafeConvert.ToSingle(data, "ColdResist"),
                SafeConvert.ToSingle(data, "MindResist"), SafeConvert.ToSingle(data, "CorruptResist"),
                SafeConvert.ToInt32(data, "Icon"), // Use ToInt32
                SafeConvert.ToString(data, "Colour", "#FFFFFF"),
                SafeConvert.ToInt32(data, "Weight"),
                SafeConvert.ToInt32(data, "Model"), // Use ToInt32
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
                SafeConvert.ToInt32(data, "MaxDurability", 100), 
                SafeConvert.ToSingle(data, "Damage"), 
                SafeConvert.ToSingle(data, "Speed", 1.0f),
                SafeConvert.ToInt32(data, "DamageType"),
                SafeConvert.ToInt32(data, "SlotType"),
                SafeConvert.ToSingle(data, "SlashResist"), SafeConvert.ToSingle(data, "ThrustResist"), SafeConvert.ToSingle(data, "CrushResist"),
                SafeConvert.ToSingle(data, "HeatResist"), SafeConvert.ToSingle(data, "ShockResist"), SafeConvert.ToSingle(data, "ColdResist"),
                SafeConvert.ToSingle(data, "MindResist"), SafeConvert.ToSingle(data, "CorruptResist"),
                SafeConvert.ToInt32(data, "Icon"), // Use ToInt32
                SafeConvert.ToString(data, "Colour", "#FFFFFF"),
                SafeConvert.ToInt32(data, "Weight"),
                SafeConvert.ToInt32(data, "Model"), // Use ToInt32
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
    private void LinkSubComponentsToTemplates()
    {
        foreach (var kvp in subComponentsById)
        {
            SubComponent instance = kvp.Value;
            if (subComponentTemplatesById.TryGetValue(instance.SubComponentTemplateID, out SubComponentTemplate template))
            {
                instance.Template = template;
                if (string.IsNullOrEmpty(instance.Name))
                {
                    // instance.Name = template.Name;
                }
            }
            else
            {
                LogWarning($"SubComponent instance ID {instance.SubComponentID} has missing template reference (TemplateID: {instance.SubComponentTemplateID}).");
            }
        }
        LogInfo("Linked sub-component instances to templates.");
    }
    protected override void OnDestroy()
    {
        OnDataLoaded -= PerformPostLoadActions;
        base.OnDestroy();
    }
    #endregion
}