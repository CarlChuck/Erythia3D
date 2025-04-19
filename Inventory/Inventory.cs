using System; // For Action
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [SerializeField] private int bagspace;
    [SerializeField] private List<Item> items = new List<Item>();

    public event Action OnInventoryChanged;
    public void SetupInventory()
    {
        SetBagSpace(20);
        OnInventoryChanged?.Invoke(); // Initial update
    }
    public void ClearInventory()
    {
        items.Clear();
        OnInventoryChanged?.Invoke();
    }
    public bool AddItem(Item item)
    {
        if (item == null) return false;

        if (items.Count >= bagspace)
        {
            Debug.LogWarning("Inventory is full. Cannot add item: " + item.ItemName);
            // Optionally, trigger a "Inventory Full" message in UI
            return false;
        }
        items.Add(item);
        OnInventoryChanged?.Invoke(); // Notify listeners
        return true;
    }    
    // Try removing a specific item instance
    public bool RemoveItem(Item item)
    {
        if (item == null) return false;

        bool removed = items.Remove(item);
        if (removed)
        {
            OnInventoryChanged?.Invoke(); // Notify listeners
        }
        return removed;
    }

    // Get all items (useful for UI display)
    public List<Item> GetAllItems()
    {
        return items; // Return a copy if you want to prevent external modification: return new List<Item>(items);
    }

    public Item GetItem(int index)
    {
        if (index >= 0 && index < items.Count)
        {
            return items[index];
        }
        return null;
    }
    public Item GetItemByName(string itemName)
    {
        foreach (Item item in items)
        {
            if (item.ItemName == itemName)
            {
                return item;
            }
        }
        return null;
    }

    public int GetTotalWeight()
    {
        int totalWeight = 0;
        if (items.Count > 0)
        {
            foreach (Item item in items)
            {
                totalWeight += (int)item.Weight;
            }
        }
        return totalWeight;
    }
    public void SetBagSpace(int newBagSpace)
    {
        bagspace = newBagSpace;
    }
    public int GetBagSpace()
    {
        return bagspace;
    }
    public bool IsFull() 
    { 
        return items.Count >= bagspace; 
    }
}
