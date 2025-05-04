using UnityEngine;
using Modules.Rendering.Outline; 

public class OutlineToggle : MonoBehaviour
{
    private OutlineComponent outlineComponent;

    void Awake()
    {
        FindOutlineComponent();
    }

    void OnEnable()
    {
        FindOutlineComponent();
    }

    void FindOutlineComponent()
    {
        outlineComponent = GetComponentInChildren<OutlineComponent>(true); // Include inactive children

        if (outlineComponent == null)
        {
            Debug.LogWarning($"OutlineToggle on '{gameObject.name}' could not find an OutlineComponent on itself or in its children.", this);
        }
    }

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
        }
    }
}