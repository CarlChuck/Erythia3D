using UnityEngine;
using System;

public class Item : MonoBehaviour
{
    public string ItemName { get; private set; } = "Default Item Name";
    public int ItemID { get; private set; }
    public int ItemTemplateID { get; private set; }
    public ItemType Type { get; private set; } = ItemType.Other;
    public int Durability { get; private set; } = 100;
    public int MaxDurability { get; private set; } = 100;
    public float Damage { get; private set; } = 0f;
    public float Speed { get; private set; } = 1.0f;
    public WeaponType WeaponType { get; private set; } = WeaponType.None;
    public ItemSlotType Slot { get; private set; } = ItemSlotType.None;
    public float SlashResist { get; private set; } = 0f;
    public float ThrustResist { get; private set; } = 0f;
    public float CrushResist { get; private set; } = 0f;
    public float HeatResist { get; private set; } = 0f;
    public float ShockResist { get; private set; } = 0f;
    public float ColdResist { get; private set; } = 0f;
    public float MindResist { get; private set; } = 0f;
    public float CorruptResist { get; private set; } = 0f;
    public int Icon { get; private set; } = 0;
    [NonSerialized] public Sprite IconSprite;
    public string ColourHex { get; private set; } = "#FFFFFF";
    [NonSerialized] public Color ItemColor = Color.white;
    public int Weight { get; private set; } = 1;
    public int Model { get; private set; } = 0;
    public bool IsStackable { get; private set; } = false;
    public int StackSizeMax { get; private set; } = 1;
    public int Price { get; private set; } = 0;
    public void SetItem(int id, int templateID, string itemName, int itemType, int durability, int maxDurability, float damage, float speed, int weaponType, int slotType, float slashRes, float thrustRes, float crushRes, float heatRes,
        float shockRes, float coldRes, float mindRes, float corruptRes, int icon, string colourHex, int weight, int model,
        bool stackable, int stackSizeMax, int price
        )
    {
        ItemID = id;
        ItemTemplateID = templateID;
        ItemName = itemName;
        Type = Enum.IsDefined(typeof(ItemType), itemType) ? (ItemType)itemType : ItemType.Other;
        Durability = durability;
        MaxDurability = maxDurability;
        Damage = damage;
        Speed = speed;
        WeaponType = Enum.IsDefined(typeof(WeaponType), weaponType) ? (WeaponType)weaponType : WeaponType.None;
        Slot = Enum.IsDefined(typeof(ItemSlotType), slotType) ? (ItemSlotType)slotType : ItemSlotType.None;

        SlashResist = slashRes;
        ThrustResist = thrustRes;
        CrushResist = crushRes;
        HeatResist = heatRes;
        ShockResist = shockRes;
        ColdResist = coldRes;
        MindResist = mindRes;
        CorruptResist = corruptRes;

        Icon = icon;
        ColourHex = colourHex ?? "#FFFFFF";
        ColorUtility.TryParseHtmlString(ColourHex, out ItemColor);
        // TODO: Load Sprite from IconPath

        Weight = weight;
        Model = model;
        // TODO: Load Model from ModelPath

        IsStackable = stackable;
        StackSizeMax = IsStackable ? stackSizeMax : 1;
        Price = price;
    }

    public void SetItemID(int id)
    {
        if (ItemID <= 0)
        {
            ItemID = id; 
            
        }
        else
        {
            Debug.LogWarning($"Attempted to change ItemID for '{ItemName}' from {ItemID} to {id}"); 
            
        }
    }
    public string GetDescription() 
    {
        //TODO: Add description logic
        return "No description available."; 
    }
    public void SetDamage(int damage)
    {
        Damage = damage;
    }
    public void SetSpeed(int speed)
    {
        Speed = speed;
    }
    public void SetArmourResists(int[] armourResists)
    {
        if (armourResists.Length != 6)
        {
            Debug.LogError("Armour resist array must have exactly 6 elements.");
            return;
        }
        SlashResist = armourResists[0];
        ThrustResist = armourResists[1];
        CrushResist = armourResists[2];
        HeatResist = armourResists[3];
        ShockResist = armourResists[4];
        ColdResist = armourResists[5];
    }
    public void SetDurability(int dura)
    {
        Durability = dura;
        MaxDurability = dura;
    }
}
public enum ItemType
{
    Cuirass,
    Greaves,
    Vambraces,
    Helm,
    Hauberk,
    Trousers,
    Sleeves,
    Coif,
    Neck,
    Waist,
    Back,
    Boots,
    Ear,
    Finger,
    PrimaryHand,
    SecondaryHand,
    Weapon1h,
    Weapon2h,
    Shield,
    MiningTool,
    WoodTool,
    HarvestingTool,
    Resource,
    Potion,
    Schematic,
    Other,
}
public enum ItemSlotType
{
    None,
    Head,
    Chest,
    Legs,
    Hands,
    Feet,
    Neck,
    Waist,
    Back,
    Finger,
    Ear,
    PrimaryHand,
    SecondaryHand,
    Tool
}
public enum WeaponType
{
    None,
    Dagger,
    Mace,
    CurvedSword,
    Shield,
    Sword,
    Axe,
    ShortSpear,
    Maul,
    GreatSword,
    GreatAxe,
    LongSpear,
    Bow,
}
public enum ItemDamageType
{
    Slashing,
    Piercing,
    Blunt,
    Heat,
    Cold,
    Electric,
    Corruption
}
public enum ItemBonusType
{
    None,
    Strength,
    Dexterity,
    Constitution,
    Intelligence,
    Spirit,
    Health,
    Mana,
    Damage,
    Defense,
    CritChance,
    AttackSpeed,
}
