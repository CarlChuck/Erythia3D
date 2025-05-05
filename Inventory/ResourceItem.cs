using JetBrains.Annotations;
using System;
using UnityEngine;

public class ResourceItem : MonoBehaviour
{
    [SerializeField] private Resource resource;
    [SerializeField] private int currentStackSize;
    public float Weight { get; private set; } = 1.0f;
    public int StackSizeMax { get; private set; } = 1;
    public int Price { get; private set; } = 0;

    public Sprite IconSprite { get; private set; }

    private int databaseID = 0; // Stores the ResourceItemID from the database

    public Resource Resource => resource;
    public int CurrentStackSize => currentStackSize;

    public void Initialize(Resource resource, int quantity = 1)
    {
        this.resource = resource;
        currentStackSize = quantity;
        StackSizeMax = resource.GetResourceTemplate()?.StackSizeMax ?? 100000;
        Weight = (float)(currentStackSize * resource.Weight) / 100;
        Price = resource.Value * CurrentStackSize;
        IconSprite = IconLibrary.Instance.GetIconByResourceType(resource.Type);
    }

    public void UpdateStackSize(int stackToAdd = 0, int stackToRemove = 0)
    {
        currentStackSize += stackToAdd;
        currentStackSize -= stackToRemove;
        if (currentStackSize > StackSizeMax)
        {
            currentStackSize = StackSizeMax;
        }
        else if (currentStackSize < 0)
        {
            currentStackSize = 0;
        }
        Weight = (float)(currentStackSize * resource.Weight) / 100;
        Price = resource.Value * CurrentStackSize;
    }

    public void SetDatabaseID(int id)
    {
        if (databaseID == 0 && id > 0)
        {
            databaseID = id;
        }
        else if (id <= 0)
        {
             Debug.LogWarning($"Attempted to set invalid Database ID ({id}) for ResourceItem '{resource?.ResourceName ?? "Unknown"}'.", this);
        }
        else
        {
            Debug.LogWarning($"Attempted to change Database ID for ResourceItem '{resource?.ResourceName ?? "Unknown"}' from {databaseID} to {id}. This is usually not allowed.", this);
        }
    }

    public int GetDatabaseID()
    {
        return databaseID;
    }
    public string GetDescription()
    {
        //TODO: Add description logic
        return resource.ResourceName;
    }
}
