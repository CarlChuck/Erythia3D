using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CraftingManager : BaseManager
{
    private const string RecipesTableName = "Recipes";

    [Header("Prefabs")]
    [SerializeField] private GameObject recipePrefab;

    [Header("Runtime Data")]
    private Dictionary<int, Recipe> recipesById = new Dictionary<int, Recipe>();
    private Dictionary<string, Recipe> recipesByName = new Dictionary<string, Recipe>();
    private Dictionary<int, List<Recipe>> recipesByWorkbenchType = new Dictionary<int, List<Recipe>>();


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
            { "WorkbenchType", "INT DEFAULT 0" },

            //Stat Distribution
            { "Stat1", "INT DEFAULT 0" },
            { "Stat1Distribution", "INT DEFAULT 0" },
            { "Stat2", "INT DEFAULT 0" },
            { "Stat2Distribution", "INT DEFAULT 0" },
            { "Stat3", "INT DEFAULT 0" },
            { "Stat3Distribution", "INT DEFAULT 0" },

            // Resource 1
            { "Resource1", "INT DEFAULT 0" },
            { "Resource1Amount", "INT DEFAULT 0" },
            { "Resource1TypeLevel", "INT DEFAULT 0" },

            // Resource 2
            { "Resource2", "INT DEFAULT 0" },
            { "Resource2Amount", "INT DEFAULT 0" },
            { "Resource2TypeLevel", "INT DEFAULT 0" },

            // Resource 3
            { "Resource3", "INT DEFAULT 0" },
            { "Resource3Amount", "INT DEFAULT 0" },
            { "Resource3TypeLevel", "INT DEFAULT 0" },

            // Resource 4
            { "Resource4", "INT DEFAULT 0" },
            { "Resource4Amount", "INT DEFAULT 0" },
            { "Resource4TypeLevel", "INT DEFAULT 0" },

            // Required Item 1
            { "Item1", "INT DEFAULT 0" },
            { "Item1Amount", "INT DEFAULT 0" },

            // Required Item 2
            { "Item2", "INT DEFAULT 0" },
            { "Item2Amount", "INT DEFAULT 0" },

            // Required Item 3
            { "Item3", "INT DEFAULT 0" },
            { "Item3Amount", "INT DEFAULT 0" },

            // Required Item 4
            { "Item4", "INT DEFAULT 0" },
            { "Item4Amount", "INT DEFAULT 0" },

            // Required Item 5
            { "Item5", "INT DEFAULT 0" },
            { "Item5Amount", "INT DEFAULT 0" },

            // Required Item 6
            { "Item6", "INT DEFAULT 0" },
            { "Item6Amount", "INT DEFAULT 0" },

            // Required Item 7
            { "Item7", "INT DEFAULT 0" },
            { "Item7Amount", "INT DEFAULT 0" },

            // Required Item 8
            { "Item8", "INT DEFAULT 0" },
            { "Item8Amount", "INT DEFAULT 0" },

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
        recipesByWorkbenchType.Clear();

        // Load all recipes from the database
        await LoadRecipesAsync();

        // Initialize WorkBenchManager with the loaded recipes grouped by workbench type
        if (WorkBenchManager.Instance != null)
        {
            // Pass a new dictionary to avoid potential modification issues if WorkBenchManager stores the reference directly
            WorkBenchManager.Instance.InitializeDefaultWorkBenchesFromRecipes(new Dictionary<int, List<Recipe>>(recipesByWorkbenchType));
        }
        else
        {
            Debug.LogWarning("WorkBenchManager.Instance is null. Cannot initialize default workbenches with recipes from CraftingManager.");
        }
        
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
            int workbenchType = SafeConvert.ToInt32(row, "WorkbenchType");

            // --- Stat Distribution ---
            int stat1 = SafeConvert.ToInt32(row, "Stat1");
            int stat1Distribution = SafeConvert.ToInt32(row, "Stat1Distribution");
            int stat2 = SafeConvert.ToInt32(row, "Stat2");
            int stat2Distribution = SafeConvert.ToInt32(row, "Stat2Distribution");
            int stat3 = SafeConvert.ToInt32(row, "Stat3");
            int stat3Distribution = SafeConvert.ToInt32(row, "Stat3Distribution");

            // --- Resources ---
            int[] resourceID = new int[4];
            int[] requiredResourceAmounts = new int[4];
            int[] resourceTypeLevels = new int[4];

            // --- Items ---
            SubComponentTemplate[] requiredComponents = new SubComponentTemplate[8];
            int[] componentAmounts = new int[8];

            for (int i = 0; i < 4; i++)
            {
                int index = i + 1;
                resourceID[i] = SafeConvert.ToInt32(row, $"Resource{index}");
                requiredResourceAmounts[i] = SafeConvert.ToInt32(row, $"Resource{index}Amount");
                resourceTypeLevels[i] = SafeConvert.ToInt32(row, $"Resource{index}TypeLevel");
            }

            for (int i = 0; i < 8; i++)
            {
                int index = i + 1;
                int componentID = SafeConvert.ToInt32(row, $"Item{index}");
                componentAmounts[i] = SafeConvert.ToInt32(row, $"Item{index}Amount");

                if (componentID > 0)
                {
                    requiredComponents[i] = ItemManager.Instance?.GetSubComponentTemplateByID(componentID);
                    if (requiredComponents[i] == null)
                    {
                        Debug.LogWarning($"Failed to find SubComponentTemplate with ID {componentID} for recipe {recipeName} (Item{index})");
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
                recipe.Initialize(recipeID, recipeName, recipeDescription, recipeType, workbenchType,
                                  stat1, stat1Distribution, stat2, stat2Distribution, stat3, stat3Distribution,
                                  resourceID, requiredResourceAmounts, resourceTypeLevels,
                                  requiredComponents, componentAmounts,
                                  outputItem, outputSubComponent);

                recipesById[recipeID] = recipe;
                recipesByName[recipeName] = recipe;
                recipeObj.name = $"Recipe_{recipeID}_{recipeName}";

                // Populate recipesByWorkbenchType
                if (!recipesByWorkbenchType.ContainsKey(workbenchType))
                {
                    recipesByWorkbenchType[workbenchType] = new List<Recipe>();
                }
                recipesByWorkbenchType[workbenchType].Add(recipe);
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

    public List<Recipe> GetRecipesByWorkbenchType(int workbenchType)
    {
        if (recipesByWorkbenchType.TryGetValue(workbenchType, out List<Recipe> recipeList))
        {
            return new List<Recipe>(recipeList); // Return a copy to prevent external modification
        }
        return new List<Recipe>(); // Return an empty list if type not found
    }
    #endregion
}