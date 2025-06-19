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
    /*
    public object CraftRecipe(PlayerStatBlock character, Recipe recipe, Resource res1, Resource res2, Resource res3, Resource res4, SubComponent component1, SubComponent component2, SubComponent component3, SubComponent component4, SubComponent component5, SubComponent component6, SubComponent component7, SubComponent component8)
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
            if (component8 != null)
            {
                components = new SubComponent[8] { component1, component2, component3, component4, component5, component6, component7, component8 };
            }
            else if (component7 != null)
            {
                components = new SubComponent[7] { component1, component2, component3, component4, component5, component6, component7 };
            }
            else if (component6 != null)
            {
                components = new SubComponent[6] { component1, component2, component3, component4, component5, component6 };
            }
            else if (component5 != null)
            {
                components = new SubComponent[5] { component1, component2, component3, component4, component5 };
            }
            else if (component4 != null)
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
                    return CraftSubComponent(character, recipe, resources, components);
                }
                else if (recipe.GetRecipeType() == RecipeType.Weapon)
                {
                    return CraftWeapon(character, recipe, resources, components);
                }
                else if (recipe.GetRecipeType() == RecipeType.Armour)
                {
                    return CraftArmour(character, recipe, resources, components);
                }
                else if (recipe.GetRecipeType() == RecipeType.Tool)
                {
                    return CraftTool(character, recipe, resources, components);
                }
                else
                {
                    return CraftItem(character, recipe, resources, components);
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
    */
    public async Task<Item> CraftItem(PlayerStatBlock character, Recipe recipe, Resource[] resources, SubComponent[] components)
    {
        ItemTemplate itemTemplate = recipe.OutputItem;
        Item itemToReturn = MapTemplateToItem(itemTemplate);
        int[] resValues = GetFinalStatValueArray(resources, components);

        int stat1 = (resValues[recipe.Stat1] * recipe.Stat1Distribution) / 100;
        int stat2 = (resValues[recipe.Stat2] * recipe.Stat2Distribution) / 100;
        int stat3 = (resValues[recipe.Stat3] * recipe.Stat3Distribution) / 100;

        //TODO no idea what goes on here yet.

        int durability = (int)(itemTemplate.MaxDurability * (resValues[3] / 1000));
        itemToReturn.SetDurability(durability);

        long itemID = await itemManager.SaveNewItemAsync(itemToReturn);
        if (itemID > 0)
        {
            itemToReturn.SetItemID((int)itemID);
            character.OnPickupItem(itemToReturn);
            bool isSaved = await inventoryManager.AddItemToInventoryAsync(character.GetCharacterID(), (int)itemID, 0);
            return itemToReturn;
        }
        else
        {
            Debug.LogError("Failed to save crafted Armour to database. Destroying instance.");
            Destroy(itemToReturn.gameObject);
            return null;
        }
    }
    public async Task<Item> CraftWeapon(PlayerStatBlock character, Recipe recipe, Resource[] resources, SubComponent[] components)
    {
        ItemTemplate itemTemplate = recipe.OutputItem;
        Item itemToReturn = MapTemplateToItem(itemTemplate);
        int[] resValues = GetFinalStatValueArray(resources, components);

        int stat1 = (resValues[recipe.Stat1] * recipe.Stat1Distribution) / 100; // 50%
        int stat2 = (resValues[recipe.Stat2] * recipe.Stat2Distribution) / 100; // 50%
        int stat3 = (resValues[recipe.Stat3] * recipe.Stat3Distribution) / 100; // 50%

        int weaponDamage = (int)(itemTemplate.Damage * ((stat1 + stat2) / 1000));
        int weaponSpeed = (int)(itemTemplate.Speed * ((stat1 + stat3) / 1000));
        itemToReturn.SetDamage(weaponDamage);
        itemToReturn.SetSpeed(weaponSpeed);

        int durability = (int)(itemTemplate.MaxDurability * (resValues[3] / 1000));
        itemToReturn.SetDurability(durability);


        long itemID = await itemManager.SaveNewItemAsync(itemToReturn);
        if (itemID > 0)
        {
            itemToReturn.SetItemID((int)itemID);
            character.OnPickupItem(itemToReturn);
            bool isSaved = await inventoryManager.AddItemToInventoryAsync(character.GetCharacterID(), (int)itemID, 0);
            return itemToReturn;
        }
        else
        {
            Debug.LogError("Failed to save crafted Armour to database. Destroying instance.");
            Destroy(itemToReturn.gameObject);
            return null;
        }
    }
    public async Task<Item> CraftArmour(PlayerStatBlock character, Recipe recipe, Resource[] resources, SubComponent[] components)
    {
        ItemTemplate itemTemplate = recipe.OutputItem;
        Item itemToReturn = MapTemplateToItem(itemTemplate);
        int[] resValues = GetFinalStatValueArray(resources, components);

        int stat1 = (resValues[recipe.Stat1] * recipe.Stat1Distribution) / 100; // 50%
        int stat2 = (resValues[recipe.Stat2] * recipe.Stat2Distribution) / 100; // 25%
        int stat3 = (resValues[recipe.Stat3] * recipe.Stat3Distribution) / 100; // 25%

        int slashResist = (int)(itemTemplate.SlashResist * ((stat1 + stat2 + stat3) / 1000));
        int thrustResist = (int)(itemTemplate.ThrustResist * ((stat1 + stat2 + stat3) / 1000));
        int crushResist = (int)(itemTemplate.CrushResist * ((stat1 + stat2 + stat3) / 1000));
        int heatResist = (int)(itemTemplate.HeatResist * ((stat1 + stat2 + stat3) / 1000));
        int shockResist = (int)(itemTemplate.ShockResist * ((stat1 + stat2 + stat3) / 1000));
        int coldResist = (int)(itemTemplate.ColdResist * ((stat1 + stat2 + stat3) / 1000));

        itemToReturn.SetArmourResists(new int[] { slashResist, thrustResist, crushResist, heatResist, shockResist, coldResist });

        int durability = (int)(itemTemplate.MaxDurability * (resValues[3] / 1000));
        itemToReturn.SetDurability(durability);

        long itemID = await itemManager.SaveNewItemAsync(itemToReturn);
        if (itemID > 0)
        {
            itemToReturn.SetItemID((int)itemID);
            character.OnPickupItem(itemToReturn);
            bool isSaved = await inventoryManager.AddItemToInventoryAsync(character.GetCharacterID(), (int)itemID, 0);
            return itemToReturn;
        }
        else
        {
            Debug.LogError("Failed to save crafted Armour to database. Destroying instance.");
            Destroy(itemToReturn.gameObject);
            return null;
        }
    }
    public async Task<Item> CraftTool(PlayerStatBlock character, Recipe recipe, Resource[] resources, SubComponent[] components)
    {
        ItemTemplate itemTemplate = recipe.OutputItem;
        Item itemToReturn = MapTemplateToItem(itemTemplate);
        int[] resValues = GetFinalStatValueArray(resources, components);

        int stat1 = (resValues[recipe.Stat1] * recipe.Stat1Distribution) / 100; // 50%
        int stat2 = (resValues[recipe.Stat2] * recipe.Stat2Distribution) / 100; // 25%
        int stat3 = (resValues[recipe.Stat3] * recipe.Stat3Distribution) / 100; // 25%



        int toolDamage = (int)(itemTemplate.Damage * ((stat1 + stat2 + stat3) / 1000));
        itemToReturn.SetDamage(toolDamage);

        int durability = (int)(itemTemplate.MaxDurability * (resValues[3] / 1000));
        itemToReturn.SetDurability(durability);

        long itemID = await itemManager.SaveNewItemAsync(itemToReturn);
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
    public async Task<SubComponent> CraftSubComponent(PlayerStatBlock character, Recipe recipe, Resource[] resources, SubComponent[] components)
    {
        SubComponentTemplate subCompTemplate = recipe.OutputSubComponent;
        if (subCompTemplate == null)
        {
            Debug.LogError("Recipe OutputSubComponent template is null.");
            return null;
        }

        SubComponent subComponentToReturn = Instantiate(PrefabLibrary.Instance.GetSubComponentPrefab());
        if (subComponentToReturn == null)
        {
            Debug.LogError("Failed to instantiate SubComponent prefab.");
            return null;
        }
        subComponentToReturn.Template = subCompTemplate;

        int[] resValues = GetFinalStatValueArray(resources, components);

        int finalQuality = resValues[0];
        int finalToughness = resValues[1];
        int finalStrength = resValues[2];
        int finalDensity = resValues[3];
        int finalAura = resValues[4];
        int finalEnergy = resValues[5];
        int finalProtein = resValues[6];
        int finalCarbohydrate = resValues[7];
        int finalFlavour = resValues[8];


        subComponentToReturn.SetSubComponent(
            0,
            subCompTemplate.Name,
            subCompTemplate.ComponentTemplateID,
            (int)subCompTemplate.ComponentType,
            finalQuality,
            finalToughness,
            finalStrength,
            finalDensity,
            finalAura,
            finalEnergy,
            finalProtein,
            finalCarbohydrate,
            finalFlavour
        );
        
        long subComponentDbId = await itemManager.SaveNewSubComponentAsync(subComponentToReturn);
        if (subComponentDbId > 0)
        {
            subComponentToReturn.SetSubComponentID((int)subComponentDbId);
            return subComponentToReturn;
        }
        else
        {
            Debug.LogError("Failed to save crafted subcomponent to database. Destroying instance.");
            Destroy(subComponentToReturn.gameObject);
            return null;
        }
    }

    #region Helpers
    public int[] GetFinalStatValueArray(Resource[] resources, SubComponent[] components)
    {
        int resCount = resources.Length;
        int componentCount = components.Length;

        int totalQuality = 0;
        int totalToughness = 0;
        int totalStrength = 0;
        int totalDensity = 0;
        int totalAura = 0;
        int totalEnergy = 0;
        int totalProtein = 0;
        int totalCarbohydrate = 0;
        int totalFlavour = 0;

        for (int i = 0; i < resources.Length; i++)
        {
            totalQuality += resources[i].Quality;
            totalToughness += resources[i].Toughness;
            totalStrength += resources[i].Strength;
            totalDensity += resources[i].Density;
            totalAura += resources[i].Aura;
            totalEnergy += resources[i].Energy;
            totalProtein += resources[i].Protein;
            totalCarbohydrate += resources[i].Carbohydrate;
            totalFlavour += resources[i].Flavour;
        }
        for (int i = 0; i < components.Length; i++)
        {
            totalQuality += components[i].Quality;
            totalToughness += components[i].Toughness;
            totalStrength += components[i].Strength;
            totalDensity += components[i].Density;
            totalAura += components[i].Aura;
            totalEnergy += components[i].Energy;
            totalProtein += components[i].Protein;
            totalCarbohydrate += components[i].Carbohydrate;
            totalFlavour += components[i].Flavour;
        }
        int finalQuality = totalQuality / (resCount + componentCount);
        int finalToughness = totalToughness / (resCount + componentCount);
        int finalStrength = totalStrength / (resCount + componentCount);
        int finalDensity = totalDensity / (resCount + componentCount);
        int finalAura = totalAura / (resCount + componentCount);
        int finalEnergy = totalEnergy / (resCount + componentCount);
        int finalProtein = totalProtein / (resCount + componentCount);
        int finalCarbohydrate = totalCarbohydrate / (resCount + componentCount);
        int finalFlavour = totalFlavour / (resCount + componentCount);
        return new int[] { finalQuality, finalToughness, finalStrength, finalDensity, finalAura, finalEnergy, finalProtein, finalCarbohydrate, finalFlavour };
    }
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
    /*
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
    */
    public Item MapTemplateToItem(ItemTemplate itemTemplate)
    {
        Item item = Instantiate(PrefabLibrary.Instance.GetItemPrefab());
        item.SetItem(0, itemTemplate.ItemTemplateID, itemTemplate.ItemName, (int)itemTemplate.Type, itemTemplate.MaxDurability, itemTemplate.MaxDurability, itemTemplate.Damage, itemTemplate.Speed, (int)itemTemplate.WeaponType,
            (int)itemTemplate.Slot, itemTemplate.SlashResist, itemTemplate.ThrustResist, itemTemplate.CrushResist, itemTemplate.HeatResist, itemTemplate.ShockResist, itemTemplate.ColdResist,
            itemTemplate.MindResist, itemTemplate.CorruptResist, itemTemplate.Icon, itemTemplate.ColourHex,
            itemTemplate.Weight, itemTemplate.Model,
            itemTemplate.IsStackable, itemTemplate.StackSizeMax, itemTemplate.Price);
        return item;
    }
    #endregion
}
