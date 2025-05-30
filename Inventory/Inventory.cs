using System; // For Action
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [SerializeField] private int bagspace;
    [SerializeField] private List<Item> items = new List<Item>();
    [SerializeField] private List<ResourceItem> resourceItems = new List<ResourceItem>();
    [SerializeField] private List<SubComponent> subComponents = new List<SubComponent>();

    public event Action OnInventoryChanged;

    #region Setup
    public void SetupInventory()
    {
        SetBagSpace(20);
        items.Clear();
        resourceItems.Clear();
        subComponents.Clear();
        OnInventoryChanged?.Invoke(); // Initial update
    }
    #endregion
    public void ClearInventory()
    {
        items.Clear();
        resourceItems.Clear();
        subComponents.Clear();
        OnInventoryChanged?.Invoke();
    }
    public bool AddItem(Item item)
    {
        if (item == null) 
        {
            return false;
        }

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
        if (resourceItem != null)
        {
            resourceItem.transform.SetParent(transform);
            resourceItems.Add(resourceItem);
            OnInventoryChanged?.Invoke(); // Notify listeners
            return true;
        }
        else
        {
            Debug.LogWarning("Resource item is null. Cannot add to inventory.");
            return false;
        }
    }

    public bool AddSubComponent(SubComponent subComponent)
    {
        if (subComponent == null)
        {
            Debug.LogWarning("Attempted to add a null SubComponent.");
            return false;
        }

        if (IsFull()) // Use IsFull() which now checks all types
        {
            Debug.LogWarning("Inventory is full. Cannot add SubComponent: " + subComponent.Name);
            return false;
        }

        subComponent.transform.SetParent(transform); // Assume SubComponent is a MonoBehaviour and parent it
        subComponents.Add(subComponent);
        OnInventoryChanged?.Invoke(); // Notify listeners
        return true;
    }

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

    public bool RemoveSubComponent(SubComponent subComponent)
    {
        if (subComponent == null) return false;

        bool removed = subComponents.Remove(subComponent);
        if (removed)
        {
            OnInventoryChanged?.Invoke(); // Notify listeners
        }
        return removed;
    }

    public bool UpdateResourceQuantity(ResourceItem itemToUpdate, int amountToAdd = 0, int amountToRemove = 0)
    {
        // Ensure the item actually belongs to this inventory list
        if (itemToUpdate != null && resourceItems.Contains(itemToUpdate))
        {
            itemToUpdate.UpdateStackSize(amountToAdd, amountToRemove);
            // If stack becomes empty after update, consider removing it (optional, depends on design)
            // if (itemToUpdate.CurrentStackSize <= 0) { RemoveResourceItem(itemToUpdate); }
            
            OnInventoryChanged?.Invoke(); // Notify UI that something changed
            return true;
        }
        else
        {
            Debug.LogWarning($"UpdateResourceQuantity: Item '{itemToUpdate?.Resource?.ResourceName ?? "NULL"}' not found in this inventory.");
            return false;
        }
    }

    public bool IsFull() 
    { 
        return items.Count + resourceItems.Count + subComponents.Count >= bagspace; 
    }

    #region Getters
    public int GetBagSpace()
    {
        return bagspace;
    }
    public List<Item> GetAllItems()
    {
        return items; // Return a copy if you want to prevent external modification: return new List<Item>(items);
    }
    public List<ResourceItem> GetAllResourceItems()
    {
        return resourceItems;
    }
    public List<SubComponent> GetAllSubComponents()
    {
        return subComponents;
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
    public SubComponent GetSubComponent(int index)
    {
        if (index >= 0 && index < subComponents.Count)
        {
            return subComponents[index];
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
    #endregion

    #region Setters 
    public void SetBagSpace(int newBagSpace)
    {
        bagspace = newBagSpace;
    }
    #endregion
}