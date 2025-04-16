using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{


    #region RandomNameVariables
    private System.Random randomNumber = new System.Random();
    private List<string> firstPartOfName = new List<string> { "Zor", "Vox", "Kel", "Thor", "Nix", "Jax", "Kael", "Valt", "Ron", "Pax", "Tek", "Mars", "Loki", "Zek", "Vor", "Rax", "Gorth", "Krom", "Vex", "Borg", "Kage", "Riven", "Soren", "Thane", "Onyx", "Zeal", "Kyro", "Valk", "Rune", "Slade", "Kane", "Thrak", "Voro", "Gorthok", "Kaid", "Zarek", "Vexx", "Ryk", "Kyel", "Thren", "Syg", "Rive", "Vrik", "Krag", "Throk", "Zekt", "Val", "Rag", "Kyr", "Valt" };
    private List<string> secondPartOfName = new List<string> { "da", "ri", "sa", "ta", "ki", "mi", "no", "la", "fa", "ga", "va", "zi", "mo", "lu", "be", "do", "pa", "su", "ku", "go", "dee", "re", "se", "tee", "kee", "mee", "na", "lee", "fee", "gee", "vee", "zee", "moe", "loo", "bee", "doe", "pea", "soo", "koo", "goo", "jee", "ree", "see", "tee", "lee", "nee", "gee", "vee", "zee", "moe", "ka" };
    private List<string> thirdPartOfName = new List<string> { "ton", "lee", "nex", "rus", "kus", "tus", "vus", "lys", "rex", "dus", "kas", "los", "tus", "vas", "gos", "nus", "vos", "kes", "das", "tus", "bane", "fade", "gore", "holt", "kane", "lane", "lyth", "mose", "naan", "pike", "rath", "roth", "ryn", "sank", "slade", "snod", "spar", "stadt", "stoke", "torn", "void", "wake", "wynn", "wyn", "xan", "yeld", "zell", "zorn", "zorv" };
    #endregion

    [SerializeField] private Transform resourcesParent;
    [SerializeField] private Transform resourceTemplatesParent;

    private const string ResourceSpawnsTableName = "ResourceSpawns";
    private const string ResourceTemplateTableName = "ResourceTemplate";

    [Header("Prefabs")]
    [SerializeField] private Resource resourcePrefab;
    [SerializeField] private ResourceTemplate resourceTemplatePrefab;

    [Header("Runtime Data (Loaded)")]
    private List<ResourceTemplate> resourceTemplates = new List<ResourceTemplate>();
    private List<Resource> spawnedResources = new List<Resource>();
    private Dictionary<int, ResourceTemplate> templatesById = new Dictionary<int, ResourceTemplate>();

    public bool isInitialized = false;
    private Task initializationTask;
    public event Action OnDataLoaded;

    #region Singleton
    public static ResourceManager Instance { get; private set; }
    private void Awake()
    {
        Instance = this;
    }
    #endregion

    private void Start()
    {
        InitializeResourceDataTablesIfNotExists();
        StartInitialization();
        OnDataLoaded += PerformPostLoadActions;
        //SetResourceTemplateIDs();
        /*
        foreach (Resource resource in spawnedResources)
        {
            resource.gameObject.transform.SetParent(resourcesParent);
        }
        foreach (ResourceTemplate template in resourceTemplates)
        {
            template.gameObject.transform.SetParent(resourceTemplatesParent);
        }
        */
    }
    private void OnDestroy()
    {
        // Unsubscribe from events
        OnDataLoaded -= PerformPostLoadActions;

        if (Instance == this)
        {
            Instance = null; // Clear singleton instance if this is the one being destroyed
        }
    }
    private void PerformPostLoadActions()
    {
        Debug.Log("Performing post-load actions (parenting objects)...");
        // Now it's safe to access the lists
        foreach (Resource resource in spawnedResources)
        {
            if (resource != null && resource.gameObject != null && resourcesParent != null)
            {
                resource.gameObject.transform.SetParent(resourcesParent, false); // Use worldPositionStays = false
            }
            else { Debug.LogWarning("Skipping parenting for null/destroyed resource or missing parent."); }
        }
        foreach (ResourceTemplate template in resourceTemplates)
        {
            if (template != null && template.gameObject != null && resourceTemplatesParent != null)
            {
                template.gameObject.transform.SetParent(resourceTemplatesParent, false); // Use worldPositionStays = false
            }
            else { Debug.LogWarning("Skipping parenting for null/destroyed template or missing parent."); }
        }
        Debug.Log("Post-load actions complete.");
    }

    public void StartInitialization()
    {
        // Prevent starting multiple initializations
        if (initializationTask == null || initializationTask.IsCompleted)
        {
            Debug.Log("Starting ResourceManager Initialization...");
            isInitialized = false; // Ensure marked as not initialized until task completes fully
            initializationTask = InitializeAsync();

            initializationTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.LogError($"ResourceManager Initialization Failed: {t.Exception}");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext()); // Use Unity's context for logging
        }
        else
        {
            Debug.LogWarning("ResourceManager initialization already in progress.");
        }
    }
    private void InitializeResourceDataTablesIfNotExists()
    {
        // Definition for ResourceSpawns Table
        Dictionary<string, string> spawnColumns = new Dictionary<string, string>
        {
            {"ResourceSpawnID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"ResourceName", "VARCHAR(255)"},
            {"ResourceTemplateID", "INT"},
            {"Type", "INT"},
            {"SubType", "INT"},
            {"Quality", "INT"},
            {"Toughness", "INT"},
            {"Strength", "INT"},
            {"Density", "INT"},
            {"Aura", "INT"},
            {"Energy", "INT"},
            {"Protein", "INT"},
            {"Carbohydrate", "INT"},
            {"Flavour", "INT"},
            {"Weight", "INT"},
            {"StartDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"},
            {"EndDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"}
        };

        if (!DatabaseManager.Instance.TableExists(ResourceSpawnsTableName))
        {
            bool tableCreated = DatabaseManager.Instance.CreateTableIfNotExists(ResourceSpawnsTableName, spawnColumns);
            Debug.Log($"{ResourceSpawnsTableName} table creation attempt result: {tableCreated}");
            if (!tableCreated) Debug.LogError($"Failed to create {ResourceSpawnsTableName} table.");
        }
        else
        {
            //Debug.Log($"{ResourceSpawnsTableName} table already exists.");
        }

        // Definition for ResourceTemplate Table
        Dictionary<string, string> templateColumns = new Dictionary<string, string>
        {
            {"ResourceTemplateID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"Order", "INT"},
            {"Family", "INT"},
            {"Type", "INT"},
            {"Quality", "INT"},
            {"Toughness", "INT"},
            {"Strength", "INT"},
            {"Density", "INT"},
            {"Aura", "INT"},
            {"Energy", "INT"},
            {"Protein", "INT"},
            {"Carbohydrate", "INT"},
            {"Flavour", "INT"},
            {"StackSizeMax", "INT DEFAULT 100"},
            {"Weight", "INT"}
        };

        if (!DatabaseManager.Instance.TableExists(ResourceTemplateTableName))
        {
            bool tableCreated = DatabaseManager.Instance.CreateTableIfNotExists(ResourceTemplateTableName, templateColumns);
            Debug.Log($"{ResourceTemplateTableName} table creation attempt result: {tableCreated}");
            if (!tableCreated) Debug.LogError($"Failed to create {ResourceTemplateTableName} table.");
        }
        else
        {
            //Debug.Log($"{ResourceTemplateTableName} table already exists.");
        }
    }
    private async Task InitializeAsync()
    {
        resourceTemplates.Clear();
        spawnedResources.Clear();
        templatesById.Clear();

        try
        {
            // 1. Ensure Database Tables Exist (Now Async)
            await InitializeResourceDataTablesIfNotExistsAsync(); // Wait for tables

            // 2. Load Templates Asynchronously
            Debug.Log("Loading Resource Templates...");
            List<ResourceTemplate> templates = await LoadAllResourceTemplatesAsync();
            if (templates == null) throw new Exception("Failed to load Resource Templates.");
            resourceTemplates = templates;
            templatesById = resourceTemplates.ToDictionary(t => t.ResourceTemplateID, t => t);
            Debug.Log($"Successfully loaded and indexed {resourceTemplates.Count} templates.");



            // 3. Load Spawned Resources Asynchronously
            Debug.Log("Loading Spawned Resources...");
            List<Resource> spawns = await LoadAllSpawnedResourcesAsync();
            if (spawns == null) // Check if loading failed
            {
                throw new Exception("Failed to load Spawned Resources.");
            }
            spawnedResources = spawns;

            // 4. Link Spawned Resources to Templates
            LinkSpawnedResourcesToTemplates(); // Moved to helper
            Debug.Log($"Successfully loaded and linked {spawnedResources.Count} spawned resources.");

            // 5. Mark as Initialized and Notify Listeners (on main thread)
            // Use TaskScheduler.FromCurrentSynchronizationContext() to ensure the final steps
            // run on the Unity main thread, which is safer for invoking events that might
            // trigger Unity API calls in subscribers (like the parenting logic).
            await Task.Factory.StartNew(() =>
            {
                isInitialized = true;
                Debug.Log("ResourceManager Initialization Complete. Invoking OnDataLoaded.");
                OnDataLoaded?.Invoke(); // Signal that data is ready
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

        }
        catch (Exception ex)
        {
            // Logging should happen on main thread for safety if possible
            await Task.Factory.StartNew(() =>
            {
                Debug.LogError($"ResourceManager Initialization Async Error: {ex.Message}\n{ex.StackTrace}");
                isInitialized = false;
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

        }
    }

    public async Task<List<Resource>> LoadAllSpawnedResourcesAsync()
    { 
        List<Resource> resources = new List<Resource>();
        string query = $"SELECT * FROM `{ResourceSpawnsTableName}`";
        Debug.Log($"Executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null) 
            { 
                Debug.LogError($"Query failed for '{ResourceSpawnsTableName}'."); 
                return null; 
            }
            if (results.Count > 0)
            {
                foreach (var rowData in results)
                {
                    Resource resource = Instantiate(resourcePrefab.gameObject).GetComponent<Resource>();
                    if (resource == null) 
                    { 
                        Debug.LogError("Failed to get Resource component.");
                        Destroy(resource.gameObject);
                        continue; 
                    }
                    MapDictionaryToResource(resource, rowData);
                    resources.Add(resource);
                }
                Debug.Log($"Loaded {resources.Count} spawned resources data rows.");
            }
            else 
            { 
                Debug.LogWarning($"No results for '{ResourceSpawnsTableName}'."); 
            }
            return resources;
        }
        catch (Exception ex) { Debug.LogError($"Error loading resources from '{ResourceSpawnsTableName}': {ex.Message}"); return null; }
    }
    public async Task<List<ResourceTemplate>> LoadAllResourceTemplatesAsync()
    {
        List<ResourceTemplate> templates = new List<ResourceTemplate>();
        string query = $"SELECT * FROM `{ResourceTemplateTableName}`";
        Debug.Log($"Executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);
            if (results == null)
            {
                Debug.LogError($"Query execution failed for table '{ResourceTemplateTableName}'.");
                return null;
            }
            if (results.Count == 0)
            {
                Debug.LogWarning($"Query returned no results for table '{ResourceTemplateTableName}'.");
                return templates;
            }

            foreach (var rowData in results)
            {             
                ResourceTemplate template = Instantiate(resourceTemplatePrefab).GetComponent<ResourceTemplate>();
                if (template == null)
                {
                    Debug.LogError("Failed to get ResourceTemplate component from instantiated prefab.");
                    Destroy(template.gameObject); // Clean up failed instance
                    continue;
                }
                MapDictionaryToResourceTemplate(template, rowData);
                templates.Add(template);
            }
            Debug.Log($"Loaded {templates.Count} resource templates data rows.");
            return templates;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading all resource templates from '{ResourceTemplateTableName}': {ex.Message}");
            return null; // Indicate failure
        }
    }

    private async Task InitializeResourceDataTablesIfNotExistsAsync()
    {
        Debug.Log("Checking and initializing resource data tables async...");
        bool spawnTableOK = await EnsureTableExistsAsync(ResourceSpawnsTableName, GetSpawnTableDefinition());
        bool templateTableOK = await EnsureTableExistsAsync(ResourceTemplateTableName, GetTemplateTableDefinition());

        if (!spawnTableOK || !templateTableOK)
        {
            throw new Exception("Failed to initialize required resource database tables async.");
        }
        Debug.Log("Resource data tables checked/initialized async.");
    }
    private Dictionary<string, string> GetSpawnTableDefinition()
    { /* ... same as before ... */
        return new Dictionary<string, string> {
            {"ResourceSpawnID", "INT AUTO_INCREMENT PRIMARY KEY"}, {"ResourceName", "VARCHAR(255)"},
            {"ResourceTemplateID", "INT"}, {"Type", "INT"}, {"SubType", "INT"}, {"Quality", "INT"},
            {"Toughness", "INT"}, {"Strength", "INT"}, {"Density", "INT"}, {"Aura", "INT"},
            {"Energy", "INT"}, {"Protein", "INT"}, {"Carbohydrate", "INT"}, {"Flavour", "INT"},
            {"Weight", "INT"}, {"StartDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"}, {"EndDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"}
        };
    }
    private Dictionary<string, string> GetTemplateTableDefinition()
    { /* ... same as before ... */
        return new Dictionary<string, string> {
            {"ResourceTemplateID", "INT AUTO_INCREMENT PRIMARY KEY"}, {"Order", "INT"}, {"Family", "INT"},
            {"Type", "INT"}, {"Quality", "INT"}, {"Toughness", "INT"}, {"Strength", "INT"},
            {"Density", "INT"}, {"Aura", "INT"}, {"Energy", "INT"}, {"Protein", "INT"},
            {"Carbohydrate", "INT"}, {"Flavour", "INT"}, {"StackSizeMax", "INT DEFAULT 100"}, {"Weight", "INT"}
         };
    }
    private async Task<bool> EnsureTableExistsAsync(string tableName, Dictionary<string, string> columns)
    {
        try
        {
            bool exists = await DatabaseManager.Instance.TableExistsAsync(tableName);
            if (!exists)
            {
                Debug.Log($"Table '{tableName}' does not exist. Attempting to create async...");
                bool created = await DatabaseManager.Instance.CreateTableIfNotExistsAsync(tableName, columns);
                if (created)
                {
                    Debug.Log($"Successfully created table '{tableName}' async.");
                    return true;
                }
                else
                {
                    Debug.LogError($"Failed to create table '{tableName}' async.");
                    return false;
                }
            }
            else
            {
                Debug.Log($"Table '{tableName}' already exists.");
                return true; // Table exists
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error checking/creating table async '{tableName}': {ex.Message}");
            return false;
        }
    }

    #region Saving
    public async Task<long> SaveSpawnedResourceAsync(Resource resource)
    {
        long newResourceId = -1; 
        if (resource == null) 
        { 
            Debug.LogError("Cannot save null resource."); 
            return -1; 
        }
        if (resource.ResourceTemplate == null) 
        { 
            Debug.LogError($"Resource '{resource.ResourceName}' has no associated template. Cannot save."); 
            return -1; 
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            { "ResourceName", resource.ResourceName },
            { "ResourceTemplateID", resource.ResourceTemplate.ResourceTemplateID },
            { "Type", (int)resource.Type},
            { "SubType", (int)resource.Subtype},
            { "Quality", resource.Quality },
            { "Toughness", resource.Toughness },
            { "Strength", resource.Strength },
            { "Density", resource.Density },
            { "Aura", resource.Aura },
            { "Energy", resource.Energy },
            { "Protein", resource.Protein },
            { "Carbohydrate", resource.Carbohydrate },
            { "Flavour", resource.Flavour },
            { "Weight", resource.Weight },
            { "StartDate", resource.StartDate },
            { "EndDate", resource.EndDate }
        };

        try
        {
            // Use InsertDataAsync directly
            bool success = await DatabaseManager.Instance.InsertDataAsync(ResourceSpawnsTableName, values);

            if (success)
            {
                // Use GetLastInsertIdAsync directly
                newResourceId = await DatabaseManager.Instance.GetLastInsertIdAsync();
                if (newResourceId > 0) // Check if ID is valid
                {
                    resource.ResourceSpawnID = (int)newResourceId;
                    Debug.Log($"Successfully saved resource '{resource.ResourceName}' with SpawnID: {newResourceId}");
                }
                else
                {
                    Debug.LogError($"Insert succeeded but failed to get last insert ID for resource '{resource.ResourceName}'.");
                    success = false; // Mark as failed if ID retrieval fails
                }
            }
            else
            {
                Debug.LogError($"Failed to insert resource into '{ResourceSpawnsTableName}'.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception saving resource to '{ResourceSpawnsTableName}': {ex.Message}");
            newResourceId = -1;
        }
        return newResourceId; // Returns the ResourceSpawnID
    }
    #endregion

    #region Spawn New Resource
    public async Task<Resource> SpawnResourceFromTemplateAsync(int templateId, ResourceSubType subType)
    {
        ResourceTemplate template = resourceTemplates.Find(t => t.ResourceTemplateID == templateId);
        if (template == null)
        {
            Debug.LogError($"Cannot spawn resource: Template ID {templateId} not found in resourceTemplates list.");
            return null;
        }
        Debug.Log($"Found template: {template.ResourceTemplateID}");
        return await SpawnResourceFromTemplateAsync(template, subType);
    }
    public async Task<Resource> SpawnResourceFromTemplateAsync(ResourceType resType, ResourceSubType subType)
    {
        ResourceTemplate template = resourceTemplates.Find(t => t.Type == resType);
        if (template == null)
        {
            Debug.LogError($"Cannot spawn resource: Template type {resType} not found in resourceTemplates list.");
            return null;
        }
        Debug.Log($"Found template: {template.ResourceTemplateID}");
        return await SpawnResourceFromTemplateAsync(template, subType);
    }
    public async Task<Resource> SpawnResourceFromTemplateAsync(ResourceTemplate template, ResourceSubType subType)
    {
        // 2. Create new Resource object
        Resource newResource = Instantiate(GetResourcePrefab(), resourcesParent).GetComponent<Resource>();

        // 3. Assign template
        newResource.ResourceTemplate = template;

        // 4. Generate Random Name
        newResource.ResourceName = GenerateRandomResourceName();

        // 5. Randomize Stats (+/- 50 from template, ensuring minimum of 1 and maximum of 1000)
        newResource.Quality = ClampNumber(template.Quality + randomNumber.Next(-100, 101));
        newResource.Toughness = ClampNumber(template.Toughness + randomNumber.Next(-50, 51));
        newResource.Strength = ClampNumber(template.Strength + randomNumber.Next(-50, 51));
        newResource.Density = ClampNumber(template.Density + randomNumber.Next(-50, 51));
        newResource.Aura = ClampNumber(template.Aura + randomNumber.Next(-50, 51));
        newResource.Energy = ClampNumber(template.Energy + randomNumber.Next(-50, 51));
        newResource.Protein = ClampNumber(template.Protein + randomNumber.Next(-50, 51));
        newResource.Carbohydrate = ClampNumber(template.Carbohydrate + randomNumber.Next(-50, 51));
        newResource.Flavour = ClampNumber(template.Flavour + randomNumber.Next(-50, 51));

        // 6. Set immutable stats
        newResource.Type = template.Type;
        newResource.Subtype = subType;
        newResource.Weight = template.Weight;
        newResource.StartDate = DateTime.UtcNow;
        newResource.EndDate = DateTime.UtcNow.AddHours(randomNumber.Next(120,216));

        // 7. Save the new resource instance to the ResourceSpawns table
        Debug.Log($"Saving newly generated resource: {newResource.ResourceName} with stats based on template {newResource.ResourceTemplate.ResourceTemplateID}");
        long newSpawnId = await SaveSpawnedResourceAsync(newResource);

        if (newSpawnId > 0)
        {
            Debug.Log($"Successfully saved spawned resource {newResource.ResourceName} with ResourceSpawnID: {newSpawnId}");
            newResource.ResourceSpawnID = (int)newSpawnId; // Set the ResourceSpawnID to the new ID from DB
            spawnedResources.Add(newResource);
            return newResource; // newResource object now contains the ResourceSpawnID in its ResourceID property
        }
        else
        {
            Debug.LogError($"Failed to save spawned resource based on template {newResource.ResourceTemplate.ResourceTemplateID}.");
            return null;
        }
    }
    #endregion

    #region Getters
    public Resource GetResourcePrefab()
    {
        return resourcePrefab;
    }
    public ResourceTemplate GetResourceTemplatePrefab()
    {
        return resourceTemplatePrefab;
    }
    public List<Resource> GetAllSpawnedResources()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Attempted to access spawned resources before ResourceManager is initialized.");
            return new List<Resource>(); // Return empty list
        }
        return spawnedResources; // Or return a copy
    }
    public List<ResourceTemplate> GetAllTemplates()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Attempted to access templates before ResourceManager is initialized.");
            return new List<ResourceTemplate>(); // Return empty list
        }
        return resourceTemplates; // Or return a copy: new List<ResourceTemplate>(loadedTemplates)
    }
    public Resource GetSpawnedResourceById(int spawnId)
    {
        if (!isInitialized) return null;
        // Use Linq Find (or create a dictionary if frequent lookups needed)
        return spawnedResources.Find(r => r.ResourceSpawnID == spawnId);
    }
    public ResourceTemplate GetTemplateById(int templateId)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Attempted to access template by ID before ResourceManager is initialized.");
            return null;
        }
        templatesById.TryGetValue(templateId, out ResourceTemplate template);
        return template; // Returns null if not found
    }

    #endregion

    #region Helpers
    private string GenerateRandomResourceName()
    {
        string firstPart = firstPartOfName[randomNumber.Next(firstPartOfName.Count)];
        string secondPart = secondPartOfName[randomNumber.Next(secondPartOfName.Count)];
        string thirdPart = thirdPartOfName[randomNumber.Next(secondPartOfName.Count)];
        string assembledName = $"{firstPart} {secondPart} {thirdPart}";
        foreach (Resource resource in spawnedResources)
        {
            if (resource.ResourceName == assembledName)
            {
                Debug.Log($"Duplicate name found: {assembledName}. Generating a new one.");
                return GenerateRandomResourceName(); // Recursively generate a new name
            }
        }

        return assembledName;
    }
    private int ClampNumber(int value, int min = 1, int max = 1000)
    {
        return Math.Max(min, Math.Min(max, value));
    }    
    private void MapDictionaryToResourceTemplate(ResourceTemplate resourceTemplate, Dictionary<string, object> data)
    {
        try
        {
            resourceTemplate.SetResourceTemplate(
                Convert.ToInt32(data["ResourceTemplateID"]),
                data["Order"] != DBNull.Value ? Convert.ToInt32(data["Order"]) : 0,
                data["Family"] != DBNull.Value ? Convert.ToInt32(data["Family"]) : 0,
                data["Type"] != DBNull.Value ? Convert.ToInt32(data["Type"]) : 0,
                data["Quality"] != DBNull.Value ? Convert.ToInt32(data["Quality"]) : 0,
                data["Toughness"] != DBNull.Value ? Convert.ToInt32(data["Toughness"]) : 0,
                data["Strength"] != DBNull.Value ? Convert.ToInt32(data["Strength"]) : 0,
                data["Density"] != DBNull.Value ? Convert.ToInt32(data["Density"]) : 0,
                data["Aura"] != DBNull.Value ? Convert.ToInt32(data["Aura"]) : 0,
                data["Energy"] != DBNull.Value ? Convert.ToInt32(data["Energy"]) : 0,
                data["Protein"] != DBNull.Value ? Convert.ToInt32(data["Protein"]) : 0,
                data["Carbohydrate"] != DBNull.Value ? Convert.ToInt32(data["Carbohydrate"]) : 0,
                data["Flavour"] != DBNull.Value ? Convert.ToInt32(data["Flavour"]) : 0,
                data["StackSizeMax"] != DBNull.Value ? Convert.ToInt32(data["StackSizeMax"]) : 1,
                data["Weight"] != DBNull.Value ? Convert.ToInt32(data["Weight"]) : 1
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error mapping dictionary to ResourceTemplate: {ex.Message} - Data: {string.Join(", ", data.Select(kv => kv.Key + "=" + kv.Value))}");
        }
    }
    private void MapDictionaryToResource(Resource resource, Dictionary<string, object> data)
    {
        int resourceSpawnId = -1;
        string resourceName = null;
        int templateId = -1;
        int type = -1;
        int subType = -1;
        int quality = 0;
        int toughness = 0;
        int strength = 0;
        int density = 0;
        int aura = 0;
        int energy = 0;
        int protein = 0;
        int carbohydrate = 0;
        int flavour = 0;
        int weight = 0;
        DateTime startDate = DateTime.MinValue;
        DateTime endDate = DateTime.MinValue;
        try
        {
            resourceSpawnId = data["ResourceSpawnID"] != DBNull.Value ? Convert.ToInt32(data["ResourceSpawnID"]) : -1;
            resourceName = data["ResourceName"] != DBNull.Value ? Convert.ToString(data["ResourceName"]) : null;
            templateId = data["ResourceTemplateID"] != DBNull.Value ? Convert.ToInt32(data["ResourceTemplateID"]) : -1;
            type = data["Type"] != DBNull.Value ? Convert.ToInt32(data["Type"]) : -1;
            subType = data["SubType"] != DBNull.Value ? Convert.ToInt32(data["SubType"]) : -1;
            quality = data["Quality"] != DBNull.Value ? Convert.ToInt32(data["Quality"]) : 0;
            toughness = data["Toughness"] != DBNull.Value ? Convert.ToInt32(data["Toughness"]) : 0;
            strength = data["Strength"] != DBNull.Value ? Convert.ToInt32(data["Strength"]) : 0;
            density = data["Density"] != DBNull.Value ? Convert.ToInt32(data["Density"]) : 0;
            aura = data["Aura"] != DBNull.Value ? Convert.ToInt32(data["Aura"]) : 0;
            energy = data["Energy"] != DBNull.Value ? Convert.ToInt32(data["Energy"]) : 0;
            protein = data["Protein"] != DBNull.Value ? Convert.ToInt32(data["Protein"]) : 0;
            carbohydrate = data["Carbohydrate"] != DBNull.Value ? Convert.ToInt32(data["Carbohydrate"]) : 0;
            flavour = data["Flavour"] != DBNull.Value ? Convert.ToInt32(data["Flavour"]) : 0;
            weight = data["Weight"] != DBNull.Value ? Convert.ToInt32(data["Weight"]) : 0;
            startDate = data["StartDate"] != DBNull.Value ? Convert.ToDateTime(data["StartDate"]) : DateTime.MinValue;
            endDate = data["EndDate"] != DBNull.Value ? Convert.ToDateTime(data["EndDate"]) : DateTime.MinValue;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error mapping dictionary to Resource: {ex.Message} - Data: {string.Join(", ", data.Select(kv => kv.Key + "=" + kv.Value))}");
        }
        resource.SetResource(
            resourceSpawnId,
            resourceName,
            templateId,
            type,
            subType,
            quality,
            toughness,
            strength,
            density,
            aura,
            energy,
            protein,
            carbohydrate,
            flavour,
            weight,
            startDate,
            endDate
        );
    }
    private void LinkSpawnedResourcesToTemplates()
    {
        foreach (var resource in spawnedResources)
        {
            // Ensure GetResourceTemplateID() exists on Resource class
            if (templatesById.TryGetValue(resource.GetResourceTemplateID(), out ResourceTemplate template))
            {
                resource.ResourceTemplate = template;
            }
            else
            {
                Debug.LogWarning($"Spawned resource '{resource.ResourceName}' (SpawnID: {resource.ResourceSpawnID}) has missing template reference (TemplateID: {resource.GetResourceTemplateID()}).");
            }
        }
    }
    #endregion

}
public enum ResourceType
{ 
    //Liquid
    SeaWater,
    RiverWater,
    LakeWater,
    BoodoxMilk,
    AurochMilk,
    AuyakMilk,
    AetisHoney,
    Tar,

    //Rock
    Basalt,
    Chalk,
    Clay,
    Coal,
    Flint,
    Gneiss,
    Granite,
    Limestone,
    Marble,
    Obsidian,
    Sandstone,
    Shale,
    Slate,

    //Metal
    Copper,
    Tin,
    Lead,
    Iron,
    Silver,
    Gold,
    Indicium,
    Moonium,
    Rubicite,
    Glitterstone,
    DarkIron,
    Faycite,
    Voidstone,

    //Gemstone
    Amethyst,
    Diamond,
    Emerald,
    Garnet,
    Onyx,
    Opal,
    Ruby,
    Sapphire,
    Topaz,
    Tourmaline,
    Malachite,
    Quartz,
    Jasper,
    Pearl,

    //Wood
    Rubywood,
    NightOak,
    WhiteBeech,
    SpiderPalm,
    Spearwood,
    Ironwood,
    StormOak,
    Tirque,
    Satinwood,
    Rosewood,
    Stonewood,
    IceBirch,
    DeepRoot,
    RiverHawthorn,
    PlainsYew,
    ShadowAsh,
    Treefern,
    DarkMangrove,
    JadeMangrove,
    SilverMangrove,
    DawnElm,
    RazerPine,
    GiantFir,
    SpinedPalm,
    SilkPalm,
    TundraPine,
    TkyanMushroom,

    //Creature
    Hide,
    Carapace,
    Scale,
    Bone,
    ReptileMeat,
    MammalMeat,
    BirdMeat,
    InsectMeat,
    FishMeat,
    CrabMeat,
    ExlixEgg,
    LeptonEgg,
    RikTelEgg,
    VexuEgg,

    //Plants

    //Herbs

    //Magical?
}
public enum ResourceSubType 
{
    //Region
    Ithoria,
    Aelystian,
    Qadian,
    Melivian,
    Getaii,
    Kasmiran,
    Anurian,
    Tkyan,
    Thalvakian,
    Valahoran,
    Hivernean,

    //Creature
    Nura,
    Boodox,
    Hog,
    Moa,
    Auroch,
    ShadowebSpider,
    Tigyn,
    Etrus,
    Exlix,
    Auyak,
    Ektle,
    Lepton,
    RikTel,
    WoodAetis,
    Crab,
    TreeWasp,
    ReaperSpider,
    DesertAetis,
    Stoneback,
    Vexu,
    Wurm,
    Shark
}
public enum ResourceOrder
{
    Liquid,
    Mineral,
    Animal,
    Plant,
}
public enum ResourceFamily
{
    Water,
    Stone,
    Metal,
    Gemstone,
    Wood,
    Hide,
    Carapace,
    Scale,
}