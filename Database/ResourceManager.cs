using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MySqlConnector;

public class ResourceManager : BaseManager
{
    #region RandomNameVariables
    private System.Random randomNumber = new System.Random();
    private List<string> firstPartOfName = new List<string> { "Zor", "Vox", "Kel", "Thor", "Nix", "Jax", "Kael", "Valt", "Ron", "Pax", "Tek", "Mars", "Loki", "Zek", "Vor", "Rax", "Gorth", "Krom", "Vex", "Borg", "Kage", "Riven", "Soren", "Thane", "Onyx", "Zeal", "Kyro", "Valk", "Rune", "Slade", "Kane", "Thrak", "Voro", "Gorthok", "Kaid", "Zarek", "Vexx", "Ryk", "Kyel", "Thren", "Syg", "Rive", "Vrik", "Krag", "Throk", "Zekt", "Val", "Rag", "Kyr", "Valt" };
    private List<string> secondPartOfName = new List<string> { "da", "ri", "sa", "ta", "ki", "mi", "no", "la", "fa", "ga", "va", "zi", "mo", "lu", "be", "do", "pa", "su", "ku", "go", "dee", "re", "se", "tee", "kee", "mee", "na", "lee", "fee", "gee", "vee", "zee", "moe", "loo", "bee", "doe", "pea", "soo", "koo", "goo", "jee", "ree", "see", "tee", "lee", "nee", "gee", "vee", "zee", "moe", "ka" };
    private List<string> thirdPartOfName = new List<string> { "ton", "lee", "nex", "rus", "kus", "tus", "vus", "lys", "rex", "dus", "kas", "los", "tus", "vas", "gos", "nus", "vos", "kes", "das", "tus", "bane", "fade", "gore", "holt", "kane", "lane", "lyth", "mose", "naan", "pike", "rath", "roth", "ryn", "sank", "slade", "snod", "spar", "stadt", "stoke", "torn", "void", "wake", "wynn", "wyn", "xan", "yeld", "zell", "zorn", "zorv" };
    #endregion

    // --- Table Names ---
    private const string ResourceTemplatesTableName = "ResourceTemplates";
    private const string ResourceInstancesTableName = "Resources";

    [Header("Prefabs")]
    [SerializeField] private Resource resourcePrefab;
    [SerializeField] private ResourceTemplate resourceTemplatePrefab;


    [Header("Parent Transforms (Optional)")]
    [SerializeField] private Transform resourceInstancesParent;
    [SerializeField] private Transform resourceTemplatesParent;

    [Header("Runtime Data (Loaded)")]
    private List<ResourceTemplate> loadedTemplates = new List<ResourceTemplate>();
    private List<Resource> loadedResourceInstances = new List<Resource>();
    private Dictionary<int, ResourceTemplate> templatesById = new Dictionary<int, ResourceTemplate>();
    private Dictionary<int, Resource> resourcesById = new Dictionary<int, Resource>();


    #region Singleton
    public static ResourceManager Instance;

    protected void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate ResourceManager detected. Destroying self.");
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    private void Start()
    {
        StartInitialization();
        OnDataLoaded += PerformPostLoadActions;
    }

