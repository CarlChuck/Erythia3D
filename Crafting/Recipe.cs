using UnityEngine;

public class Recipe : MonoBehaviour
{
    [SerializeField] private int recipeID;
    [SerializeField] private string recipeName;
    [SerializeField] private string recipeDescription;
    [SerializeField] private ResourceType[] requiredResources = new ResourceType[4];
    [SerializeField] private int[] requiredResourceAmounts = new int[4];
    [SerializeField] private ItemTemplate[] requiredItems = new ItemTemplate[4];
    [SerializeField] private ItemTemplate outputItem;

    public int RecipeID => recipeID;
    public string RecipeName => recipeName;
    public string RecipeDescription => recipeDescription;
    public ResourceType[] RequiredResources => requiredResources;
    public int[] RequiredResourceAmounts => requiredResourceAmounts;
    public ItemTemplate[] RequiredItems => requiredItems;
    public ItemTemplate OutputItem => outputItem;

    public void Initialize(int id, string name, string description, ResourceType[] resources, int[] resourceAmounts, ItemTemplate[] items, ItemTemplate output)
    {
        recipeID = id;
        recipeName = name;
        recipeDescription = description;
        requiredResources = resources;
        requiredResourceAmounts = resourceAmounts;
        requiredItems = items;
        outputItem = output;
    }
} 