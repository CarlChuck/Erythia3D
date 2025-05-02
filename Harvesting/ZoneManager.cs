using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks; // Add Task namespace
using System.Linq; // Add Linq namespace
using System; // For Exception

public class ZoneManager : MonoBehaviour
{
    [SerializeField] private Transform marketWaypoint;
    [SerializeField] private List<ResourceNode> activeNodes = new List<ResourceNode>();
    [SerializeField] private ResourceSubType regionResourceType; // Set in Inspector
    [SerializeField] private List<Resource> regionResources = new List<Resource>(); // List of resources for this region
    [SerializeField] private ResourceItem resourceItemPrefab;

    private bool isInitialized = false; 

    private async void Start()
    {
        isInitialized = false;
        Debug.Log("ZoneManager Start: Waiting for ResourceManager initialization...");

        // 1. Wait for ResourceManager to be ready
        while (!ResourceManager.Instance.GetIsInitialized())
        {
            Debug.LogWarning("ZoneManager waiting for ResourceManager data to load...");
            await Task.Yield(); // Wait a frame
        }
        Debug.Log("ResourceManager initialized. Proceeding with ZoneManager setup.");


        // 2. Find and register nodes (synchronous part)
        ResourceNode[] nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        activeNodes.Clear(); // Clear list before registering
        foreach (ResourceNode node in nodes)
        {
            RegisterNode(node);
        }
        Debug.Log($"Registered {activeNodes.Count} resource nodes.");

        // 3. Populate region resources (synchronous, uses already loaded data)
        PopulateRegionResources();

        // 4. Assign resources to nodes asynchronously (might involve DB writes)
        await AssignResourcesToNodesAsync();

        isInitialized = true;
        Debug.Log("ZoneManager Initialization Complete.");
    }
    private void PopulateRegionResources()
    {
        // ResourceManager instance check already happened in Start
        if (ResourceManager.Instance == null) return; // Should not happen if Start logic is correct

        regionResources.Clear(); // Clear previous list

        // Filter resources from ResourceManager's already loaded list
        // Use try-catch in case ResourceManager.Instance becomes null unexpectedly
        try
        {
            List<Resource> allSpawned = ResourceManager.Instance.GetAllResourceInstances();
            if (allSpawned != null)
            {
                // Use LINQ for cleaner filtering
                regionResources = allSpawned.Where(resource => resource != null && resource.Subtype == regionResourceType).ToList();
                Debug.Log($"Populated regionResources with {regionResources.Count} resources matching subtype {regionResourceType}.");
            }
            else
            {
                Debug.LogWarning("GetAllSpawnedResources returned null.");
            }

        }
        catch (Exception ex)
        {
            Debug.LogError($"Error accessing ResourceManager or filtering resources: {ex.Message}");
        }


    }
    private async Task<Resource> CreateNewResourceAsync(ResourceType type)
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("Cannot create resource, ResourceManager instance not found!");
            return null;
        }
        Debug.Log($"Attempting to spawn new resource of type {type} for region subtype {regionResourceType}...");
        try
        {
            // Await the async spawn method directly
            Resource newResource = await ResourceManager.Instance.SpawnResourceFromTemplateAsync(type, regionResourceType);

            if (newResource != null)
            {
                regionResources.Add(newResource); // Add the new resource to the local list
                Debug.Log($"Successfully spawned and added new resource: {newResource.ResourceName} (ID: {newResource.ResourceSpawnID})");
            }
            else
            {
                Debug.LogError($"Failed to spawn resource of type {type} from ResourceManager.");
            }
            return newResource;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during CreateNewResourceAsync for type {type}: {ex.Message}");
            return null;
        }
    }

    #region Node Management
    private async Task AssignResourcesToNodesAsync()
    {
        Debug.Log("Assigning resources to nodes...");

        // First, group nodes by their resource type
        var nodesByType = activeNodes.GroupBy(node => node.GetResourceType());

        foreach (var nodeGroup in nodesByType)
        {
            ResourceType type = nodeGroup.Key;
            Debug.Log($"Processing {nodeGroup.Count()} nodes of type {type}...");

            // Find existing resource of this type in our region
            Resource existingResource = regionResources.FirstOrDefault(r => r.Type == type);
            
            // If no existing resource, create one
            if (existingResource == null)
            {
                Debug.Log($"No existing resource found for type {type}. Creating new one...");
                existingResource = await CreateNewResourceAsync(type);
                if (existingResource == null)
                {
                    Debug.LogError($"Failed to create resource for type {type}. Skipping {nodeGroup.Count()} nodes.");
                    continue;
                }
            }

            // Assign the resource to all nodes in this group
            foreach (ResourceNode node in nodeGroup)
            {
                Debug.Log($"Assigning resource '{existingResource.ResourceName}' (ID: {existingResource.ResourceSpawnID}) to node '{node.gameObject.name}'.");
                node.SetResource(existingResource);
            }
        }

        Debug.Log("Finished assigning resources to nodes.");
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

    public bool GetIsInitialized()
    {
        return isInitialized;
    }
    #endregion
}