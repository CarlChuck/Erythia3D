using System.Collections.Generic;
using UnityEngine;

public class ZoneManager : MonoBehaviour
{
    [SerializeField] private Transform marketWaypoint;
    [SerializeField] private List<ResourceNode> activeNodes = new List<ResourceNode>();
    [SerializeField] private ResourceSubType regionResourceType; // Set in Inspector
    private List<Resource> regionResources = new List<Resource>(); // List of resources for this region

    private void Start()
    {
        // Find all resource nodes in the scene
        ResourceNode[] nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);

        foreach (ResourceNode node in nodes)
        {
            RegisterNode(node);
        }

        // Populate the regionResources list
        PopulateRegionResources();
        AssignResourcesToNodes();
    }

    private void PopulateRegionResources()
    {
        // Ensure ResourceManager is available
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("ResourceManager instance not found!");
            return;
        }

        // Filter resources from ResourceManager's spawnedResources
        regionResources = ResourceManager.Instance.GetAllSpawnedResources().FindAll(resource => resource.Subtype == regionResourceType);

        Debug.Log($"Populated regionResources with {regionResources.Count} resources matching {regionResourceType}.");
    }

    private void AssignResourcesToNodes()
    {
        foreach (ResourceNode node in activeNodes)
        {
            // Check if the node already has a resource assigned
            if (node.GetComponent<Resource>() == null)
            {
                // Assign a random resource from the regionResources list
                int randomIndex = Random.Range(0, regionResources.Count);
                Resource assignedResource = regionResources[randomIndex];
                // Set the resource on the node
                node.SetResource(assignedResource);
            }
        }
    }

    #region Node Management
    public void RegisterNode(ResourceNode node)
    {
        if (!activeNodes.Contains(node))
        {
            activeNodes.Add(node);
            node.SetResourceSubType(regionResourceType);
        }
    }

    // Only needed for nodes under buildings
    public void UnregisterNode(ResourceNode node)
    {
        if (activeNodes.Contains(node))
        {
            activeNodes.Remove(node);
        }
    }
    #endregion

    #region Getters
    public Transform GetWaypoint()
    {
        return marketWaypoint;
    }

    public List<Resource> GetRegionResources()
    {
        return regionResources;
    }
    #endregion
}
