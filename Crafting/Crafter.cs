using UnityEngine;

public class Crafter : MonoBehaviour
{
    [SerializeField] private ItemManager itemManager;

    private void Start()
    {
        itemManager = ItemManager.Instance;
    }
    
    public object CraftRecipe(Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, Item item1, Item item2, Item item3, Item item4)
    {
        object objectToReturn = null;
        if (recipe != null)
        {
            if (recipe.GetRecipeType() == RecipeType.SubComponent)
            {
                CraftSubComponent(recipe, res1, res2, res3, res4, item1, item2, item3, item4);
            }
            else if (recipe.GetRecipeType() == RecipeType.Weapon)
            {
                CraftWeapon(recipe, res1, res2, res3, res4, item1, item2, item3, item4);
            }
            else if (recipe.GetRecipeType() == RecipeType.Armour)
            {
                CraftArmour(recipe, res1, res2, res3, res4, item1, item2, item3, item4);
            }
            else if (recipe.GetRecipeType() == RecipeType.Tool)
            {
                CraftItem(recipe, res1, res2, res3, res4, item1, item2, item3, item4);
            }
            else
            {
                CraftItem(recipe, res1, res2, res3, res4, item1, item2, item3, item4);
            }
        }
        else
        {
            Debug.LogError("Recipe is null");
        }
        return objectToReturn;
    }
    public Item CraftItem(Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, Item item1, Item item2, Item item3, Item item4)
    {
        Item itemToReturn = null;


        return itemToReturn;
    }
    public Item CraftWeapon(Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, Item item1, Item item2, Item item3, Item item4)
    {
        Item itemToReturn = null;


        return itemToReturn;
    }
    public Item CraftArmour(Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, Item item1, Item item2, Item item3, Item item4)
    {
        Item itemToReturn = null;


        return itemToReturn;
    }
    public Item CraftTool(Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, SubComponent item1, SubComponent item2, SubComponent item3, SubComponent item4)
    {
        Item itemToReturn = Instantiate(itemManager.GetItemPrefab());
        ItemTemplate itemTemplate = recipe.OutputItem;

        int resValue1 = 0;
        int resValue2 = 0;
        int resValue3 = 0;
        int resValue4 = 0;
        int itemValue1 = 0;
        int itemValue2 = 0;
        int itemValue3 = 0;
        int itemValue4 = 0;

        if (recipe.ResourceStat1[0] > 0)
        {
            int stat1 = 0;
            int stat2 = 0;
            if (res1.GetResourceType() == recipe.RequiredResources[0])
            {
                stat1 = GetStatByNumber(res1, (recipe.ResourceStat1[0]) * (recipe.ResourceStat1Dist[0]) / 100);
                stat2 = GetStatByNumber(res1, (recipe.ResourceStat2[0]) * (recipe.ResourceStat2Dist[0]) / 100);
            }
            resValue1 = stat1 + stat2;
        }
        if (recipe.ResourceStat1[1] > 0)
        {
            int stat1 = 0;
            int stat2 = 0;
            if (res2.GetResourceType() == recipe.RequiredResources[1])
            {
                stat1 = GetStatByNumber(res2, (recipe.ResourceStat1[1]) * (recipe.ResourceStat1Dist[1]) / 100);
                stat2 = GetStatByNumber(res2, (recipe.ResourceStat2[1]) * (recipe.ResourceStat2Dist[1]) / 100);
            }
            resValue2 = stat1 + stat2;
        }
        if (recipe.ResourceStat1[2] > 0)
        {
            int stat1 = 0;
            int stat2 = 0;
            if (res3.GetResourceType() == recipe.RequiredResources[2])
            {
                stat1 = GetStatByNumber(res3, (recipe.ResourceStat1[2]) * (recipe.ResourceStat1Dist[2]) / 100);
                stat2 = GetStatByNumber(res3, (recipe.ResourceStat2[2]) * (recipe.ResourceStat2Dist[2]) / 100);
            }
            resValue3 = stat1 + stat2;
        }
        if (recipe.ResourceStat1[3] > 0)
        {
            int stat1 = 0;
            int stat2 = 0;
            if (res4.GetResourceType() == recipe.RequiredResources[3])
            {
                stat1 = GetStatByNumber(res4, (recipe.ResourceStat1[3]) * (recipe.ResourceStat1Dist[3]) / 100);
                stat2 = GetStatByNumber(res4, (recipe.ResourceStat2[3]) * (recipe.ResourceStat2Dist[3]) / 100);
            }
            resValue4 = stat1 + stat2;
        }

        int toolDamage = 0;

        return itemToReturn;
    }
    public SubComponent CraftSubComponent(Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, Item item1, Item item2, Item item3, Item item4)
    {
        SubComponent subComponentToReturn = null;


        return subComponentToReturn;
    }

    #region Helpers
    public int GetStatByNumber(Resource resource, int num)
    {
        int statValueToReturn = 0;
        switch (num)
        {
            case 1:
                statValueToReturn = resource.Quality;
                break;
            case 2:
                statValueToReturn = resource.Toughness;
                break;
            case 3:
                statValueToReturn = resource.Strength;
                break;
            case 4:
                statValueToReturn = resource.Density;
                break;
            case 5:
                statValueToReturn = resource.Aura;
                break;
            case 6:
                statValueToReturn = resource.Energy;
                break;
            case 7:
                statValueToReturn = resource.Protein;
                break;
            case 8:
                statValueToReturn = resource.Carbohydrate;
                break;
            case 9:
                statValueToReturn = resource.Flavour;
                break;
            default:
                Debug.LogError("Invalid stat number");                
                break;
        }
        return statValueToReturn;
    }

    #endregion
}
