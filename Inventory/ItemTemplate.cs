using UnityEngine;
using System; // For NonSerialized

public class ItemTemplate : MonoBehaviour // Or ScriptableObject
{
    public int ItemTemplateID { get; private set; }
    public string ItemName { get; private set; } = "Default Item Name";
    public ItemType Type { get; private set; } = ItemType.Other;
    public string ExamineText { get; private set; } = "An ordinary item.";
    public int MaxDurability { get; private set; } = 100;
    public float Damage { get; private set; } = 0f;
    public float Speed { get; private set; } = 1.0f;
    public ItemDamageType DmgType { get; private set; } = ItemDamageType.Blunt;
    public ItemSlotType Slot { get; private set; } = ItemSlotType.None;
    public float SlashResist { get; private set; } = 0f;
    public float ThrustResist { get; private set; } = 0f;
    public float CrushResist { get; private set; } = 0f;
    public float HeatResist { get; private set; } = 0f;
    public float ShockResist { get; private set; } = 0f;
    public float ColdResist { get; private set; } = 0f;
    public float MindResist { get; private set; } = 0f;
    public float CorruptResist { get; private set; } = 0f;
    public int IconPath { get; private set; } = 0;
    [NonSerialized] public Sprite IconSprite; 
    public string ColourHex { get; private set; } = "#FFFFFF";
    [NonSerialized] public Color ItemColor = Color.white; 
    public int Weight { get; private set; } = 1;
    public int ModelPath { get; private set; } = 0;
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
    public void SetItemTemplate(int id, string name, int itemType, string examineText, int maxDurability, float damage, float speed, int damageType, int slotType, float slashRes, float thrustRes, float crushRes, float heatRes, 
        float shockRes, float coldRes, float mindRes, float corruptRes, int icon, string colourHex, int weight, int model, 
        int bonus1, int bonus2, int bonus3, int bonus4, int bonus5, int bonus6, int bonus7, int bonus8, int bonus1Type, int bonus2Type, int bonus3Type, int bonus4Type, int bonus5Type, int bonus6Type, int bonus7Type, int bonus8Type,
        bool stackable, int stackSizeMax,int price
        )
    {
        ItemTemplateID = id;
        ItemName = name;
        Type = Enum.IsDefined(typeof(ItemType), itemType) ? (ItemType)itemType : ItemType.Other;
        ExamineText = examineText;
        MaxDurability = maxDurability;
        Damage = damage;
        Speed = speed;
        DmgType = Enum.IsDefined(typeof(ItemDamageType), damageType) ? (ItemDamageType)damageType : ItemDamageType.Blunt;
        Slot = Enum.IsDefined(typeof(ItemSlotType), slotType) ? (ItemSlotType)slotType : ItemSlotType.None;

        SlashResist = slashRes; 
        ThrustResist = thrustRes; 
        CrushResist = crushRes;
        HeatResist = heatRes; 
        ShockResist = shockRes; 
        ColdResist = coldRes;
        MindResist = mindRes; 
        CorruptResist = corruptRes;

        IconPath = icon;
        ColourHex = colourHex ?? "#FFFFFF";
        ColorUtility.TryParseHtmlString(ColourHex, out ItemColor);
        // TODO: Load Sprite from IconPath

        Weight = weight;
        ModelPath = model;
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

    public void SetItemTemplateID(int id)
    {
        if (ItemTemplateID <= 0) { ItemTemplateID = id; }
        else { Debug.LogWarning($"Attempted to change ItemTemplateID for '{ItemName}' from {ItemTemplateID} to {id}"); }
    }
}