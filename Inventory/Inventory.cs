using System; // For Action
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [SerializeField] private int bagspace;
    [SerializeField] private List<Item> items = new List<Item>();
    [SerializeField] private List<ResourceItem> resourceItems = new List<ResourceItem>();

    public event Action OnInventoryChanged;
    public void SetupInventory()
    {
        SetBagSpace(20);
        OnInventoryChanged?.Invoke(); // Initial update
    }
    public void ClearInventory()
    {
        items.Clear();
        resourceItems.Clear();
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

    public bool AddResourceItem(ResourceItem resourceItem)
    {
        if (resourceItem == null) return false;

        // Check if we already have this resource type
        ResourceItem existingItem = resourceItems.Find(r => r.Resource == resourceItem.Resource);
        if (existingItem != null)
        {
            // Update stack size of existing item
            existingItem.UpdateStackSize(stackToAdd: resourceItem.CurrentStackSize);
        }
        else
        {
            if (resourceItems.Count >= bagspace)
            {
                Debug.LogWarning("Inventory is full. Cannot add resource item: " + resourceItem.Resource.ResourceName);
                return false;
            }
            resourceItems.Add(resourceItem);
        }
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

    public bool RemoveResourceItem(ResourceItem resourceItem)
    {
        if (resourceItem == null) return false;

        bool removed = resourceItems.Remove(resourceItem);
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

    public List<ResourceItem> GetAllResourceItems()
    {
        return resourceItems;
    }

    public Item GetItem(int index)
    {
        if (index >= 0 && index < items.Count)
        {
            return items[index];
        }
        return null;
    }

    public ResourceItem GetResourceItem(int index)
    {
        if (index >= 0 && index < resourceItems.Count)
        {
            return resourceItems[index];
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

    public ResourceItem GetResourceItemByResource(Resource resource)
    {
        return resourceItems.Find(r => r.Resource == resource);
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
        if (resourceItems.Count > 0)
        {
            foreach (ResourceItem resourceItem in resourceItems)
            {
                totalWeight += (int)resourceItem.Weight;
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
        return items.Count + resourceItems.Count >= bagspace; 
    }
}
