using UnityEngine;

public class EquipmentSlot : MonoBehaviour
{
    private Item itemInSlot;
    private ItemType slotType;

    public void SetSlot(ItemType iType, Item item = null)
    {
        slotType = iType;
        itemInSlot = item;
    }

    public void SetItem(Item itemForSlot)
    {
        itemInSlot = itemForSlot;
    }
    public Item GetItemInSlot()
    {
        return itemInSlot;
    }
    public ItemType GetSlotType()
    {
        return slotType;
    }
}