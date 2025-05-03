using UnityEngine;
using System;

public class Item : MonoBehaviour
{
    public string ItemName { get; private set; } = "Default Item Name";
    public int ItemID { get; private set; }
    public int ItemTemplateID { get; private set; }
    public ItemTemplate Template { get; set; }
    public ItemType Type { get; private set; } = ItemType.Other;
    public int Durability { get; private set; } = 100;
    public int MaxDurability { get; private set; } = 100;
    public float Damage { get; private set; } = 0f;
    public float Speed { get; private set; } = 1.0f;
    public ItemDamageType DamageType { get; private set; } = ItemDamageType.Blunt;
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
    public int Bonus1 { get; private set; } = 0;
    public int Bonus2 { get; private set; } = 0;
    public int Bonus3 { get; private set; } = 0;
    public int Bonus4 { get; private set; } = 0;
    public int Bonus5 { get; private set; } = 0;
    public int Bonus6 { get; private set; } = 0;
    public int Bonus7 { get; private set; } = 0;
    public int Bonus8 { get; private set; } = 0;
    public ItemBonusType Bonus1Type { get; private set; } = ItemBonusType.None;
    public ItemBonusType Bonus2Type { get; private set; } = ItemBonusType.None;
    public ItemBonusType Bonus3Type { get; private set; } = ItemBonusType.None;
    public ItemBonusType Bonus4Type { get; private set; } = ItemBonusType.None;
    public ItemBonusType Bonus5Type { get; private set; } = ItemBonusType.None;
    public ItemBonusType Bonus6Type { get; private set; } = ItemBonusType.None;
    public ItemBonusType Bonus7Type { get; private set; } = ItemBonusType.None;
    public ItemBonusType Bonus8Type { get; private set; } = ItemBonusType.None;
    public bool IsStackable { get; private set; } = false;
    public int StackSizeMax { get; private set; } = 1;
    public int Price { get; private set; } = 0;
    public void SetItem(int id, int templateID, string name, int itemType, int durability, int maxDurability, float damage, float speed, int damageType, int slotType, float slashRes, float thrustRes, float crushRes, float heatRes,
        float shockRes, float coldRes, float mindRes, float corruptRes, int icon, string colourHex, int weight, int model,
        int bonus1, int bonus2, int bonus3, int bonus4, int bonus5, int bonus6, int bonus7, int bonus8, int bonus1Type, int bonus2Type, int bonus3Type, int bonus4Type, int bonus5Type, int bonus6Type, int bonus7Type, int bonus8Type,
        bool stackable, int stackSizeMax, int price
        )
    {
        ItemID = id;
        ItemTemplateID = templateID;
        ItemName = name;
        Type = Enum.IsDefined(typeof(ItemType), itemType) ? (ItemType)itemType : ItemType.Other;
        Durability = durability;
        MaxDurability = maxDurability;
        Damage = damage;
        Speed = speed;
        DamageType = Enum.IsDefined(typeof(ItemDamageType), damageType) ? (ItemDamageType)damageType : ItemDamageType.Blunt;
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

        Bonus1 = bonus1; Bonus2 = bonus2; Bonus3 = bonus3; Bonus4 = bonus4;
        Bonus5 = bonus5; Bonus6 = bonus6; Bonus7 = bonus7; Bonus8 = bonus8;

        Bonus1Type = Enum.IsDefined(typeof(ItemBonusType), bonus1Type) ? (ItemBonusType)bonus1Type : ItemBonusType.None;
        Bonus2Type = Enum.IsDefined(typeof(ItemBonusType), bonus2Type) ? (ItemBonusType)bonus2Type : ItemBonusType.None;
        Bonus3Type = Enum.IsDefined(typeof(ItemBonusType), bonus3Type) ? (ItemBonusType)bonus3Type : ItemBonusType.None;
        Bonus4Type = Enum.IsDefined(typeof(ItemBonusType), bonus4Type) ? (ItemBonusType)bonus4Type : ItemBonusType.None;
        Bonus5Type = Enum.IsDefined(typeof(ItemBonusType), bonus5Type) ? (ItemBonusType)bonus5Type : ItemBonusType.None;
        Bonus6Type = Enum.IsDefined(typeof(ItemBonusType), bonus6Type) ? (ItemBonusType)bonus6Type : ItemBonusType.None;
        Bonus7Type = Enum.IsDefined(typeof(ItemBonusType), bonus7Type) ? (ItemBonusType)bonus7Type : ItemBonusType.None;
        Bonus8Type = Enum.IsDefined(typeof(ItemBonusType), bonus8Type) ? (ItemBonusType)bonus8Type : ItemBonusType.None;

        IsStackable = stackable;
        StackSizeMax = IsStackable ? stackSizeMax : 1;
        Price = price;
    }

    public void SetItemID(int id)
    {
        if (ItemID <= 0) { ItemID = id; }
        else { Debug.LogWarning($"Attempted to change ItemID for '{ItemName}' from {ItemID} to {id}"); }
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
