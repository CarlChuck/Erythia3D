using System;
using UnityEngine;

public class Resource : MonoBehaviour // Assuming INT stats to match ResourceSpawns schema
{
    public int ResourceSpawnID { get; set; } 
    public string ResourceName { get; set; }
    private int resourceTemplateID; // temp only needed if ResourceTemplate needs a delay to be added
    public ResourceTemplate ResourceTemplate { get; set; }
    public ResourceType Type { get; set; } 
    public ResourceSubType Subtype { get; set; }
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

    public void SetResource(int id, string name, int template, int type, int sType, int quality, int toughness, int strength, int density, int aura, int energy, int protein, int carbohydrate, int flavour, int weight, int value, DateTime startAt, DateTime endAt)
    {
        ResourceSpawnID = id;
        ResourceName = name;
        resourceTemplateID = template;
        Type = (ResourceType)type;
        Subtype = (ResourceSubType)sType;
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
        ResourceTemplate = newResourceTemplate;
    }
    public int GetResourceTemplateID()
    {
        return resourceTemplateID;
    }
    public ResourceType GetResourceType()
    {
        return Type;
    }
    public ResourceSubType GetResourceSubType()
    {
        return Subtype;
    }
}