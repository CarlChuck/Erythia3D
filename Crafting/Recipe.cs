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
    [SerializeField] private ResourceType[] resourceTypes = new ResourceType[4];
    [SerializeField] private ResourceFamily[] resourceFamilies = new ResourceFamily[4];
    [SerializeField] private ResourceOrder[] resourceOrders = new ResourceOrder[4];
    [SerializeField] private int[] resourceAmounts = new int[4];
    [SerializeField] private int[] resourceTypeLevels = new int[4];
    [SerializeField] private int[] resourceStat1 = new int[4];
    [SerializeField] private int[] resourceStat1Dist = new int[4];
    [SerializeField] private int[] resourceStat2 = new int[4];
    [SerializeField] private int[] resourceStat2Dist = new int[4];

    [Header("Items")]
    [SerializeField] private SubComponentTemplate[] subComponents = new SubComponentTemplate[4];
    [SerializeField] private int[] componentAmounts = new int[4];
    [SerializeField] private int[] subComponentStat1 = new int[4];
    [SerializeField] private int[] subComponentStat1Dist = new int[4];
    [SerializeField] private int[] subComponentStat2 = new int[4];
    [SerializeField] private int[] subComponentStat2Dist = new int[4];

    [Header("Output")]
    [SerializeField] private ItemTemplate outputItem;
    [SerializeField] private SubComponentTemplate outputSubComponent;

    // Public Properties
    public int RecipeID => recipeID;
    public string RecipeName => recipeName;
    public string RecipeDescription => recipeDescription;
    public int RecipeType => recipeType;

    // Resource Properties
    public ResourceType[] ResourceTypes => resourceTypes;
    public ResourceFamily[] ResourceFamilies => resourceFamilies;
    public ResourceOrder[] ResourceOrders => resourceOrders;
    public int[] ResourceAmounts => resourceAmounts;
    public int[] ResourceTypeLevels => resourceTypeLevels;
    public int[] ResourceStat1 => resourceStat1;
    public int[] ResourceStat1Dist => resourceStat1Dist;
    public int[] ResourceStat2 => resourceStat2;
    public int[] ResourceStat2Dist => resourceStat2Dist;

    // SubComponent Properties
    public SubComponentTemplate[] SubComponents => subComponents;
    public int[] ComponentAmounts => componentAmounts;
    public int[] SubComponentStat1 => subComponentStat1;
    public int[] SubComponentStat1Dist => subComponentStat1Dist;
    public int[] SubComponentStat2 => subComponentStat2;
    public int[] SubComponentStat2Dist => subComponentStat2Dist;

    // Output Property
    public ItemTemplate OutputItem => outputItem;
    public SubComponentTemplate OutputSubComponent => outputSubComponent;

    // Updated Initialize method
    public void Initialize(int id, string name, string description, int type, int[] resourceIDs,  int[] resourceAmnts, int[] resTypeLevels, int[] resStat1, int[] resStat1Dist, int[] resStat2, int[] resStat2Dist, SubComponentTemplate[] subComp, int[] compAmounts, int[] compStat1, int[] compStat1Dist, int[] compStat2, int[] compStat2Dist, ItemTemplate outputIt, SubComponentTemplate outputSub)
    {
        recipeID = id;
        recipeName = name;
        recipeDescription = description;
        recipeType = type;

        for (int i = 0; i < 4; i++)
        {
            if (resourceIDs != null && resourceIDs[i] > 0)
            {
                if (resourceTypeLevels[i] == 1)
                {
                    if (resourceIDs[i] > Enum.GetValues(typeof(ResourceOrder)).Length)
                    {
                        resourceOrders[i] = (ResourceOrder)resourceIDs[i];
                    }
                }
                else if (resourceTypeLevels[i] == 2)
                {
                    if (resourceIDs[i] > Enum.GetValues(typeof(ResourceFamily)).Length)
                    {
                        resourceFamilies[i] = (ResourceFamily)resourceIDs[i];

                    }
                }
                else if (resourceTypeLevels[i] == 3)
                {
                    if (resourceIDs[i] > Enum.GetValues(typeof(ResourceType)).Length)
                    {
                        resourceTypes[i] = (ResourceType)resourceIDs[i];
                    }
                }
            }
        }
        resourceAmounts = resourceAmnts ?? new int[4];
        resourceTypeLevels = resTypeLevels ?? new int[4];
        resourceStat1 = resStat1 ?? new int[4];
        resourceStat1Dist = resStat1Dist ?? new int[4];
        resourceStat2 = resStat2 ?? new int[4];
        resourceStat2Dist = resStat2Dist ?? new int[4];

        subComponents = subComp ?? new SubComponentTemplate[4];
        componentAmounts = compAmounts ?? new int[4];
        subComponentStat1 = compStat1 ?? new int[4];
        subComponentStat1Dist = compStat1Dist ?? new int[4];
        subComponentStat2 = compStat2 ?? new int[4];
        subComponentStat2Dist = compStat2Dist ?? new int[4];

        outputItem = outputIt;
        outputSubComponent = outputSub;
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