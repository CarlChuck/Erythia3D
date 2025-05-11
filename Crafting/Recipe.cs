using UnityEngine;
using System; 

public class Recipe : MonoBehaviour
{
    [Header("Basic Info")]
    [SerializeField] private int recipeID;
    [SerializeField] private string recipeName;
    [SerializeField] private string recipeDescription;
    [SerializeField] private int recipeType;
    [SerializeField] private int workbenchType;

    [Header("Stat Distribution")]
    [SerializeField] private int stat1;
    [SerializeField] private int stat1Distribution;
    [SerializeField] private int stat2;
    [SerializeField] private int stat2Distribution;
    [SerializeField] private int stat3;
    [SerializeField] private int stat3Distribution;

    [Header("Resources")]
    [SerializeField] private ResourceType[] resourceTypes = new ResourceType[4];
    [SerializeField] private ResourceFamily[] resourceFamilies = new ResourceFamily[4];
    [SerializeField] private ResourceOrder[] resourceOrders = new ResourceOrder[4];
    [SerializeField] private int[] resourceAmounts = new int[4];
    [SerializeField] private int[] resourceTypeLevels = new int[4];

    [Header("Items")]
    [SerializeField] private SubComponentTemplate[] subComponents = new SubComponentTemplate[8];
    [SerializeField] private int[] componentAmounts = new int[8];

    [Header("Output")]
    [SerializeField] private ItemTemplate outputItem;
    [SerializeField] private SubComponentTemplate outputSubComponent;

    // Public Properties
    public int RecipeID => recipeID;
    public string RecipeName => recipeName;
    public string RecipeDescription => recipeDescription;
    public int RecipeType => recipeType;
    public int WorkbenchType => workbenchType;

    // Stat Distribution Properties
    public int Stat1 => stat1;
    public int Stat1Distribution => stat1Distribution;
    public int Stat2 => stat2;
    public int Stat2Distribution => stat2Distribution;
    public int Stat3 => stat3;
    public int Stat3Distribution => stat3Distribution;

    // Resource Properties
    public ResourceType[] ResourceTypes => resourceTypes;
    public ResourceFamily[] ResourceFamilies => resourceFamilies;
    public ResourceOrder[] ResourceOrders => resourceOrders;
    public int[] ResourceAmounts => resourceAmounts;
    public int[] ResourceTypeLevels => resourceTypeLevels;

    // SubComponent Properties
    public SubComponentTemplate[] SubComponents => subComponents;
    public int[] ComponentAmounts => componentAmounts;

    // Output Property
    public ItemTemplate OutputItem => outputItem;
    public SubComponentTemplate OutputSubComponent => outputSubComponent;

    // Updated Initialize method
    public void Initialize(int id, string name, string description, int type, int workbenchType,
                           int s1, int s1Dist, int s2, int s2Dist, int s3, int s3Dist,
                           int[] resourceIDs, int[] resourceAmnts, int[] resTypeLevels,
                           SubComponentTemplate[] subComp, int[] compAmounts,
                           ItemTemplate outputIt, SubComponentTemplate outputSub)
    {
        recipeID = id;
        recipeName = name;
        recipeDescription = description;
        recipeType = type;
        this.workbenchType = workbenchType;

        // Stat Distribution
        stat1 = s1;
        stat1Distribution = s1Dist;
        stat2 = s2;
        stat2Distribution = s2Dist;
        stat3 = s3;
        stat3Distribution = s3Dist;

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

        subComponents = subComp ?? new SubComponentTemplate[8];
        componentAmounts = compAmounts ?? new int[8];

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