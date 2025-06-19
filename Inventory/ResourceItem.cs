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

    public Resource Resource
    {
        get { return resource; }
    }

    public int CurrentStackSize
    {
        get { return currentStackSize; }
    }

    public void SetResourceItem(Resource resource, int stackSize, int quantity = 1)
    {
        this.resource = resource;
        currentStackSize = quantity;
        StackSizeMax = stackSize;
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
    public void SetStackSize(int stackSize)
    {
        if (stackSize > StackSizeMax)
        {
            stackSize = StackSizeMax;
        }
        else if (stackSize < 0)
        {
            stackSize = 0;
        }
        currentStackSize = stackSize;
        Weight = (float)(currentStackSize * resource.Weight) / 100;
        Price = resource.Value * CurrentStackSize;
    }
    public string GetDescription()
    {
        //TODO: Add description logic
        return resource.ResourceName;
    }
}
