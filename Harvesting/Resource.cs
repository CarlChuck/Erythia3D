using System;
using UnityEngine;

public class Resource : MonoBehaviour // Assuming INT stats to match ResourceSpawns schema
{
    public int ResourceSpawnID { get; private set; } 
    public string ResourceName { get; private set; }
    public int ResourceTemplateID { get; private set; }
    public ResourceType Type { get; private set; } 
    public ResourceSubType SubType { get; private set; }
    public ResourceOrder Order { get; private set; } 
    public ResourceFamily Family { get; private set; } 
    public int Quality { get; private set; } 
    public int Toughness { get; private set; }
    public int Strength { get; private set; }
    public int Density { get; private set; }
    public int Aura { get; private set; }
    public int Energy { get; private set; }
    public int Protein { get; private set; }
    public int Carbohydrate { get; private set; }
    public int Flavour { get; private set; }
    public int Weight { get; private set; }
    public int Value { get; private set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public void SetResource(int id, string newName, int templateID, int type, int sType, int order, int family, int quality, int toughness, int strength, int density, int aura, int energy, int protein, int carbohydrate, int flavour, int weight, int value, DateTime startAt, DateTime endAt)
    {
        ResourceSpawnID = id;
        ResourceName = newName;
        if (newName == null)
        {
            ResourceName = "Broken Name";
        }
        ResourceTemplateID = templateID;
        Type = (ResourceType)type;
        SubType = (ResourceSubType)sType;
        Order = (ResourceOrder)order;
        Family = (ResourceFamily)order;
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

    public bool IsResourceInDate()
    {
        DateTime currentDate = DateTime.Now;
        if (currentDate >= StartDate && currentDate <= EndDate)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}