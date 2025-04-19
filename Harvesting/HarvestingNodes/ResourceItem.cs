using JetBrains.Annotations;
using UnityEngine;

public class ResourceItem : Item
{
    private int stackSizeMax;
    private int currentStackSize;
    private Resource resource;


    public void UpdateStackSize(int stackToAdd = 0, int stackToRemove = 0)
    {
        currentStackSize += stackToAdd;
        currentStackSize -= stackToRemove;
        if (currentStackSize > stackSizeMax)
        {
            currentStackSize = stackSizeMax;
        }
        else if (currentStackSize < 0)
        {
            currentStackSize = 0;
        }
       // Weight = (currentStackSize * resource.Weight)/10;
    }
}
