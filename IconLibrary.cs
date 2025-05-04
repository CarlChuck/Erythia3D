using System.Collections.Generic;
using UnityEngine;

public class IconLibrary : MonoBehaviour
{
    [SerializeField] private List<Sprite> icons;

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

    public Sprite GetIconByID(int iconID)
    {
        if (iconID >= 0 && iconID < icons.Count)
        {
            return icons[iconID];
        }
        else
        {
            Debug.LogWarning($"IconLibrary: Requested icon ID {iconID} is out of bounds (Library size: {icons.Count}).");
            return null;
        }
    }

    public Sprite GetIconByResourceType(ResourceType resourceType)
    {
        // TODO: Implement this
        return null;
    }
}
