using UnityEngine;
using System; // For NonSerialized

public class SubComponentTemplate : MonoBehaviour // Or just `public class SubComponentTemplate` if not a component
{
    public int ComponentTemplateID { get; private set; }
    public string Name { get; private set; }
    public int ComponentType { get; private set; } // Consider using an enum if types are predefined
    public int Icon { get; private set; }
    public string Colour { get; private set; }
    [NonSerialized] public Color ComponentColor = Color.white; // For runtime use
    public int Weight { get; private set; }
    public bool Stackable { get; private set; }
    public int StackSizeMax { get; private set; }
    public int Price { get; private set; }

    public void SetSubComponentTemplate(int templateID, string name, int componentType, int icon, string colour, int weight, bool stackable, int stackSizeMax, int price)
    {
        ComponentTemplateID = templateID;
        Name = name;
        ComponentType = componentType;
        Icon = icon;
        Colour = colour ?? "#FFFFFF";
        Weight = weight;
        Stackable = stackable;
        StackSizeMax = stackable ? stackSizeMax : 1; // Ensure non-stackable is size 1
        Price = price;

        // Attempt to parse color
        ColorUtility.TryParseHtmlString(Colour, out ComponentColor);

        // TODO: Load Icon Sprite based on Icon ID/Path if needed
    }
}