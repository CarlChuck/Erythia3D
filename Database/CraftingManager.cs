using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CraftingManager : BaseManager
{
    [SerializeField] private GameObject recipePrefab;
    private Dictionary<int, Recipe> recipesById = new Dictionary<int, Recipe>();
    private Dictionary<string, Recipe> recipesByName = new Dictionary<string, Recipe>();

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
            { "Resource1", "INT" },
            { "Resource1Amount", "INT" },
            { "Resource2", "INT" },
            { "Resource2Amount", "INT" },
            { "Resource3", "INT" },
            { "Resource3Amount", "INT" },
            { "Resource4", "INT" },
            { "Resource4Amount", "INT" },
            { "Item1", "INT" },
            { "Item2", "INT" },
            { "Item3", "INT" },
            { "Item4", "INT" },
            { "OutputID", "INT" }
        };

        // Ensure the Recipes table exists
        if (!await EnsureTableExistsAsync("Recipes", recipeColumns))
        {
            Debug.LogError("Failed to create or verify Recipes table");
            return;
        }

        // Load all recipes from the database
        await LoadRecipesAsync();
        
        isInitialized = true;
        NotifyDataLoaded();
        Debug.Log("CraftingManager initialization complete");
    }

    private async Task LoadRecipesAsync()
    {
        string query = "SELECT * FROM Recipes";
        var results = await QueryDataAsync(query);

        if (results == null)
        {
            Debug.LogError("Failed to load recipes from database");
            return;
        }

        foreach (var row in results)
        {
            int recipeID = SafeConvert.ToInt32(row, "RecipeID");
            string recipeName = SafeConvert.ToString(row, "RecipeName");
            string recipeDescription = SafeConvert.ToString(row, "RecipeDescription");
            
            // Load required resources and their amounts
            ResourceType[] requiredResources = new ResourceType[4];
            int[] requiredResourceAmounts = new int[4];
            
            requiredResources[0] = (ResourceType)SafeConvert.ToInt32(row, "Resource1");
            requiredResourceAmounts[0] = SafeConvert.ToInt32(row, "Resource1Amount");
            requiredResources[1] = (ResourceType)SafeConvert.ToInt32(row, "Resource2");
            requiredResourceAmounts[1] = SafeConvert.ToInt32(row, "Resource2Amount");
            requiredResources[2] = (ResourceType)SafeConvert.ToInt32(row, "Resource3");
            requiredResourceAmounts[2] = SafeConvert.ToInt32(row, "Resource3Amount");
            requiredResources[3] = (ResourceType)SafeConvert.ToInt32(row, "Resource4");
            requiredResourceAmounts[3] = SafeConvert.ToInt32(row, "Resource4Amount");
            
            // Load required items and convert to ItemTemplates
            ItemTemplate[] requiredItems = new ItemTemplate[4];
            int[] itemIDs = new int[4];
            itemIDs[0] = SafeConvert.ToInt32(row, "Item1");
            itemIDs[1] = SafeConvert.ToInt32(row, "Item2");
            itemIDs[2] = SafeConvert.ToInt32(row, "Item3");
            itemIDs[3] = SafeConvert.ToInt32(row, "Item4");

            // Convert item IDs to ItemTemplates
            for (int i = 0; i < 4; i++)
            {
                if (itemIDs[i] > 0) // Only try to convert if there's an actual item ID
                {
                    requiredItems[i] = ItemManager.Instance.GetItemTemplateById(itemIDs[i]);
                    if (requiredItems[i] == null)
                    {
                        Debug.LogError($"Failed to find ItemTemplate with ID {itemIDs[i]} for recipe {recipeName}");
                    }
                }
            }
            
            // Get the output ItemTemplate
            int outputID = SafeConvert.ToInt32(row, "OutputID");
            ItemTemplate outputItem = ItemManager.Instance.GetItemTemplateById(outputID);
            if (outputItem == null)
            {
                Debug.LogError($"Failed to find ItemTemplate with ID {outputID} for recipe {recipeName}");
                continue;
            }

            // Create and initialize the recipe object
            GameObject recipeObj = Instantiate(recipePrefab, transform);
            Recipe recipe = recipeObj.GetComponent<Recipe>();
            if (recipe != null)
            {
                recipe.Initialize(recipeID, recipeName, recipeDescription, requiredResources, requiredResourceAmounts, requiredItems, outputItem);
                recipesById[recipeID] = recipe;
                recipesByName[recipeName] = recipe;
            }
            else
            {
                Debug.LogError($"Failed to get Recipe component for recipe {recipeName}");
                Destroy(recipeObj);
            }
        }
    }

    public Recipe GetRecipeByID(int recipeID)
    {
        if (recipesById.TryGetValue(recipeID, out Recipe recipe))
        {
            return recipe;
        }
        return null;
    }

    public Recipe GetRecipeByName(string recipeName)
    {
        if (recipesByName.TryGetValue(recipeName, out Recipe recipe))
        {
            return recipe;
        }
        return null;
    }
} 