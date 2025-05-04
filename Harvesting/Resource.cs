using System;
using UnityEngine;

public class Resource : MonoBehaviour // Assuming INT stats to match ResourceSpawns schema
{
    public int ResourceSpawnID { get; set; } 
    public string ResourceName { get; set; }
    private int resourceTemplateID; // temp only needed if ResourceTemplate needs a delay to be added
    public ResourceTemplate resourceTemplate;
    public ResourceType Type { get; set; } 
    public ResourceSubType SubType { get; set; }
    public int Quality { get; set; } 
    public int Toughness { get; set; }
    public int Strength { get; set; }
    public int Density { get; set; }
    public int Aura { get; set; }
    public int Energy { get; set; }
    public int Protein { get; set; }
    public int Carbohydrate { get; set; }
    public int Flavour { get; set; }
    public int Weight { get; set; }
    public int Value { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public void SetResource(int id, string newName, ResourceTemplate template, int type, int sType, int quality, int toughness, int strength, int density, int aura, int energy, int protein, int carbohydrate, int flavour, int weight, int value, DateTime startAt, DateTime endAt)
    {
        ResourceSpawnID = id;
        ResourceName = newName;
        if (newName == null)
        {
            ResourceName = "Broken Name";
        }
        resourceTemplate = template;
        resourceTemplateID = template.ResourceTemplateID;
        Type = (ResourceType)type;
        SubType = (ResourceSubType)sType;
        Quality = quality;
        Toughness = toughness;
        Strength = strength;
        Density = density;
        Aura = aura;
        Energy = energy;
        Protein = protein;
        Carbohydrate = carbohydrate;
        Flavour = flavour;
        Weight = weight;
        Value = value;
        StartDate = startAt;
        EndDate = endAt;
    }
    public void SetResource(int id, string newName, int templateID, int type, int sType, int quality, int toughness, int strength, int density, int aura, int energy, int protein, int carbohydrate, int flavour, int weight, int value, DateTime startAt, DateTime endAt)
    {
        ResourceSpawnID = id;
        ResourceName = newName;
        if (newName == null)
        {
            ResourceName = "Broken Name";
        }
        resourceTemplateID = templateID;
        Type = (ResourceType)type;
        SubType = (ResourceSubType)sType;
        Quality = quality;
        Toughness = toughness;
        Strength = strength;
        Density = density;
        Aura = aura;
        Energy = energy;
        Protein = protein;
        Carbohydrate = carbohydrate;
        Flavour = flavour;
        Weight = weight;
        Value = value;
        StartDate = startAt;
        EndDate = endAt;
    }
    public void SetResourceTemplateID(ResourceTemplate newResourceTemplate)
    {
        resourceTemplate = newResourceTemplate;
    }
    public int GetResourceTemplateID()
    {
        return resourceTemplateID;
    }
    public ResourceTemplate GetResourceTemplate()
    {
        if (resourceTemplate == null)
        {
            resourceTemplate = ResourceManager.Instance.GetResourceTemplateById(resourceTemplateID);
            return resourceTemplate;
        }
        else
        {
            return resourceTemplate;
        }
    }
    public void SetResourceTemplate(ResourceTemplate newResourceTemplate)
    {
        resourceTemplate = newResourceTemplate;
    }
    public ResourceType GetResourceType()
    {
        return Type;
    }
    public ResourceSubType GetResourceSubType()
    {
        return SubType;
    }
    public ResourceOrder GetResourceOrder()
    {           
        return resourceTemplate.Order;
    }

}