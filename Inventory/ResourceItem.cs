using JetBrains.Annotations;
using UnityEngine;

public class ResourceItem : MonoBehaviour
{
    [SerializeField] private Resource resource;
    [SerializeField] private int currentStackSize;
    public float Weight { get; private set; } = 1.0f;
    public int StackSizeMax { get; private set; } = 1;
    public int Price { get; private set; } = 0;
    
    public Resource Resource => resource;
    public int CurrentStackSize => currentStackSize;

    public void Initialize(Resource resource, int quantity = 1)
    {
        this.resource = resource;
        currentStackSize = quantity;
        StackSizeMax = resource.ResourceTemplate?.StackSizeMax ?? 100;
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
}
