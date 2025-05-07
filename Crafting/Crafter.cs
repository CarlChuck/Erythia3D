using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using static UnityEngine.Rendering.DebugUI;

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
            SubComponent[] components;
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
            if (component4 != null)
            {
                components = new SubComponent[4] { component1, component2, component3, component4 };
            }
            else if (component3 != null)
            {
                components = new SubComponent[3] { component1, component2, component3 };
            }
            else if (component2 != null)
            {
                components = new SubComponent[2] { component1, component2 };
            }
            else
            {
                components = new SubComponent[1] { component1 };
            }
                

            if (IsResourcesAndSubComponentsMatching(recipe, resources, components))
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
            else
            {
                Debug.LogError("Resources and subcomponents do not match recipe requirements");
                return null;
            }
        }
        else
        {
            Debug.LogError("Recipe is null");
            return null;
        }

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

        int resCount = 0;
        if (res1 != null) resCount++;
        if (res2 != null) resCount++;
        if (res3 != null) resCount++;
        if (res4 != null) resCount++;

        int componentCount = 0;
        if (component1 != null) componentCount++;
        if (component2 != null) componentCount++;
        if (component3 != null) componentCount++;
        if (component4 != null) componentCount++;

        int[] resValues = new int[resCount];
        int[] subComponentValues = new int[componentCount];

        if (recipe.ResourceStat1[0] > 0 && res1 != null)
        {
            int stat1 = GetStatByNumber(recipe.ResourceStat1[0], res1, null);
            int stat2 = GetStatByNumber(recipe.ResourceStat2[0], res1, null);

            resValues[0] = ((stat1 * recipe.ResourceStat1Dist[0]) / 100) + ((stat2 * recipe.ResourceStat2Dist[0]) / 100);
        }
        if (recipe.ResourceStat1[1] > 0 && res2 != null)
        {
            int stat1 = GetStatByNumber(recipe.ResourceStat1[1], res2, null);
            int stat2 = GetStatByNumber(recipe.ResourceStat2[1], res2, null);

            resValues[1] = ((stat1 * recipe.ResourceStat1Dist[1]) / 100) + ((stat2 * recipe.ResourceStat2Dist[1]) / 100);
        }
        if (recipe.ResourceStat1[2] > 0 && res3 != null)
        {
            int stat1 = GetStatByNumber(recipe.ResourceStat1[2], res3, null);
            int stat2 = GetStatByNumber(recipe.ResourceStat2[2], res3, null);

            resValues[2] = ((stat1 * recipe.ResourceStat1Dist[2]) / 100) + ((stat2 * recipe.ResourceStat2Dist[2]) / 100);
        }
        if (recipe.ResourceStat1[3] > 0 && res4 != null)
        {
            int stat1 = GetStatByNumber(recipe.ResourceStat1[3], res4, null);
            int stat2 = GetStatByNumber(recipe.ResourceStat2[3], res4, null);

            resValues[3] = ((stat1 * recipe.ResourceStat1Dist[3]) / 100) + ((stat2 * recipe.ResourceStat2Dist[3]) / 100);
        }
        if (recipe.SubComponentStat1[0] > 0 && component1 != null)
        {
            int stat1 = GetStatByNumber(recipe.SubComponentStat1[0], null, component1);
            int stat2 = GetStatByNumber(recipe.SubComponentStat2[0], null, component1);
            subComponentValues[0] = ((stat1 * recipe.SubComponentStat1Dist[0]) / 100) + ((stat2 * recipe.SubComponentStat2Dist[0]) / 100);
        }
        if (recipe.SubComponentStat1[1] > 0 && component2 != null)
        {
            int stat1 = GetStatByNumber(recipe.SubComponentStat1[1], null, component2);
            int stat2 = GetStatByNumber(recipe.SubComponentStat2[1], null, component2);
            subComponentValues[1] = ((stat1 * recipe.SubComponentStat1Dist[1]) / 100) + ((stat2 * recipe.SubComponentStat2Dist[1]) / 100);
        }
        if (recipe.SubComponentStat1[2] > 0 && component3 != null)
        {
            int stat1 = GetStatByNumber(recipe.SubComponentStat1[2], null, component3);
            int stat2 = GetStatByNumber(recipe.SubComponentStat2[2], null, component3);
            subComponentValues[2] = ((stat1 * recipe.SubComponentStat1Dist[2]) / 100) + ((stat2 * recipe.SubComponentStat2Dist[2]) / 100);
        }
        if (recipe.SubComponentStat1[3] > 0 && component4 != null)
        {
            int stat1 = GetStatByNumber(recipe.SubComponentStat1[3], null, component4);
            int stat2 = GetStatByNumber(recipe.SubComponentStat2[3], null, component4);
            subComponentValues[3] = ((stat1 * recipe.SubComponentStat1Dist[3]) / 100) + ((stat2 * recipe.SubComponentStat2Dist[3]) / 100);
        }

        int averageResValue = 0;
        for (int i = 0; i < resValues.Length; i++)
        {
            averageResValue += resValues[i];
        }
        averageResValue /= resValues.Length;

        int averageSubComponentValue = 0;
        for (int i = 0; i < subComponentValues.Length; i++)
        {
            averageSubComponentValue += subComponentValues[i];
        }
        averageSubComponentValue /= subComponentValues.Length;


        int toolDamage = (int)(recipe.OutputItem.Damage * (averageResValue + averageSubComponentValue) / 200);

        itemToReturn.SetDamage(toolDamage);

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
    public int GetStatByNumber(int num, Resource resource = null, SubComponent component = null)
    {
        if (resource == null && component == null)
        {
            Debug.LogError("No resource or subcomponent provided");
            return 0;
        }
        if (component == null)
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
        else
        {

            int statValueToReturn = 0;
            switch (num)
            {
                case 1:
                    statValueToReturn = component.Quality;
                    break;
                case 2:
                    statValueToReturn = component.Toughness;
                    break;
                case 3:
                    statValueToReturn = component.Strength;
                    break;
                case 4:
                    statValueToReturn = component.Density;
                    break;
                case 5:
                    statValueToReturn = component.Aura;
                    break;
                case 6:
                    statValueToReturn = component.Energy;
                    break;
                case 7:
                    statValueToReturn = component.Protein;
                    break;
                case 8:
                    statValueToReturn = component.Carbohydrate;
                    break;
                case 9:
                    statValueToReturn = component.Flavour;
                    break;
                default:
                    Debug.LogError("Invalid stat number");
                    break;
            }
            return statValueToReturn;
        }
    }

    public bool IsResourcesAndSubComponentsMatching(Recipe recipe, Resource[] resources, SubComponent[] components)
    {
        for (int i = 0; i < resources.Length; i++)
        {
            if (recipe.ResourceTypeLevels[i] == 0)
            {
                if (recipe.ResourceOrders[i] != resources[i].GetResourceOrder())
                {
                    return false;
                }
            }
            else if (recipe.ResourceTypeLevels[i] == 1)
            {
                if (recipe.ResourceFamilies[i] != resources[i].GetResourceTemplate().Family)
                {
                    return false;
                }
            }
            else if (recipe.ResourceTypeLevels[i] == 2)
            {
                if (recipe.ResourceTypes[i] != resources[i].GetResourceType())
                {
                    return false;
                }
            }
            else
            {
                Debug.LogError("Invalid resource type level");
            }
            if (recipe.ComponentAmounts[i] > 0)
            {
                if (recipe.SubComponents[i].ComponentTemplateID != components[i].SubComponentTemplateID)
                {
                    return false;
                }
            }
        }
        return true;
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
