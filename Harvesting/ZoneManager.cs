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

    private bool isInitialized = false; // Add initialization flag

    private async void Start()
    {
        isInitialized = false;
        Debug.Log("ZoneManager Start: Waiting for ResourceManager initialization...");

        // 1. Wait for ResourceManager to be ready
        while (!ResourceManager.Instance.isInitialized)
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
            List<Resource> allSpawned = ResourceManager.Instance.GetAllSpawnedResources();
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

        foreach (ResourceNode node in activeNodes)
        {

            Resource currentResource = node.GetResource(); 

            if (currentResource == null || !regionResources.Contains(currentResource)) // Check if node needs a resource or if its current one isn't in the valid list
            {
                Debug.Log($"Node '{node.gameObject.name}' needs a resource assignment (Type: {node.GetResourceType()}). Searching...");
                Resource assignedResource = null;


                // If no suitable existing resource found, create a new one
                if (assignedResource == null)
                {
                    Debug.Log($"No existing suitable resource found for node '{node.gameObject.name}'. Creating new one...");
                    assignedResource = await CreateNewResourceAsync(node.GetResourceType());
                }

                // Assign the found or created resource to the node
                if (assignedResource != null)
                {
                    Debug.Log($"Assigning resource '{assignedResource.ResourceName}' (ID: {assignedResource.ResourceSpawnID}) to node '{node.gameObject.name}'.");
                    node.SetResource(assignedResource);
                }
                else
                {
                    Debug.LogError($"Failed to find or create a resource for node '{node.gameObject.name}' (Type: {node.GetResourceType()}). Node will remain empty.");
                    node.SetResource(null); // Ensure it's explicitly set to null if assignment fails
                }
            }
            else
            {
                Debug.Log($"Node '{node.gameObject.name}' already has a valid resource assigned: {currentResource.ResourceName}");
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
    #endregion
}
