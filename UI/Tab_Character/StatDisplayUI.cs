using UnityEngine;
using TMPro;

public class StatDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text statNameText;
    [SerializeField] private TMP_Text statValueText;

    private Stat linkedStat; // Reference to the actual Stat data

    // Call this after instantiating the prefab
    public void Setup(string displayName, Stat statToLink)
    {
        if (statNameText != null)
        {
            statNameText.text = displayName + ":"; // Add colon for clarity
        }
        linkedStat = statToLink;
        UpdateValue(); // Set initial value
    }

    // Call this to refresh the displayed value
    public void UpdateValue()
    {
        if (linkedStat != null && statValueText != null)
        {
            statValueText.text = linkedStat.GetStatValue().ToString();
        }
        else if (statValueText != null)
        {
            statValueText.text = "N/A"; // Indicate if stat link is broken
        }
    }
}