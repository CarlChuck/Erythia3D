using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CraftingManager : BaseManager
{
    [Header("Prefabs")]
    [SerializeField] private GameObject recipePrefab;

    [Header("Runtime Data")]
    private Dictionary<int, Recipe> recipesById = new Dictionary<int, Recipe>();
    private Dictionary<string, Recipe> recipesByName = new Dictionary<string, Recipe>();

    private const string RecipesTableName = "Recipes";

    #region Singleton
    public static CraftingManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Multiple CraftingManager instances detected. Destroying the new one.");
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    #region Initialize
    private void Start()
    {
        StartInitialization();
    }
    protected override async Task InitializeAsync()
    {
        Debug.Log("Initializing CraftingManager...");
        
        // Define the Recipes table structure
        var recipeColumns = new Dictionary<string, string>
        {
            { "RecipeID", "INT AUTO_INCREMENT PRIMARY KEY" },
            { "RecipeName", "VARCHAR(255)" },
            { "RecipeDescription", "TEXT" },
            { "RecipeType", "INT" },

            // Resource 1
            { "Resource1", "INT" },
            { "Resource1Amount", "INT" },
            { "Resource1TypeLevel", "INT" },
            { "Resource1Stat1", "INT" },
            { "Resource1Stat1Distribution", "INT" },
            { "Resource1Stat2", "INT" },
            { "Resource1Stat2Distribution", "INT" },

            // Resource 2
            { "Resource2", "INT" },
            { "Resource2Amount", "INT" },
            { "Resource2TypeLevel", "INT" },
            { "Resource2Stat1", "INT" },
            { "Resource2Stat1Distribution", "INT" },
            { "Resource2Stat2", "INT" },
            { "Resource2Stat2Distribution", "INT" },

            // Resource 3
            { "Resource3", "INT" },
            { "Resource3Amount", "INT" },
            { "Resource3TypeLevel", "INT" },
            { "Resource3Stat1", "INT" },
            { "Resource3Stat1Distribution", "INT" },
            { "Resource3Stat2", "INT" },
            { "Resource3Stat2Distribution", "INT" },

            // Resource 4
            { "Resource4", "INT" },
            { "Resource4Amount", "INT" },
            { "Resource4TypeLevel", "INT" },
            { "Resource4Stat1", "INT" },
            { "Resource4Stat1Distribution", "INT" },
            { "Resource4Stat2", "INT" },
            { "Resource4Stat2Distribution", "INT" },

            // Required Item 1
            { "Item1", "INT" },
            { "Item1Amount", "INT" },
            { "Item1Stat1", "INT" },
            { "Item1Stat1Distribution", "INT" },
            { "Item1Stat2", "INT" },
            { "Item1Stat2Distribution", "INT" },

            // Required Item 2
            { "Item2", "INT" },
            { "Item2Amount", "INT" },
            { "Item2Stat1", "INT" },
            { "Item2Stat1Distribution", "INT" },
            { "Item2Stat2", "INT" },
            { "Item2Stat2Distribution", "INT" },

            // Required Item 3
            { "Item3", "INT" },
            { "Item3Amount", "INT" },
            { "Item3Stat1", "INT" },
            { "Item3Stat1Distribution", "INT" },
            { "Item3Stat2", "INT" },
            { "Item3Stat2Distribution", "INT" },

            // Required Item 4
            { "Item4", "INT" },
            { "Item4Amount", "INT" },
            { "Item4Stat1", "INT" },
            { "Item4Stat1Distribution", "INT" },
            { "Item4Stat2", "INT" },
            { "Item4Stat2Distribution", "INT" },

            // Output
            { "OutputID", "INT" }
        };

        // Ensure the Recipes table exists
        bool recipesTableOK = await EnsureTableExistsAsync(RecipesTableName, recipeColumns);

        if (!recipesTableOK)
        {
            Debug.LogError("Failed to create or verify required crafting table (Recipes)");
            return;
        }

        // Clear existing recipe data
        recipesById.Clear();
        recipesByName.Clear();

        // Load all recipes from the database
        await LoadRecipesAsync();
        
        isInitialized = true;
        NotifyDataLoaded();
        Debug.Log("CraftingManager initialization complete");
    }
    private async Task LoadRecipesAsync()
    {
        if (recipePrefab == null)
        {
            Debug.LogError("Recipe Prefab is not assigned in CraftingManager.");
            return;
        }

        string query = $"SELECT * FROM {RecipesTableName}";
        var results = await QueryDataAsync(query);

        if (results == null)
        {
            Debug.LogError("Failed to load recipes from database");
            return;
        }
        Debug.Log($"Loaded {results.Count} recipe rows from DB.");

        foreach (var row in results)
        {
            // --- Basic Info ---
            int recipeID = SafeConvert.ToInt32(row, "RecipeID");
            string recipeName = SafeConvert.ToString(row, "RecipeName");
            string recipeDescription = SafeConvert.ToString(row, "RecipeDescription");
            int recipeType = SafeConvert.ToInt32(row, "RecipeType");

            // --- Resources ---
            ResourceType[] requiredResources = new ResourceType[4];
            int[] requiredResourceAmounts = new int[4];
            int[] resourceTypeLevels = new int[4];
            int[] resourceStat1 = new int[4];
            int[] resourceStat1Dist = new int[4];
            int[] resourceStat2 = new int[4];
            int[] resourceStat2Dist = new int[4];

            for (int i = 0; i < 4; i++)
            {
                int index = i + 1;
                requiredResources[i] = (ResourceType)SafeConvert.ToInt32(row, $"Resource{index}");
                requiredResourceAmounts[i] = SafeConvert.ToInt32(row, $"Resource{index}Amount");
                resourceTypeLevels[i] = SafeConvert.ToInt32(row, $"Resource{index}TypeLevel");
                resourceStat1[i] = SafeConvert.ToInt32(row, $"Resource{index}Stat1");
                resourceStat1Dist[i] = SafeConvert.ToInt32(row, $"Resource{index}Stat1Distribution");
                resourceStat2[i] = SafeConvert.ToInt32(row, $"Resource{index}Stat2");
                resourceStat2Dist[i] = SafeConvert.ToInt32(row, $"Resource{index}Stat2Distribution");
            }

            // --- Items ---
            ItemTemplate[] requiredItems = new ItemTemplate[4];
            int[] requiredItemAmounts = new int[4];
            int[] itemStat1 = new int[4];
            int[] itemStat1Dist = new int[4];
            int[] itemStat2 = new int[4];
            int[] itemStat2Dist = new int[4];

            for (int i = 0; i < 4; i++)
            {
                int index = i + 1;
                int itemID = SafeConvert.ToInt32(row, $"Item{index}");
                requiredItemAmounts[i] = SafeConvert.ToInt32(row, $"Item{index}Amount");
                itemStat1[i] = SafeConvert.ToInt32(row, $"Item{index}Stat1");
                itemStat1Dist[i] = SafeConvert.ToInt32(row, $"Item{index}Stat1Distribution");
                itemStat2[i] = SafeConvert.ToInt32(row, $"Item{index}Stat2");
                itemStat2Dist[i] = SafeConvert.ToInt32(row, $"Item{index}Stat2Distribution");

                if (itemID > 0)
                {
                    requiredItems[i] = ItemManager.Instance?.GetItemTemplateById(itemID);
                    if (requiredItems[i] == null)
                    {
                        Debug.LogWarning($"Failed to find ItemTemplate with ID {itemID} for recipe {recipeName}");
                    }
                }
            }

            // --- Output ---
            int outputID = SafeConvert.ToInt32(row, "OutputID");
            ItemTemplate outputItem = ItemManager.Instance?.GetItemTemplateById(outputID);
            if (outputItem == null)
            {
                Debug.LogWarning($"Failed to find output ItemTemplate with ID {outputID} for recipe {recipeName}");
                continue;
            }

            // --- Create and Initialize Recipe ---
            GameObject recipeObj = Instantiate(recipePrefab, transform);
            Recipe recipe = recipeObj.GetComponent<Recipe>();
            if (recipe != null)
            {
                recipe.Initialize(recipeID, recipeName, recipeDescription, recipeType,
                                  requiredResources, requiredResourceAmounts, resourceTypeLevels, resourceStat1, resourceStat1Dist, resourceStat2, resourceStat2Dist,
                                  requiredItems, requiredItemAmounts, itemStat1, itemStat1Dist, itemStat2, itemStat2Dist,
                                  outputItem);

                recipesById[recipeID] = recipe;
                recipesByName[recipeName] = recipe;
                recipeObj.name = $"Recipe_{recipeID}_{recipeName}";
            }
            else
            {
                Debug.LogError($"Failed to get Recipe component for recipe {recipeName}");
                Destroy(recipeObj);
            }
        }
        Debug.Log($"Finished processing recipes. Loaded {recipesById.Count} recipes.");
    }
    #endregion

    public void CraftRecipe(Recipe recipe, Resource resource1, Resource resource2, Resource resource3, Resource resource4, Item item1, Item item2, Item item3, Item item4, Item output)
    {
        
    }
    public void CraftItem(Recipe recipe,Resource resource1, Resource resource2, Resource resource3, Resource resource4, Item item1, Item item2, Item item3, Item item4, Item output)
    {
        
    }
    public void CraftSubComponent(Recipe recipe, Resource resource1, Resource resource2, Resource resource3, Resource resource4, Item item1, Item item2, Item item3, Item item4, Item output)
    {
        
    }

    #region Getters
    public Recipe GetRecipeByID(int recipeID)
    {
        recipesById.TryGetValue(recipeID, out Recipe recipe);
        return recipe;
    }
    public Recipe GetRecipeByName(string recipeName)
    {
        recipesByName.TryGetValue(recipeName, out Recipe recipe);
        return recipe;
    }
    #endregion
}