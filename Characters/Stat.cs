using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class Stat : MonoBehaviour
{
    private int baseValue;
    private List<int> modifiers;
    private int maxPossible;
    private StatType statType;

    private void Awake()
    {
        modifiers = new List<int>();
    }
    public void InitialiseStat(int start, int max, StatType sType)
    {
        maxPossible = max;
        SetBaseValue(start);
        statType = sType;
    }
    public void SetBaseValue(int newValue)
    {
        if (baseValue > maxPossible)
        {
            baseValue = maxPossible;
        }
        baseValue = newValue;
    }
    public void AddToBaseValue(int valueToAdd)
    {
        if (baseValue + valueToAdd > maxPossible)
        {
            baseValue = maxPossible;
        }
        else
        {
            baseValue += valueToAdd;
        }
    }
    public void AddModifier(int modifierToAdd)
    {
        modifiers[modifierToAdd] = modifierToAdd;
    }
    public int GetStatBaseValue()
    {
        return baseValue;
    }
    public int GetStatValue()
    {
        int modifiedValue = baseValue;
        foreach (int modifier in modifiers)
        {
            modifiedValue += modifier;
        }
        return modifiedValue;
    }
    public StatType GetStatType() 
    { 
        return statType; 
    }
}
