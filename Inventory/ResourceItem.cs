using JetBrains.Annotations;
using UnityEngine;

public class ResourceItem : MonoBehaviour
{
    [SerializeField] private Resource resource;
    [SerializeField] private int currentStackSize;
    public float Weight { get; private set; } = 1.0f;
    public int StackSizeMax { get; private set; } = 1;
    public int Price { get; private set; } = 0;
    
    private int databaseID = 0; // Stores the ResourceItemID from the database

    public Resource Resource => resource;
    public int CurrentStackSize => currentStackSize;

    public void Initialize(Resource resource, int quantity = 1)
    {
        this.resource = resource;
        currentStackSize = quantity;
        StackSizeMax = resource.ResourceTemplate?.StackSizeMax ?? 100000;
        Weight = resource.Weight;
        Price = resource.Quality * 10; // Example pricing based on quality
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
        Weight = (currentStackSize * resource.Weight) / 10;
    }

    /// <summary>
    /// Sets the unique database ID for this resource item instance.
    /// Should generally only be called once during loading.
    /// </summary>
    /// <param name="id">The ResourceItemID from the database.</param>
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

    /// <summary>
    /// Gets the unique database ID (ResourceItemID) associated with this instance.
    /// </summary>
    /// <returns>The database ID, or 0 if not set.</returns>
    public int GetDatabaseID()
    {
        return databaseID;
    }
}
