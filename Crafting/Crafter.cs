using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

public class Crafter : MonoBehaviour
{
    [SerializeField] private ItemManager itemManager;
    [SerializeField] private InventoryManager inventoryManager;

    private void Start()
    {
        itemManager = ItemManager.Instance;
        inventoryManager = InventoryManager.Instance;
    }
    
    public object CraftRecipe(PlayerCharacter character, Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, SubComponent component1, SubComponent component2, SubComponent component3, SubComponent component4)
    {
        if (recipe != null)
        {
            Resource[] resources;
            if (res4 != null)
            {
                resources = new Resource[4] { res1, res2, res3, res4 };
            }
            else if (res3 != null)
            {
                resources = new Resource[3] { res1, res2, res3 };
            }
            else if (res2 != null)
            {
                resources = new Resource[2] { res1, res2 };
            }
            else
            {
                resources = new Resource[1] { res1 };
            }

            bool areResourcesMatching = true;
            for (int i = 0; i < resources.Length; i++)
            {
                if (recipe.ResourceTypeLevels[i] == 0)
                {
                    if (recipe.ResourceOrders[i] != resources[i].GetResourceOrder())
                    {
                        areResourcesMatching = false;
                        break;
                    }
                }
                else if (recipe.ResourceTypeLevels[i] == 1)
                {
                    if (recipe.ResourceFamilies[i] != resources[i].GetResourceTemplate().Family)
                    {
                        areResourcesMatching = false;
                        break;
                    }
                }
                else if (recipe.ResourceTypeLevels[i] == 2)
                {
                    if (recipe.ResourceTypes[i] != resources[i].GetResourceType())
                    {
                        areResourcesMatching = false;
                        break;
                    }
                }
                else
                {
                    Debug.LogError("Invalid resource type level");
                }
            }            

            if (areResourcesMatching)
            {
                if (recipe.GetRecipeType() == RecipeType.SubComponent)
                {
                    return CraftSubComponent(character, recipe, res1, res2, res3, res4, component1, component2, component3, component4);
                }
                else if (recipe.GetRecipeType() == RecipeType.Weapon)
                {
                    return CraftWeapon(character, recipe, res1, res2, res3, res4, component1, component2, component3, component4);
                }
                else if (recipe.GetRecipeType() == RecipeType.Armour)
                {
                    return CraftArmour(character, recipe, res1, res2, res3, res4, component1, component2, component3, component4);
                }
                else if (recipe.GetRecipeType() == RecipeType.Tool)
                {
                    return CraftTool(character, recipe, res1, res2, res3, res4, component1, component2, component3, component4);
                }
                else
                {
                    return CraftItem(character, recipe, res1, res2, res3, res4, component1, component2, component3, component4);
                }
            }
        }
        else
        {
            Debug.LogError("Recipe is null");
        }
        return null;
    }
    public Item CraftItem(PlayerCharacter character, Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, SubComponent component1, SubComponent component2, SubComponent component3, SubComponent component4)
    {
        ItemTemplate itemTemplate = recipe.OutputItem;
        Item itemToReturn = MapTemplateToItem(itemTemplate);


        return itemToReturn;
    }
    public Item CraftWeapon(PlayerCharacter character, Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, SubComponent component1, SubComponent component2, SubComponent component3, SubComponent component4)
    {
        ItemTemplate itemTemplate = recipe.OutputItem;
        Item itemToReturn = MapTemplateToItem(itemTemplate);


        return itemToReturn;
    }
    public Item CraftArmour(PlayerCharacter character, Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, SubComponent component1, SubComponent component2, SubComponent component3, SubComponent component4)
    {
        ItemTemplate itemTemplate = recipe.OutputItem;
        Item itemToReturn = MapTemplateToItem(itemTemplate);


        return itemToReturn;
    }
    public async Task<Item> CraftTool(PlayerCharacter character, Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, SubComponent component1, SubComponent component2, SubComponent component3, SubComponent component4)
    {
        ItemTemplate itemTemplate = recipe.OutputItem;
        Item itemToReturn = MapTemplateToItem(itemTemplate);


        int resValue1 = 0;
        int resValue2 = 0;
        int resValue3 = 0;
        int resValue4 = 0;
        int subComponentValue1 = 0;
        int subComponentValue2 = 0;
        int subComponentValue3 = 0;
        int subComponentValue4 = 0;

        if (recipe.ResourceStat1[0] > 0)
        {
            int stat1 = 0;
            int stat2 = 0;
            if (res1.GetResourceType() == recipe.ResourceTypes[0])
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
            if (res2.GetResourceType() == recipe.ResourceTypes[1])
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
            if (res3.GetResourceType() == recipe.ResourceTypes[2])
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
            if (res4.GetResourceType() == recipe.ResourceTypes[3])
            {
                stat1 = GetStatByNumber(res4, (recipe.ResourceStat1[3]) * (recipe.ResourceStat1Dist[3]) / 100);
                stat2 = GetStatByNumber(res4, (recipe.ResourceStat2[3]) * (recipe.ResourceStat2Dist[3]) / 100);
            }
            resValue4 = stat1 + stat2;
        }
        if (recipe.SubComponentStat1[0] > 0)
        {
            int stat1 = 0;
            int stat2 = 0;

            subComponentValue1 = stat1 + stat2;
        }





        int toolDamage = 0;

        long itemID = await itemManager.SaveNewItemInstanceAsync(itemToReturn);
        if (itemID > 0)
        {
            itemToReturn.SetItemID((int)itemID);
            character.OnPickupItem(itemToReturn);
            bool isSaved = await inventoryManager.AddItemToInventoryAsync(character.GetCharacterID(), (int)itemID, 0);
            return itemToReturn;
        }
        else
        {
            Debug.LogError("Failed to save crafted tool to database. Destroying instance.");
            Destroy(itemToReturn.gameObject);
            return null;
        }
    }
    public SubComponent CraftSubComponent(PlayerCharacter character, Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, SubComponent component1, SubComponent component2, SubComponent component3, SubComponent component4)
    {
        SubComponentTemplate itemTemplate = recipe.OutputSubComponent;
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

    public Item MapTemplateToItem(ItemTemplate itemTemplate)
    {
        Item item = Instantiate(itemManager.GetItemPrefab());
        item.SetItem(0, itemTemplate.ItemTemplateID, itemTemplate.ItemName, (int)itemTemplate.Type, itemTemplate.MaxDurability, itemTemplate.MaxDurability, itemTemplate.Damage, itemTemplate.Speed, (int)itemTemplate.WeaponType,
            (int)itemTemplate.Slot, itemTemplate.SlashResist, itemTemplate.ThrustResist, itemTemplate.CrushResist, itemTemplate.HeatResist, itemTemplate.ShockResist, itemTemplate.ColdResist,
            itemTemplate.MindResist, itemTemplate.CorruptResist, itemTemplate.Icon, itemTemplate.ColourHex,
            itemTemplate.Weight, itemTemplate.Model,
            itemTemplate.IsStackable, itemTemplate.StackSizeMax, itemTemplate.Price);
        return item;
    }

    #endregion
}
