using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;

public class ResourceTemplate : MonoBehaviour
{
    public int ResourceTemplateID { get; protected set; }
    public string TemplateName { get; protected set; }
    public ResourceOrder Order { get; protected set; }
    public ResourceFamily Family { get; protected set; }
    public ResourceType Type { get; protected set; }
    public int Quality { get; protected set; } 
    public int Toughness { get; protected set; }
    public int Strength { get; protected set; }
    public int Density { get; protected set; }
    public int Aura { get; protected set; }
    public int Energy { get; protected set; }
    public int Protein { get; protected set; }
    public int Carbohydrate { get; protected set; }
    public int Flavour { get; protected set; }
    public int StackSizeMax { get; protected set; }
    public int Weight { get; protected set; }
    public int Value { get; protected set; }

    
    public void SetResourceTemplate(int id, string templateName, int order, int family, int type, int quality, int toughness, int strength, int density, int aura, int energy, int protein, int carbohydrate, int flavour, int stackSizeMax, int weight, int value)
    {
        ResourceTemplateID = id;
        TemplateName = templateName;
        Order = (ResourceOrder)order;
        Family = (ResourceFamily)family;
        Type = (ResourceType)type;
        Quality = quality;
        Toughness = toughness;
        Strength = strength;
        Density = density;
        Aura = aura;
        Energy = energy;
        Protein = protein;
        Carbohydrate = carbohydrate;
        Flavour = flavour;
        StackSizeMax = stackSizeMax;
        Weight = weight;
        Value = value;
    }
    public void SetResourceTemplateID(int id)
    {
        ResourceTemplateID = id;
    }
}