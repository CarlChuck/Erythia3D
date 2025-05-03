using UnityEngine;
using Modules.Rendering.Outline; // Ensure this namespace is correct

// Removed [ExecuteInEditMode]
public class OutlineToggle : MonoBehaviour
{
    private OutlineComponent outlineComponent; // Reference to the component to toggle

    // Removed isOutlineActive and previousState - state is now managed externally

    void Awake()
    {
        FindOutlineComponent(); // Find the component on awake
        // No need to set initial state here, ResourceNode.Start will handle it
    }

    void OnEnable()
    {
        // Re-find the component if the object is re-enabled
        FindOutlineComponent();
        // Potentially set default state if needed when re-enabled outside of Start sequence
        // if (outlineComponent != null) outlineComponent.enabled = false; // Optional: Default to off on enable
    }

    // Removed OnValidate()

    void FindOutlineComponent()
    {
        // Try to find the component on this object or its children
        outlineComponent = GetComponentInChildren<OutlineComponent>(true); // Include inactive children

        if (outlineComponent == null)
        {
            Debug.LogWarning($"OutlineToggle on '{gameObject.name}' could not find an OutlineComponent on itself or in its children.", this);
        }
    }

    // Removed ApplyInitialState()

    // Removed Update() with key press toggle (or keep for debugging if desired)

    // Removed ToggleOutline() - external scripts now call SetOutlineActive directly

    // This method is now the primary way to control the outline state
    public void SetOutlineActive(bool active)
    {
        if (outlineComponent == null)
        {
            // Attempt to find it again if it was missing initially
            FindOutlineComponent();
            if (outlineComponent == null)
            {
                // Still not found, log error and exit
                Debug.LogError($"OutlineToggle cannot set state: OutlineComponent not found on '{gameObject.name}' or its children.", this);
                return;
            }
        }

        // Only change state if it's different and the component is valid
        if (outlineComponent != null && outlineComponent.enabled != active)
        {
            outlineComponent.enabled = active;
            // Optional: Reduced logging spam
            // Debug.Log($"OutlineComponent on '{outlineComponent.gameObject.name}' {(active ? "Enabled" : "Disabled")}", this);
        }
    }
}