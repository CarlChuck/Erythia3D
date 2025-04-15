using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    // Singleton instance
    public static ResourceManager Instance { get; private set; }

    [SerializeField] private ResourceDatabaseManager resourceDbManager;

    #region RandomNameVariables
    private System.Random randomNumber = new System.Random();
    private List<string> firstPartOfName = new List<string> { "Zor", "Vox", "Kel", "Thor", "Nix", "Jax", "Kael", "Valt", "Ron", "Pax", "Tek", "Mars", "Loki", "Zek", "Vor", "Rax", "Gorth", "Krom", "Vex", "Borg", "Kage", "Riven", "Soren", "Thane", "Onyx", "Zeal", "Kyro", "Valk", "Rune", "Slade", "Kane", "Thrak", "Voro", "Gorthok", "Kaid", "Zarek", "Vexx", "Ryk", "Kyel", "Thren", "Syg", "Rive", "Vrik", "Krag", "Throk", "Zekt", "Val", "Rag", "Kyr", "Valt" };
    private List<string> secondPartOfName = new List<string> { "da", "ri", "sa", "ta", "ki", "mi", "no", "la", "fa", "ga", "va", "zi", "mo", "lu", "be", "do", "pa", "su", "ku", "go", "dee", "re", "se", "tee", "kee", "mee", "na", "lee", "fee", "gee", "vee", "zee", "moe", "loo", "bee", "doe", "pea", "soo", "koo", "goo", "jee", "ree", "see", "tee", "lee", "nee", "gee", "vee", "zee", "moe", "ka" };
    private List<string> thirdPartOfName = new List<string> { "ton", "lee", "nex", "rus", "kus", "tus", "vus", "lys", "rex", "dus", "kas", "los", "tus", "vas", "gos", "nus", "vos", "kes", "das", "tus", "bane", "fade", "gore", "holt", "kane", "lane", "lyth", "mose", "naan", "pike", "rath", "roth", "ryn", "sank", "slade", "snod", "spar", "stadt", "stoke", "torn", "void", "wake", "wynn", "wyn", "xan", "yeld", "zell", "zorn", "zorv" };
    #endregion

    private List<ResourceTemplate> resourceTemplates = new List<ResourceTemplate>();
    private List<Resource> spawnedResources = new List<Resource>();
    private Transform resourcesParent;
    private Transform resourceTemplatesParent;
    private void Awake()
    {
        // Singleton implementation
        Instance = this;
    }
    private async void Start()
    {
        Debug.Log("Initializing ResourceManager...");

        // Load all resource templates
        resourceTemplates = await LoadAllResourceTemplatesAsync();
        Debug.Log($"Resource templates loaded: {resourceTemplates.Count}");

        // Load all spawned resources
        spawnedResources = await LoadAllSpawnedResourcesAsync();
        Debug.Log($"Spawned resources loaded: {spawnedResources.Count}");

        // Set ResourceTemplateID for all spawned resources
        SetResourceTemplateIDs();
        foreach (Resource resource in spawnedResources)
        {
            resource.gameObject.transform.SetParent(resourcesParent);
        }
        foreach (ResourceTemplate template in resourceTemplates)
        {
            template.gameObject.transform.SetParent(resourceTemplatesParent);
        }
    }

    #region Load Resources and Templates
    public async Task<List<ResourceTemplate>> LoadAllResourceTemplatesAsync()
    {
        resourceTemplates.Clear(); // Clear the list before loading new templates
        Debug.Log("Loading all resource templates...");
        resourceTemplates = await resourceDbManager.LoadAllResourceTemplatesAsync();
        Debug.Log($"Loaded {resourceTemplates.Count} resource templates.");
        return resourceTemplates;
    }
    public async Task<List<Resource>> LoadAllSpawnedResourcesAsync()
    {
        spawnedResources.Clear(); // Clear the list before loading new resources
        Debug.Log("Loading all spawned resources...");
        spawnedResources = await resourceDbManager.LoadAllSpawnedResourcesAsync();
        Debug.Log($"Loaded {spawnedResources.Count} spawned resources.");
        return spawnedResources;
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
        Resource newResource = Instantiate(resourceDbManager.GetResourcePrefab(), resourcesParent).GetComponent<Resource>();

        // 3. Assign template
        newResource.ResourceTemplate = template;

        // 4. Generate Random Name
        newResource.ResourceName = GenerateRandomResourceName();

        // 5. Randomize Stats (+/- 50 from template, ensuring minimum of 1 and maximum of 1000)
        newResource.Quality = ClampNumber(template.Quality + randomNumber.Next(-50, 51));
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
        long newSpawnId = await resourceDbManager.SaveSpawnedResourceAsync(newResource);

        if (newSpawnId > 0)
        {
            Debug.Log($"Successfully saved spawned resource {newResource.ResourceName} with ResourceSpawnID: {newSpawnId}");
            newResource.ResourceSpawnID = (int)newSpawnId; // Set the ResourceSpawnID to the new ID from DB
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
    public ResourceTemplate GetResourceTemplateByID(int resourceTemplateID)
    {
        ResourceTemplate template = resourceTemplates.Find(t => t.ResourceTemplateID == resourceTemplateID);
        if (template == null)
        {
            Debug.LogError($"Cannot find resource template: Template ID {resourceTemplateID} not found in resourceTemplates list.");
            return null;
        }
        Debug.Log($"Found template: {template.ResourceTemplateID}");
        return template;
    }
    public Resource GetResourceByID(int resourceID)
    {
        Resource resource = spawnedResources.Find(t => t.ResourceSpawnID == resourceID);
        if (resource == null)
        {
            Debug.LogError($"Cannot find resource: Resource ID {resourceID} not found in spawnedResources list.");
            return null;
        }
        Debug.Log($"Found resource: {resource.ResourceSpawnID}");
        return resource;
    }
    public List<ResourceTemplate> GetAllResourceTemplates()
    {
        return resourceTemplates;
    }
    public List<Resource> GetAllSpawnedResources()
    {
        return spawnedResources;
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
    private void SetResourceTemplateIDs()
    {
        foreach (Resource resource in spawnedResources)
        {
            // Find the matching template by ID
            ResourceTemplate template = GetResourceTemplateByID(resource.GetResourceTemplateID());
            if (template != null)
            {
                resource.SetResourceTemplateID(template);
                Debug.Log($"Set ResourceTemplateID for Resource: {resource.ResourceName} to Template: {template.ResourceTemplateID}");
            }
            else
            {
                Debug.LogWarning($"No matching template found for Resource: {resource.ResourceName} with TemplateID: {resource.GetResourceTemplateID()}");
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