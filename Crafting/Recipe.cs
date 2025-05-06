using UnityEngine;
using System; 

public class Recipe : MonoBehaviour
{
    [Header("Basic Info")]
    [SerializeField] private int recipeID;
    [SerializeField] private string recipeName;
    [SerializeField] private string recipeDescription;
    [SerializeField] private int recipeType;

    [Header("Resources")]
    [SerializeField] private ResourceType[] requiredResources = new ResourceType[4];
    [SerializeField] private int[] requiredResourceAmounts = new int[4];
    [SerializeField] private int[] resourceTypeLevels = new int[4];
    [SerializeField] private int[] resourceStat1 = new int[4];
    [SerializeField] private int[] resourceStat1Dist = new int[4];
    [SerializeField] private int[] resourceStat2 = new int[4];
    [SerializeField] private int[] resourceStat2Dist = new int[4];

    [Header("Items")]
    [SerializeField] private ItemTemplate[] requiredItems = new ItemTemplate[4];
    [SerializeField] private int[] requiredItemAmounts = new int[4];
    [SerializeField] private int[] itemStat1 = new int[4];
    [SerializeField] private int[] itemStat1Dist = new int[4];
    [SerializeField] private int[] itemStat2 = new int[4];
    [SerializeField] private int[] itemStat2Dist = new int[4];

    [Header("Output")]
    [SerializeField] private ItemTemplate outputItem;

    // Public Properties
    public int RecipeID => recipeID;
    public string RecipeName => recipeName;
    public string RecipeDescription => recipeDescription;
    public int RecipeType => recipeType;

    // Resource Properties
    public ResourceType[] RequiredResources => requiredResources;
    public int[] RequiredResourceAmounts => requiredResourceAmounts;
    public int[] ResourceTypeLevels => resourceTypeLevels;
    public int[] ResourceStat1 => resourceStat1;
    public int[] ResourceStat1Dist => resourceStat1Dist;
    public int[] ResourceStat2 => resourceStat2;
    public int[] ResourceStat2Dist => resourceStat2Dist;

    // Item Properties
    public ItemTemplate[] RequiredItems => requiredItems;
    public int[] RequiredItemAmounts => requiredItemAmounts;
    public int[] ItemStat1 => itemStat1;
    public int[] ItemStat1Dist => itemStat1Dist;
    public int[] ItemStat2 => itemStat2;
    public int[] ItemStat2Dist => itemStat2Dist;

    // Output Property
    public ItemTemplate OutputItem => outputItem;

    // Updated Initialize method
    public void Initialize(int id, string name, string description, int type, ResourceType[] resources, int[] resourceAmounts, int[] resTypeLevels, int[] resStat1, int[] resStat1Dist, int[] resStat2, int[] resStat2Dist, ItemTemplate[] items, int[] itemAmounts, int[] itmStat1, int[] itmStat1Dist, int[] itmStat2, int[] itmStat2Dist, ItemTemplate output)
    {
        recipeID = id;
        recipeName = name;
        recipeDescription = description;
        recipeType = type;

        requiredResources = resources ?? new ResourceType[4];
        requiredResourceAmounts = resourceAmounts ?? new int[4];
        resourceTypeLevels = resTypeLevels ?? new int[4];
        resourceStat1 = resStat1 ?? new int[4];
        resourceStat1Dist = resStat1Dist ?? new int[4];
        resourceStat2 = resStat2 ?? new int[4];
        resourceStat2Dist = resStat2Dist ?? new int[4];

        requiredItems = items ?? new ItemTemplate[4];
        requiredItemAmounts = itemAmounts ?? new int[4];
        itemStat1 = itmStat1 ?? new int[4];
        itemStat1Dist = itmStat1Dist ?? new int[4];
        itemStat2 = itmStat2 ?? new int[4];
        itemStat2Dist = itmStat2Dist ?? new int[4];

        outputItem = output;
    }
    public RecipeType GetRecipeType()
    {
        return (RecipeType)recipeType;
    }
} 
public enum RecipeType
{
    Item,
    SubComponent,
    Weapon,
    Tool,
    Armour
}