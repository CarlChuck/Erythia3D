using System.Collections.Generic;
using UnityEngine;

public class ZoneManager : MonoBehaviour
{
    [SerializeField] private Transform marketWaypoint;
    [SerializeField] private List<ResourceNode> activeNodes = new List<ResourceNode>();
    [SerializeField] private ResourceSubType regionResourceType; // Set in Inspector
    [SerializeField] private List<Resource> regionResources = new List<Resource>(); // List of resources for this region
    [SerializeField] private ResourceItem resourceItemPrefab;

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

    private Resource CreateNewResource(ResourceType type)
    {
        Resource newResource = null;
        var task = ResourceManager.Instance.SpawnResourceFromTemplateAsync(type, regionResourceType);
        task.Wait(); // Block until the task is completed
        newResource = task.Result; // Retrieve the result of the task
        regionResources.Add(newResource); // Add the new resource to the regionResources list
        return newResource;
    }

    #region Node Management

    private void AssignResourcesToNodes()
    {
        foreach (ResourceNode node in activeNodes)
        {
            // Check if the node already has a resource assigned
            if (node.GetComponent<Resource>() == null)
            {
                Resource assignedResource = null;
                foreach (Resource resource in regionResources)
                {
                    if (resource.Type == node.GetResourceType())
                    {
                        assignedResource = resource;
                        break; // Assign the first matching resource
                    }
                }
                if (assignedResource == null)
                {
                    assignedResource = CreateNewResource(node.GetResourceType());
                }
                node.SetResource(assignedResource);
            }
        }
    }
    public void RegisterNode(ResourceNode node)
    {
        if (!activeNodes.Contains(node))
        {
            activeNodes.Add(node);
            node.SetResourceItemPrefab(resourceItemPrefab);
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
