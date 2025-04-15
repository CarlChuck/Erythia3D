using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;


public class ResourceDatabaseManager : MonoBehaviour
{
    // Table names used by the initialization logic
    private const string ResourceSpawnsTableName = "ResourceSpawns";
    private const string ResourceTemplateTableName = "ResourceTemplate";

    [SerializeField] private GameObject resourcePrefab;
    [SerializeField] private GameObject resourceTemplatePrefab;

    public void Start()
    {
        InitializeResourceDataTablesIfNotExists();
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
            Debug.Log($"{ResourceSpawnsTableName} table already exists.");
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
            Debug.Log($"{ResourceTemplateTableName} table already exists.");
        }
    }

    #region Spawned Resources
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
    public async Task<List<Resource>> LoadAllSpawnedResourcesAsync()
    {
        List<Resource> resources = new List<Resource>();
        string query = $"SELECT * FROM `{ResourceSpawnsTableName}`";

        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);

            if (results != null)
            {
                foreach (var rowData in results)
                {
                    Resource resource = Instantiate(resourcePrefab).GetComponent<Resource>(); // Instantiate a new Resource object
                    MapDictionaryToResource(resource, rowData);
                    resources.Add(resource);                    
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading all resources from '{ResourceSpawnsTableName}': {ex.Message}");
        }
        return resources;
    }

    // Save a new spawned resource instance to the ResourceSpawns table.
    public async Task<long> SaveSpawnedResourceAsync(Resource resource)
    {
        long newResourceId = -1; // This will be the ResourceSpawnID

        // Match columns in ResourceSpawns table
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
            bool success = await Task.Run(() => DatabaseManager.Instance.InsertData(ResourceSpawnsTableName, values));

            if (success)
            {
                newResourceId = await Task.Run(() => DatabaseManager.Instance.GetLastInsertId());
                resource.ResourceSpawnID = (int)newResourceId; // Update the object's ResourceID (which represents ResourceSpawnID)
            }
            else
            {
                Debug.LogError($"Failed to insert resource into '{ResourceSpawnsTableName}'. Check DatabaseManager logs.");
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

    #region Resource Template
    // Helper to convert a Dictionary<string, object> to a ResourceTemplate object
    private void MapDictionaryToResourceTemplate(ResourceTemplate resourceTemplate,Dictionary<string, object> data)
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

    // Load all resource templates from the ResourceTemplate table.
    public async Task<List<ResourceTemplate>> LoadAllResourceTemplatesAsync()
    {
        List<ResourceTemplate> templates = new List<ResourceTemplate>();
        string query = $"SELECT * FROM `{ResourceTemplateTableName}`";

        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query);

            if (results != null)
            {
                foreach (var rowData in results)
                {
                    ResourceTemplate template = Instantiate(resourceTemplatePrefab).GetComponent<ResourceTemplate>(); // Instantiate a new ResourceTemplate object
                    MapDictionaryToResourceTemplate(template, rowData);
                    templates.Add(template);
                }
            }
            Debug.Log($"Loaded {templates.Count} resource templates.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading all resource templates from '{ResourceTemplateTableName}': {ex.Message}");
        }
        return templates;
    }

    // Load a specific resource template by its ID.
    public async Task<ResourceTemplate> LoadResourceTemplateByIdAsync(int templateId)
    {
        ResourceTemplate template = null;
        string query = $"SELECT * FROM `{ResourceTemplateTableName}` WHERE `ResourceTemplateID` = @id LIMIT 1";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@id", templateId } };

        try
        {
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query, parameters);

            if (results != null && results.Count > 0)
            {
                template = Instantiate(resourceTemplatePrefab).GetComponent<ResourceTemplate>(); // Instantiate a new ResourceTemplate object
                MapDictionaryToResourceTemplate(template, results[0]);
            }
            else
            {
                Debug.LogWarning($"Resource template with ID {templateId} not found.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading resource template ID {templateId} from '{ResourceTemplateTableName}': {ex.Message}");
        }
        return template;
    }

    // Save a new resource template to the ResourceTemplate table.
    public async Task<long> SaveResourceTemplateAsync(ResourceTemplate template)
    {
        long newTemplateId = -1;

        // Match columns in ResourceTemplate table
        Dictionary<string, object> values = new Dictionary<string, object>
        {
            { "Order", template.Order },
            { "Family", template.Family },
            { "Type", template.Type },
            { "Quality", template.Quality },
            { "Toughness", template.Toughness },
            { "Strength", template.Strength },
            { "Density", template.Density },
            { "Aura", template.Aura },
            { "Energy", template.Energy },
            { "Protein", template.Protein },
            { "Carbohydrate", template.Carbohydrate },
            { "Flavour", template.Flavour },
            { "StackSizeMax", template.StackSizeMax },
            { "Weight", template.Weight }
        };

        try
        {
            bool success = await Task.Run(() => DatabaseManager.Instance.InsertData(ResourceTemplateTableName, values));

            if (success)
            {
                newTemplateId = await Task.Run(() => DatabaseManager.Instance.GetLastInsertId());
                template.SetResourceTemplateID((int)newTemplateId); // Update the object's ID
                Debug.Log($"Successfully saved resource template '{template.ResourceTemplateID}' with new ID: {newTemplateId}");
            }
            else
            {
                Debug.LogError($"Failed to insert resource template into '{ResourceTemplateTableName}'. Check DatabaseManager logs.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception saving resource template '{template.ResourceTemplateID}' to '{ResourceTemplateTableName}': {ex.Message}");
            newTemplateId = -1;
        }
        return newTemplateId;
    }
    #endregion

    public GameObject GetResourcePrefab()
    {
        return resourcePrefab;
    }
    public GameObject GetResourceTemplatePrefab()
    {
        return resourceTemplatePrefab;
    }
}
