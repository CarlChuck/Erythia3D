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
    public void SetItemTemplate(int id, string name, int itemType, string examineText, int maxDurability, float damage, float speed, int weaponType, int slotType, float slashRes, float thrustRes, float crushRes, float heatRes, 
        float shockRes, float coldRes, float mindRes, float corruptRes, int icon, string colourHex, int weight, int model, 
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

    public void SetItemTemplateID(int id)
    {
        if (ItemTemplateID <= 0) { ItemTemplateID = id; }
        else { Debug.LogWarning($"Attempted to change ItemTemplateID for '{ItemName}' from {ItemTemplateID} to {id}"); }
    }
}