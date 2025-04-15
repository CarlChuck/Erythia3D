using UnityEngine;

public class Item : MonoBehaviour
{
    private int weight;
    private string itemName;
    private Sprite icon;
    private ItemType itemType;
    private int itemID;
    private ItemTemplate itemTemplate;

    public void CreateItem(string newItemName, ItemTemplate iTemplate, Sprite newIcon, int weightToSet, ItemType iType)
    {
        itemName = newItemName;
        itemTemplate = iTemplate;
        icon = newIcon;
        weight = weightToSet;
        itemType = iType;
    }

    public void SetWeight(int weightToSet)
    {
        weight = weightToSet;
    }

    #region Getters
    public string GetItemName()
    {
        return itemName;
    }

    public Sprite GetIcon()
    {
        return icon;
    }

    public int GetWeight()
    {
        return weight;
    }

    public ItemType GetItemType()
    {
        return itemType;
    }
    #endregion
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
    Other,
}