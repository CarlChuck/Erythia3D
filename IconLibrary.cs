using System.Collections.Generic;
using UnityEngine;

public class IconLibrary : MonoBehaviour
{
    [Header("Type-Specific Icons")]
    [Tooltip("List of icons where index matches the ResourceType enum integer value.")]
    [SerializeField] private List<Sprite> resourceIconsByType; 

    [Header("Order-Specific Icons")]
    [Tooltip("List of icons where index matches the ResourceOrder enum integer value (Mineral=0, Plant=1, etc.).")]
    [SerializeField] private List<Sprite> resourceIconsByOrder;

    [Header("General Icons by ID")]
    [Tooltip("List of general-purpose icons accessible by their index.")]
    [SerializeField] private List<Sprite> iconsById; // New list for general icons

    #region Singleton
    public static IconLibrary Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate IconLibrary found on {gameObject.name}. Destroying self.");
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    #endregion

    public Sprite GetIconByResourceType(ResourceType resourceType)
    {
        int iconIndex = (int)resourceType; // Direct mapping

        if (iconIndex >= 0 && iconIndex < resourceIconsByType.Count)
        {
            Sprite icon = resourceIconsByType[iconIndex];
            if (icon == null)
            {
                Debug.LogWarning($"IconLibrary: Icon entry for ResourceType '{resourceType}' (Index: {iconIndex}) is null in the list.");
            }
            return icon;
        }
        else
        {
            Debug.LogWarning($"IconLibrary: Requested ResourceType '{resourceType}' (Index: {iconIndex}) is out of bounds for resourceIconsByType list (Size: {resourceIconsByType.Count}).");
            // Optional Fallback: Return generic order icon?
            // return GetIconByResourceOrder(GetOrderForType(resourceType)); // Need helper
            return null;
        }
    }

    public Sprite GetIconByResourceOrder(ResourceOrder resourceOrder)
    {
        int iconIndex = (int)resourceOrder; // Direct mapping

        if (iconIndex >= 0 && iconIndex < resourceIconsByOrder.Count)
        {
            Sprite icon = resourceIconsByOrder[iconIndex];
             if (icon == null)
            {
                Debug.LogWarning($"IconLibrary: Icon entry for ResourceOrder '{resourceOrder}' (Index: {iconIndex}) is null in the list.");
            }
            return icon;
        }
        else
        {
            Debug.LogWarning($"IconLibrary: Requested ResourceOrder '{resourceOrder}' (Index: {iconIndex}) is out of bounds for resourceIconsByOrder list (Size: {resourceIconsByOrder.Count}).");
            return null;
        }
    }

    public Sprite GetIconByID(int iconID)
    {
        if (iconID >= 0 && iconID < iconsById.Count)
        {
            Sprite icon = iconsById[iconID];
            if (icon == null)
            {
                 Debug.LogWarning($"IconLibrary: Icon entry for ID {iconID} is null in the iconsById list.");
            }
            return icon;
        }
        else
        {
            Debug.LogWarning($"IconLibrary: Requested icon ID {iconID} is out of bounds for iconsById list (Size: {iconsById.Count}).");
            return null;
        }
    }
}