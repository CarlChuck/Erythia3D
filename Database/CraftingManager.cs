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
            { "RecipeType", "INT DEFAULT 0" },

            // Resource 1
            { "Resource1", "INT DEFAULT 0" },
            { "Resource1Amount", "INT DEFAULT 0" },
            { "Resource1TypeLevel", "INT DEFAULT 0" },
            { "Resource1Stat1", "INT DEFAULT 0" },
            { "Resource1Stat1Distribution", "INT DEFAULT 0" },
            { "Resource1Stat2", "INT DEFAULT 0" },
            { "Resource1Stat2Distribution", "INT DEFAULT 0" },

            // Resource 2
            { "Resource2", "INT DEFAULT 0" },
            { "Resource2Amount", "INT DEFAULT 0" },
            { "Resource2TypeLevel", "INT DEFAULT 0" },
            { "Resource2Stat1", "INT DEFAULT 0" },
            { "Resource2Stat1Distribution", "INT DEFAULT 0" },
            { "Resource2Stat2", "INT DEFAULT 0" },
            { "Resource2Stat2Distribution", "INT DEFAULT 0" },

            // Resource 3
            { "Resource3", "INT DEFAULT 0" },
            { "Resource3Amount", "INT DEFAULT 0" },
            { "Resource3TypeLevel", "INT DEFAULT 0" },
            { "Resource3Stat1", "INT DEFAULT 0" },
            { "Resource3Stat1Distribution", "INT DEFAULT 0" },
            { "Resource3Stat2", "INT DEFAULT 0" },
            { "Resource3Stat2Distribution", "INT DEFAULT 0" },

            // Resource 4
            { "Resource4", "INT DEFAULT 0" },
            { "Resource4Amount", "INT DEFAULT 0" },
            { "Resource4TypeLevel", "INT DEFAULT 0" },
            { "Resource4Stat1", "INT DEFAULT 0" },
            { "Resource4Stat1Distribution", "INT DEFAULT 0" },
            { "Resource4Stat2", "INT DEFAULT 0" },
            { "Resource4Stat2Distribution", "INT DEFAULT 0" },

            // Required Item 1
            { "Item1", "INT DEFAULT 0" },
            { "Item1Amount", "INT DEFAULT 0" },
            { "Item1Stat1", "INT DEFAULT 0" },
            { "Item1Stat1Distribution", "INT DEFAULT 0" },
            { "Item1Stat2", "INT DEFAULT 0" },
            { "Item1Stat2Distribution", "INT DEFAULT 0" },

            // Required Item 2
            { "Item2", "INT DEFAULT 0" },
            { "Item2Amount", "INT DEFAULT 0" },
            { "Item2Stat1", "INT DEFAULT 0" },
            { "Item2Stat1Distribution", "INT DEFAULT 0" },
            { "Item2Stat2", "INT DEFAULT 0" },
            { "Item2Stat2Distribution", "INT DEFAULT 0" },

            // Required Item 3
            { "Item3", "INT DEFAULT 0" },
            { "Item3Amount", "INT DEFAULT 0" },
            { "Item3Stat1", "INT DEFAULT 0" },
            { "Item3Stat1Distribution", "INT DEFAULT 0" },
            { "Item3Stat2", "INT DEFAULT 0" },
            { "Item3Stat2Distribution", "INT DEFAULT 0" },

            // Required Item 4
            { "Item4", "INT DEFAULT 0" },
            { "Item4Amount", "INT DEFAULT 0" },
            { "Item4Stat1", "INT DEFAULT 0" },
            { "Item4Stat1Distribution", "INT DEFAULT 0" },
            { "Item4Stat2", "INT DEFAULT 0" },
            { "Item4Stat2Distribution", "INT DEFAULT 0" },

            // Output
            { "OutputItemID", "INT DEFAULT 0" },
            { "OutputSubComponentID", "INT DEFAULT 0" }
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
            int[] resourceID = new int[4];
            int[] requiredResourceAmounts = new int[4];
            int[] resourceTypeLevels = new int[4];
            int[] resourceStat1 = new int[4];
            int[] resourceStat1Dist = new int[4];
            int[] resourceStat2 = new int[4];
            int[] resourceStat2Dist = new int[4];            
            
            // --- Items ---
            SubComponentTemplate[] requiredComponents = new SubComponentTemplate[4];
            int[] componentAmounts = new int[4];
            int[] componentStat1 = new int[4];
            int[] componentStat1Dist = new int[4];
            int[] componentStat2 = new int[4];
            int[] componentStat2Dist = new int[4];

            for (int i = 0; i < 4; i++)
            {
                int index = i + 1;
                resourceID[i] = SafeConvert.ToInt32(row, $"Resource{index}");
                requiredResourceAmounts[i] = SafeConvert.ToInt32(row, $"Resource{index}Amount");
                resourceTypeLevels[i] = SafeConvert.ToInt32(row, $"Resource{index}TypeLevel");
                resourceStat1[i] = SafeConvert.ToInt32(row, $"Resource{index}Stat1");
                resourceStat1Dist[i] = SafeConvert.ToInt32(row, $"Resource{index}Stat1Distribution");
                resourceStat2[i] = SafeConvert.ToInt32(row, $"Resource{index}Stat2");
                resourceStat2Dist[i] = SafeConvert.ToInt32(row, $"Resource{index}Stat2Distribution");

                int componentID = SafeConvert.ToInt32(row, $"Item{index}");
                componentAmounts[i] = SafeConvert.ToInt32(row, $"Item{index}Amount");
                componentStat1[i] = SafeConvert.ToInt32(row, $"Item{index}Stat1");
                componentStat1Dist[i] = SafeConvert.ToInt32(row, $"Item{index}Stat1Distribution");
                componentStat2[i] = SafeConvert.ToInt32(row, $"Item{index}Stat2");
                componentStat2Dist[i] = SafeConvert.ToInt32(row, $"Item{index}Stat2Distribution");

                if (componentID > 0)
                {
                    requiredComponents[i] = ItemManager.Instance?.GetSubComponentTemplateByID(componentID);
                    if (requiredComponents[i] == null)
                    {
                        Debug.LogWarning($"Failed to find ItemTemplate with ID {componentID} for recipe {recipeName}");
                    }
                }
            }

            // --- Output ---
            int outputItemID = SafeConvert.ToInt32(row, "OutputItemID");
            int outputComponentID = SafeConvert.ToInt32(row, "OutputSubComponentID");
            ItemTemplate outputItem = ItemManager.Instance?.GetItemTemplateById(outputItemID);
            SubComponentTemplate outputSubComponent = ItemManager.Instance?.GetSubComponentTemplateByID(outputComponentID);
            if (outputItem == null)
            {
                Debug.LogWarning($"Failed to find output ItemTemplate with ID {outputItemID} for recipe {recipeName}");
                Debug.LogWarning($"Failed to find output SubComponentTemplate with ID {outputItemID} for recipe {recipeName}");
                continue;
            }

            // --- Create and Initialize Recipe ---
            GameObject recipeObj = Instantiate(recipePrefab, transform);
            Recipe recipe = recipeObj.GetComponent<Recipe>();
            if (recipe != null)
            {
                recipe.Initialize(recipeID, recipeName, recipeDescription, recipeType,
                                  resourceID, requiredResourceAmounts, resourceTypeLevels, resourceStat1, resourceStat1Dist, resourceStat2, resourceStat2Dist,
                                  requiredComponents, componentAmounts, componentStat1, componentStat1Dist, componentStat2, componentStat2Dist,
                                  outputItem, outputSubComponent);

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