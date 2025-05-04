using UnityEngine;
using System; // For NonSerialized

// Represents an instance of a sub-component
public class SubComponent : MonoBehaviour // Or just public class SubComponent
{
    public int SubComponentID { get; private set; }
    public string Name { get; private set; } // Optional: Can override template name
    public int SubComponentTemplateID { get; private set; }
    [NonSerialized] public SubComponentTemplate Template; // Reference to the template
    public int ComponentType { get; private set; } // Can potentially differ from template?
    public int Quality { get; private set; }
    public int Toughness { get; private set; }
    public int Strength { get; private set; }
    public int Density { get; private set; }
    public int Aura { get; private set; }
    public int Energy { get; private set; }
    public int Protein { get; private set; }
    public int Carbohydrate { get; private set; }
    public int Flavour { get; private set; }
    public Sprite IconSprite { get; private set; }

    public void Initialize(int id, string name, int templateId, int componentType, int quality, int toughness, int strength, int density, int aura, int energy, int protein, int carbohydrate, int flavour)
    {
        SubComponentID = id;
        SubComponentTemplateID = templateId;
        Name = name; // Use instance-specific name if provided, otherwise template name might be used later
        ComponentType = componentType;
        Quality = quality;
        Toughness = toughness;
        Strength = strength;
        Density = density;
        Aura = aura;
        Energy = energy;
        Protein = protein;
        Carbohydrate = carbohydrate;
        Flavour = flavour;
        SetIconSprite();
    }

    // Optional: Method to set the ID after saving to DB if needed
    public void SetSubComponentID(int id)
    {
        if (SubComponentID <= 0) 
        { 
            SubComponentID = id; 
        }
        else 
        { 
            Debug.LogWarning($"Attempted to change SubComponentID for '{Name ?? "Unnamed"}' from {SubComponentID} to {id}"); 
        }
    }
    private void SetIconSprite()
    {
        IconSprite = IconLibrary.Instance.GetIconByID(Template.Icon);
    }
    public string GetDescription()
    {
        //TODO: Add description logic
        return Name;
    }
}