    #region Initialization
    protected override async Task InitializeAsync()
    {
        loadedTemplates.Clear();
        loadedResourceInstances.Clear();
        templatesById.Clear();
        resourcesById.Clear();

        try
        {
            // 1. Ensure Tables Exist
            await EnsureResourceTablesExistAsync();

            // 2. Load Templates
            LogInfo("Loading Resource Templates...");
            List<ResourceTemplate> templates = await LoadAllResourceTemplatesAsync();
            if (templates == null) throw new Exception("Failed to load Resource Templates.");
            loadedTemplates = templates;
            templatesById = loadedTemplates.ToDictionary(t => t.ResourceTemplateID, t => t);
            LogInfo($"Loaded and indexed {loadedTemplates.Count} resource templates.");

            // 3. Load Instances
            LogInfo("Loading Resource Instances...");
            List<Resource> instances = await LoadAllResourceInstancesAsync();
            if (instances == null) throw new Exception("Failed to load Resource Instances.");
            loadedResourceInstances = instances;
            resourcesById = loadedResourceInstances.ToDictionary(i => i.ResourceSpawnID, i => i);
            LogInfo($"Loaded {loadedResourceInstances.Count} resource instances.");

            // 4. Link Instances to Templates
            LinkInstancesToTemplates();
            LogInfo("Linked resource instances to templates.");

            // 5. Mark as Initialized and Notify
            await Task.Factory.StartNew(() => {
                isInitialized = true;
                LogInfo("Initialization Complete. Invoking OnDataLoaded.");
                NotifyDataLoaded();
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception ex)
        {
            await Task.Factory.StartNew(() => {
                LogError("Initialization Async Error", ex);
                isInitialized = false;
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
    private async Task EnsureResourceTablesExistAsync()
    {
        LogInfo("Checking and initializing resource data tables async...");
        bool templateTableOK = await EnsureTableExistsAsync(ResourceTemplatesTableName, GetResourceTemplateTableDefinition());
        bool instanceTableOK = await EnsureTableExistsAsync(ResourceInstancesTableName, GetResourceTableDefinition());

        if (!templateTableOK || !instanceTableOK)
        {
            throw new Exception("Failed to initialize required resource database tables async.");
        }
        LogInfo("Resource data tables checked/initialized async.");
    }
    public async Task<List<Resource>> LoadAllResourceInstancesAsync()
    {
        List<Resource> resources = new List<Resource>();
        string query = $"SELECT * FROM `{ResourceInstancesTableName}`";
        Debug.Log($"Executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query);
            if (results == null)
            {
                Debug.LogError($"Query failed for '{ResourceInstancesTableName}'.");
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
                Debug.Log($"Loaded {resources.Count} resource instances data rows.");
            }
            else
            {
                Debug.LogWarning($"No results for '{ResourceInstancesTableName}'.");
            }
            return resources;
        }
        catch (Exception ex) { Debug.LogError($"Error loading resources from '{ResourceInstancesTableName}': {ex.Message}"); return null; }
    }
    public async Task<List<ResourceTemplate>> LoadAllResourceTemplatesAsync()
    {
        List<ResourceTemplate> templates = new List<ResourceTemplate>();
        string query = $"SELECT * FROM `{ResourceTemplatesTableName}`";
        Debug.Log($"Executing query: {query}");
        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query);
            if (results == null)
            {
                Debug.LogError($"Query execution failed for table '{ResourceTemplatesTableName}'.");
                return null;
            }
            if (results.Count == 0)
            {
                Debug.LogWarning($"Query returned no results for table '{ResourceTemplatesTableName}'.");
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
            Debug.LogError($"Error loading all resource templates from '{ResourceTemplatesTableName}': {ex.Message}");
            return null; // Indicate failure
        }
    }
    public async Task<Resource> SpawnResourceFromTemplateAsync(ResourceType type, ResourceSubType regionType)
    {
        if (!isInitialized)
        {
            LogError("ResourceManager not initialized. Cannot spawn resource.");
            return null;
        }

        // 1. Find Template
        ResourceTemplate template = loadedTemplates.FirstOrDefault(t => t.Type == type);

        if (template == null)
        {
            LogError($"No ResourceTemplate found for type '{type}'. Cannot spawn resource.");
            return null;
        }

        if (resourcePrefab == null)
        {
            LogError("Resource Prefab is not assigned! Cannot spawn resource.");
            return null;
        }

        // 2. Instantiate Prefab
        GameObject resourceGO = Instantiate(resourcePrefab.gameObject);
        Resource newResource = resourceGO.GetComponent<Resource>();
        if (newResource == null)
        {
            LogError("Failed to get Resource component from instantiated prefab. Destroying object.");
            Destroy(resourceGO);
            return null;
        }

        // 3. Parenting
        if (resourceInstancesParent != null)
        {
            resourceGO.transform.SetParent(resourceInstancesParent, false);
        }
        else
        {
            LogWarning("resourceInstancesParent is not set. Spawned resource will be at root.");
        }

        // 4. Generate Name and Set Initial Values
        string randomName = GenerateRandomResourceName();

        // 5. Add Random Variation
        int quality = ClampNumber(template.Quality + randomNumber.Next(-250, 251) + randomNumber.Next(-250, 251));
        int toughness = ClampNumber(template.Toughness + randomNumber.Next(-100, 101) + randomNumber.Next(-100, 101));
        int strength = ClampNumber(template.Strength + randomNumber.Next(-100, 101) + randomNumber.Next(-100, 101));
        int density = ClampNumber(template.Density + randomNumber.Next(-100, 101) + randomNumber.Next(-100, 101));
        int aura = ClampNumber(template.Aura + randomNumber.Next(-100, 101) + randomNumber.Next(-100, 101));
        int energy = ClampNumber(template.Energy + randomNumber.Next(-100, 101) + randomNumber.Next(-100, 101));
        int protein = ClampNumber(template.Protein + randomNumber.Next(-100, 101) + randomNumber.Next(-100, 101));
        int carbohydrate = ClampNumber(template.Carbohydrate + randomNumber.Next(-100, 101) + randomNumber.Next(-100, 101));
        int flavour = ClampNumber(template.Flavour + randomNumber.Next(-100, 101) + randomNumber.Next(-100, 101));
        int weight = template.Weight;
        int value = GenerateValue(template); 

        // 6. Set Dates
        DateTime startDate = DateTime.UtcNow;
        int randomHours = randomNumber.Next(120, 217);
        DateTime endDate = startDate.AddHours(randomHours);

        // 7. Save to Database using a transaction
        try
        {
            using (var connection = new MySqlConnection(DatabaseManager.Instance.GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // Prepare the insert command
                        Dictionary<string, object> valuesToSave = new Dictionary<string, object>
                        {
                            { "ResourceName", randomName },
                            { "ResourceTemplateID", template.ResourceTemplateID },
                            { "Type", (int)template.Type },
                            { "SubType", (int)regionType },
                            { "Quality", quality },
                            { "Toughness", toughness },
                            { "Strength", strength },
                            { "Density", density },
                            { "Aura", aura },
                            { "Energy", energy },
                            { "Protein", protein },
                            { "Carbohydrate", carbohydrate },
                            { "Flavour", flavour },
                            { "Weight", weight },
                            { "Value", value },
                            { "StartDate", startDate },
                            { "EndDate", endDate }
                        };

                        string columns = string.Join(", ", valuesToSave.Keys.Select(k => $"`{k}`"));
                        string parameters = string.Join(", ", valuesToSave.Keys.Select(k => $"@{k}"));
                        string query = $"INSERT INTO `{ResourceInstancesTableName}` ({columns}) VALUES ({parameters}); SELECT LAST_INSERT_ID();";

                        using (var cmd = new MySqlCommand(query, connection, transaction))
                        {
                            foreach (var kvp in valuesToSave)
                            {
                                cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
                            }

                            // Execute the command and get the ID
                            object result = await cmd.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                            {
                                long newId = Convert.ToInt64(result);
                                if (newId > 0)
                                {
                                    // Commit the transaction
                                    await transaction.CommitAsync();

                                    // Set the resource data
                                    newResource.SetResource(
                                        (int)newId,
                                        randomName,
                                        template.ResourceTemplateID,
                                        (int)template.Type,
                                        (int)regionType,
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
                                        value,
                                        startDate,
                                        endDate
                                    );
                                    newResource.SetResourceTemplate(template);
                                    loadedResourceInstances.Add(newResource);
                                    resourcesById[newResource.ResourceSpawnID] = newResource;
                                    LogInfo($"Spawned and saved new Resource '{randomName}' with ID: {newId} from template ID: {template.ResourceTemplateID}");
                                    return newResource;
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }

            LogError("Failed to get last insert ID after successful insert.");
            Destroy(resourceGO);
            return null;
        }
        catch (Exception ex)
        {
            // Log the FULL exception details, including the inner exception if present
            string fullErrorMessage = $"Exception during resource saving process. Destroying spawned object. Error: {ex.Message}\nType: {ex.GetType().FullName}\nStackTrace: {ex.StackTrace}";
            if (ex.InnerException != null)
            {
                fullErrorMessage += $"\nInner Exception: {ex.InnerException.Message}\nInner Type: {ex.InnerException.GetType().FullName}\nInner StackTrace: {ex.InnerException.StackTrace}";
            }
            LogError(fullErrorMessage); // Use LogError which might already log stack trace
            
            Destroy(resourceGO);
            return null;
        }
    }
    private void MapDictionaryToResourceTemplate(ResourceTemplate resourceTemplate, Dictionary<string, object> data)
    {
        try
        {
            resourceTemplate.SetResourceTemplate(
                Convert.ToInt32(data["ResourceTemplateID"]),
                data["TemplateName"] != DBNull.Value ? Convert.ToString(data["TemplateName"]) : "Unnamed Template",
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
                data["StackSizeMax"] != DBNull.Value ? Convert.ToInt32(data["StackSizeMax"]) : 100000,
                data["Weight"] != DBNull.Value ? Convert.ToInt32(data["Weight"]) : 0,
                data["Value"] != DBNull.Value ? Convert.ToInt32(data["Value"]) : 0
            );
        }
        catch (Exception ex)
        {
            LogError($"Error mapping dictionary to ResourceTemplate: {ex.Message} - Data: {string.Join(", ", data.Select(kv => kv.Key + "=" + kv.Value))}");
        }
    }
    private void MapDictionaryToResource(Resource resource, Dictionary<string, object> data)
    {
        try
        {
            resource.SetResource(
                data["ResourceSpawnID"] != DBNull.Value ? Convert.ToInt32(data["ResourceSpawnID"]) : -1,
                data["ResourceName"] != DBNull.Value ? Convert.ToString(data["ResourceName"]) : "",
                data["ResourceTemplateID"] != DBNull.Value ? Convert.ToInt32(data["ResourceTemplateID"]) : -1,
                data["Type"] != DBNull.Value ? Convert.ToInt32(data["Type"]) : 0,
                data["SubType"] != DBNull.Value ? Convert.ToInt32(data["SubType"]) : 0,
                data["Quality"] != DBNull.Value ? Convert.ToInt32(data["Quality"]) : 0,
                data["Toughness"] != DBNull.Value ? Convert.ToInt32(data["Toughness"]) : 0,
                data["Strength"] != DBNull.Value ? Convert.ToInt32(data["Strength"]) : 0,
                data["Density"] != DBNull.Value ? Convert.ToInt32(data["Density"]) : 0,
                data["Aura"] != DBNull.Value ? Convert.ToInt32(data["Aura"]) : 0,
                data["Energy"] != DBNull.Value ? Convert.ToInt32(data["Energy"]) : 0,
                data["Protein"] != DBNull.Value ? Convert.ToInt32(data["Protein"]) : 0,
                data["Carbohydrate"] != DBNull.Value ? Convert.ToInt32(data["Carbohydrate"]) : 0,
                data["Flavour"] != DBNull.Value ? Convert.ToInt32(data["Flavour"]) : 0,
                data["Weight"] != DBNull.Value ? Convert.ToInt32(data["Weight"]) : 0,
                data["Value"] != DBNull.Value ? Convert.ToInt32(data["Value"]) : 0,
                data["StartDate"] != DBNull.Value ? Convert.ToDateTime(data["StartDate"]) : DateTime.MinValue,
                data["EndDate"] != DBNull.Value ? Convert.ToDateTime(data["EndDate"]) : DateTime.MinValue
            );
        }
        catch (Exception ex)
        {
            LogError($"Error mapping dictionary to Resource: {ex.Message} - Data: {string.Join(", ", data.Select(kv => kv.Key + "=" + kv.Value))}");
        }
    }
    private void PerformPostLoadActions()
    {
        LogInfo("Performing post-load actions (parenting objects)...");
        foreach (Resource resource in loadedResourceInstances)
        {
            if (resource != null && resource.gameObject != null && resourceInstancesParent != null)
            {
                resource.gameObject.transform.SetParent(resourceInstancesParent, false);
            }
            else { LogWarning("Skipping parenting for null/destroyed resource or missing parent."); }
        }
        foreach (ResourceTemplate template in loadedTemplates)
        {
            if (template != null && template.gameObject != null && resourceTemplatesParent != null)
            {
                template.gameObject.transform.SetParent(resourceTemplatesParent, false);
            }
            else { LogWarning("Skipping parenting for null/destroyed template or missing parent."); }
        }
        LogInfo("Post-load actions complete.");
    }
    #endregion

    #region Getters
    private Dictionary<string, string> GetResourceTableDefinition()
    {
        return new Dictionary<string, string> {
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
            {"Value", "INT"},
            {"StartDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"},
            {"EndDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"}
        };
    }
    private Dictionary<string, string> GetResourceTemplateTableDefinition()
    {
        return new Dictionary<string, string> {
            {"ResourceTemplateID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"TemplateName", "VARCHAR(255)"},
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
            {"StackSizeMax", "INT DEFAULT 100000"},
            {"Weight", "INT"},
            {"Value", "INT"}
        };
    }
    public Resource GetResourcePrefab()
    {
        return resourcePrefab;
    }
    public ResourceTemplate GetResourceTemplatePrefab()
    {
        return resourceTemplatePrefab;
    }
    public List<Resource> GetAllResourceInstances()
    {
        if (!isInitialized)
        {
            LogWarning("Attempted to access resource instances before ResourceManager is initialized.");
            return new List<Resource>(); // Return empty list
        }
        return loadedResourceInstances; // Or return a copy
    }
    public List<ResourceTemplate> GetAllResourceTemplates()
    {
        if (!isInitialized)
        {
            LogWarning("Attempted to access templates before ResourceManager is initialized.");
            return new List<ResourceTemplate>(); // Return empty list
        }
        return loadedTemplates; // Or return a copy: new List<ResourceTemplate>(loadedTemplates)
    }
    public Resource GetResourceInstanceById(int resourceID)
    {
        if (!isInitialized) return null;
        resourcesById.TryGetValue(resourceID, out Resource resource);
        return resource; // Returns null if not found
    }
    public ResourceTemplate GetResourceTemplateById(int templateID)
    {
        if (!isInitialized)
        {
            LogWarning("Attempted to access template by ID before ResourceManager is initialized.");
            return null;
        }
        templatesById.TryGetValue(templateID, out ResourceTemplate template);
        return template; // Returns null if not found
    }
    #endregion

    #region Helpers

    private int GenerateValue(ResourceTemplate resourceTemplate)
    {
        int prelimValue = resourceTemplate.Value;
        float multiplier = 1;
        if (resourceTemplate.Type == ResourceType.Coal)
        {
            multiplier = resourceTemplate.Quality + resourceTemplate.Toughness + resourceTemplate.Strength + resourceTemplate.Density + resourceTemplate.Energy;
        }
        else if (resourceTemplate.Family == ResourceFamily.Stone || resourceTemplate.Family == ResourceFamily.Metal || 
            resourceTemplate.Family == ResourceFamily.Gemstone || resourceTemplate.Family == ResourceFamily.Wood || 
            resourceTemplate.Family == ResourceFamily.Hide || resourceTemplate.Family == ResourceFamily.Carapace || 
            resourceTemplate.Family == ResourceFamily.Scale)
        {
            multiplier = resourceTemplate.Quality + resourceTemplate.Toughness + resourceTemplate.Strength + resourceTemplate.Density + resourceTemplate.Aura;
        }
        else if (resourceTemplate.Family == ResourceFamily.Meat)
        {
            multiplier = (float)(resourceTemplate.Quality + ((resourceTemplate.Energy + resourceTemplate.Protein + resourceTemplate.Carbohydrate + resourceTemplate.Flavour + resourceTemplate.Aura)*0.8f));
        }
        float value = prelimValue * (multiplier/1000);

        return (int)value;
    }
    private string GenerateRandomResourceName()
    {
        string firstPart = firstPartOfName[randomNumber.Next(firstPartOfName.Count)];
        string secondPart = secondPartOfName[randomNumber.Next(secondPartOfName.Count)];
        string thirdPart = thirdPartOfName[randomNumber.Next(thirdPartOfName.Count)];
        string assembledName = $"{firstPart}{secondPart}{thirdPart}";
        foreach (Resource resource in loadedResourceInstances)
        {
            if (resource.ResourceName == assembledName)
            {
                LogWarning($"Duplicate name found: {assembledName}. Generating a new one.");
                return GenerateRandomResourceName(); // Recursively generate a new name
            }
        }

        return assembledName;
    }
    private int ClampNumber(int value, int min = 1, int max = 1000)
    {
        return Math.Max(min, Math.Min(max, value));
    }
    private void LinkInstancesToTemplates()
    {
        foreach (var resource in loadedResourceInstances)
        {
            if (templatesById.TryGetValue(resource.GetResourceTemplateID(), out ResourceTemplate template))
            {
                resource.SetResourceTemplate(template);
            }
            else
            {
                LogWarning($"Resource instance '{resource.ResourceName}' (ID: {resource.ResourceSpawnID}) has missing template reference (TemplateID: {resource.GetResourceTemplateID()}).");
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
    SilkyHide,
    LeatheryHide,
    ThickHide,
    WoolyHide,
    ShinyCarapace,
    RigidCarapace,
    HeavyCarapace,
    ShinyScale,
    RigidScale,
    HeavyScale,
    LightBone,
    RigidBone,
    HeavyBone,

    Bone,
    ReptileMeat,
    MammalMeat,
    BirdMeat,
    InsectMeat,
    FishMeat,
    CrabMeat,
    ReptileEgg,
    BirdEgg,
    FishEgg,
    BirdFeathers,
    ReptileFeathers,
    AsiCrystal,
    OriCrystal,
    DiotCrystal,
    EseCrystal,
    CesiCrystal,
    FiyCrystal,
    LeiCrystal,
    XeCrystal,
    GhostRock,
    Silkweed,
    Redwort,
    Laceflower,
    Aldernettle,
    Windroot,
    Ribwort,
    Feverbloom,
    Hellebloom,
    Deathnettle,
    Lifebloom,
    Woundwort,
    Devilrose,
    Trishade,
    Finslip,
    Gutweed,
    Kuflower,
    HeartPepper,
    FaeLilly,
    Krateweed,
    HedgeMoss,
    DsihMoss,
    Silvermoss,
    Cottonmoss,
    Elocress,
    ThjiMoss,
    ReshMoss,
    WurmMoss,
    Bael,
    Akleme,
    Slathi,
    Riyk,
    Eatil,
    Ariac,
    Jipulse,
    CaveOat,
    Saltweed,
    SpinePalmNeedle,
    Azoana,
    Hobola,
    Etuce,
    Ciove,
    Khelish,
    Iachini,
    Weparrot,
    DesertSquash,
    CherryPepper,
    Clichoy,
    Strulene,
    Klute,
    Glasan,
    RedGuan,
    TammaniPepper,
    AnurianLeaf,
    Blayrang,
    WeepingRedcap,
    VoidMushroom,
    WoundCap,
    SorrowMushroom,
    GhostbaneMushroom,
    MithliMushroom,
    BlackButton,
    Goldcap,
    Gjincap,
    FeltLichen,
    TravelBerry,
    TundraApple,
    Boqila,
    Niamia,
    Moonfruit,
    Riaya,
    Icefruit,
    DewCherry,
    RainPlum,
    HoneyApple,
    Ulery,
    BronzeBerry,
    FadeGrape,
    OceanApple,
    Asnip,
    HoneyCactus,
    Hurnut,
    Nitech,
    Ucan,
    XaNut,
    Yumbi
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
    Meat,
    Egg,
    Feather,
    Bone,
    Silk,
    Leaf,
}