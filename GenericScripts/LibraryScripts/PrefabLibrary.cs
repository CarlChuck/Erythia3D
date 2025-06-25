using UnityEngine;

public class PrefabLibrary : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private Item itemPrefab;
    [SerializeField] private ItemTemplate itemTemplatePrefab;
    [SerializeField] private SubComponentTemplate subComponentTemplatePrefab;
    [SerializeField] private Resource resourcePrefab;
    [SerializeField] private ResourceItem resourceItemPrefab;
    [SerializeField] private SubComponent subComponentPrefab;

    #region Singleton
    public static PrefabLibrary Instance { get; private set; }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    public Item GetItemPrefab()
    {
        return itemPrefab;
    }
    public ResourceItem GetResourceItemPrefab()
    {
        return resourceItemPrefab;
    }
    public SubComponent GetSubComponentPrefab()
    {
        return subComponentPrefab;
    }
    public ItemTemplate GetItemTemplatePrefab()
    {
        return itemTemplatePrefab;
    }
    public SubComponentTemplate GetSubComponentTemplatePrefab()
    {
        return subComponentTemplatePrefab;
    }
    public Resource GetResourcePrefab()
    {
        return resourcePrefab;   
    }
}
