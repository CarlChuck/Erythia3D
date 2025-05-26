using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks; 
using System.Linq;
using System;
using Unity.Netcode;

/// <summary>
/// Server-only NetworkBehaviour that manages zone-specific gameplay coordination.
/// Should be spawned as a prefab by ServerManager when zones are loaded.
/// Handles resource node management, resource assignment, and zone interactions.
/// </summary>
public class ZoneManager : NetworkBehaviour
{
    #region Constants
    private const float initializationDelay = 1f; // Delay to ensure scene objects are ready
    #endregion

    #region Private Fields
    [SerializeField] private Transform marketWaypoint;
    [SerializeField] private List<ResourceNode> activeNodes = new List<ResourceNode>();
    [SerializeField] private ResourceSubType regionResourceType; // Set via initialization
    [SerializeField] private List<Resource> regionResources = new List<Resource>(); 
    [SerializeField] private ResourceItem resourceItemPrefab;

    private bool isInitialized = false;
    private string zoneName = string.Empty;
    private ServerManager serverManager;
    #endregion

    #region Public Properties
    public bool IsInitialized => isInitialized;
    public string ZoneName => zoneName;
    public ResourceSubType RegionResourceType => regionResourceType;
    #endregion

    #region NetworkBehaviour Lifecycle
    public override void OnNetworkSpawn()
    {
        // Ensure this ZoneManager is server-only
        // Note: Server-only visibility should be configured by ServerManager before spawning
        if (!IsServer)
        {
            Debug.LogError("ZoneManager: NetworkBehaviour spawned on client! This should be server-only.");
            return;
        }

        if (Application.isEditor || Debug.isDebugBuild)
        {
            Debug.Log($"ZoneManager: NetworkObject spawned on server for zone initialization");
        }

        // Start initialization after a small delay to ensure scene objects are ready
        StartCoroutine(DelayedInitialization());
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            CleanupZone();
            
            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.Log($"ZoneManager: NetworkObject despawned for zone '{zoneName}'");
            }
        }
    }

    /// <summary>
    /// Called by ServerManager to initialize this ZoneManager for a specific zone
    /// </summary>
    public void InitializeForZone(string targetZoneName, ResourceSubType targetResourceType, ServerManager targetServerManager)
    {
        if (!IsServer)
        {
            Debug.LogError("ZoneManager: InitializeForZone called on client! This should only be called on server.");
            return;
        }

        zoneName = targetZoneName;
        regionResourceType = targetResourceType;
        serverManager = targetServerManager;

        if (Application.isEditor || Debug.isDebugBuild)
        {
            Debug.Log($"ZoneManager: Initialized for zone '{zoneName}' with resource type '{regionResourceType}'");
        }
    }


    #endregion

    #region Initialization
    private System.Collections.IEnumerator DelayedInitialization()
    {
        // Wait for scene objects to be fully loaded and available
        yield return new WaitForSeconds(initializationDelay);
        
        // Start the async initialization
        _ = InitializeZoneAsync();
    }

    private async Task InitializeZoneAsync()
    {
        if (!IsServer)
        {
            Debug.LogError("ZoneManager: InitializeZoneAsync called on client!");
            return;
        }

        try
        {
            isInitialized = false;

            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.Log($"ZoneManager: Starting initialization for zone '{zoneName}'");
            }

            // Find and register all ResourceNodes in the scene
            await DiscoverAndRegisterNodesAsync();

            // Populate region resources from ResourceManager
            PopulateRegionResources();

            // Assign resources to nodes
            await AssignResourcesToNodesAsync();

            isInitialized = true;

            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.Log($"ZoneManager: Initialization complete for zone '{zoneName}' with {activeNodes.Count} nodes");
            }

            // Notify ServerManager that zone initialization is complete
            NotifyInitializationComplete();
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneManager: Error during initialization for zone '{zoneName}': {ex.Message}");
            isInitialized = false;
        }
    }

    private async Task DiscoverAndRegisterNodesAsync()
    {
        // Wait one frame to ensure all scene objects are instantiated
        await Task.Yield();

        // Discover MarketWaypoint in the scene
        DiscoverMarketWaypoint();

        // Discover and register ResourceNodes
        ResourceNode[] nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        activeNodes.Clear();
        
        foreach (ResourceNode node in nodes)
        {
            RegisterNode(node);
        }
        
        if (Application.isEditor || Debug.isDebugBuild)
        {
            Debug.Log($"ZoneManager: Registered {activeNodes.Count} resource nodes in zone '{zoneName}'");
        }
    }

    private void DiscoverMarketWaypoint()
    {
        // Find MarketWaypoint by name (each scene should have one with this exact name)
        GameObject waypointObject = GameObject.Find("MarketWaypoint");
        
        if (waypointObject != null)
        {
            marketWaypoint = waypointObject.transform;
            
            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.Log($"ZoneManager: Found MarketWaypoint at position {marketWaypoint.position} in zone '{zoneName}'");
            }
        }
        else
        {
            Debug.LogWarning($"ZoneManager: MarketWaypoint not found in zone '{zoneName}'. This zone may not have a market waypoint.");
            marketWaypoint = null;
        }
    }

    private void NotifyInitializationComplete()
    {
        if (serverManager != null)
        {
            // This could be expanded to notify ServerManager of successful zone initialization
            // For now, just log the completion
            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.Log($"ZoneManager: Zone '{zoneName}' is ready for player interactions");
            }
        }
    }

    private void CleanupZone()
    {
        // Clean up any resources or references before despawn
        activeNodes.Clear();
        regionResources.Clear();
        isInitialized = false;
        
        if (Application.isEditor || Debug.isDebugBuild)
        {
            Debug.Log($"ZoneManager: Cleaned up zone '{zoneName}'");
        }
    }
    #endregion

    #region Resource Management
    private void PopulateRegionResources()
    {
        if (ResourceManager.Instance == null) 
        {
            Debug.LogError("ZoneManager: ResourceManager instance not found during PopulateRegionResources!");
            return;
        }

        regionResources.Clear();

        try
        {
            List<Resource> allSpawned = ResourceManager.Instance.GetAllResourceInstances();
            if (allSpawned != null)
            {
                // Filter resources by region type and date validity
                regionResources = allSpawned.Where(resource => 
                    resource != null && 
                    resource.SubType == regionResourceType && 
                    resource.IsResourceInDate()).ToList();
                    
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    Debug.Log($"ZoneManager: Populated {regionResources.Count} region resources for zone '{zoneName}' with subtype {regionResourceType}");
                }
            }
            else
            {
                Debug.LogWarning($"ZoneManager: GetAllResourceInstances returned null for zone '{zoneName}'");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneManager: Error accessing ResourceManager for zone '{zoneName}': {ex.Message}");
        }
    }

    private async Task<Resource> CreateNewResourceAsync(ResourceType type)
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError($"ZoneManager: Cannot create resource, ResourceManager instance not found for zone '{zoneName}'!");
            return null;
        }
        
        if (Application.isEditor || Debug.isDebugBuild)
        {
            Debug.Log($"ZoneManager: Creating new resource of type {type} for zone '{zoneName}' with subtype {regionResourceType}");
        }
        
        try
        {
            Resource newResource = await ResourceManager.Instance.SpawnResourceFromTemplateAsync(type, regionResourceType);

            if (newResource != null)
            {
                regionResources.Add(newResource);
                
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    Debug.Log($"ZoneManager: Successfully created resource '{newResource.ResourceName}' (ID: {newResource.ResourceSpawnID}) for zone '{zoneName}'");
                }
            }
            else
            {
                Debug.LogError($"ZoneManager: Failed to spawn resource of type {type} for zone '{zoneName}'");
            }
            return newResource;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneManager: Exception during CreateNewResourceAsync for type {type} in zone '{zoneName}': {ex.Message}");
            return null;
        }
    }
    #endregion

    #region Node Management
    private async Task AssignResourcesToNodesAsync()
    {
        if (Application.isEditor || Debug.isDebugBuild)
        {
            Debug.Log($"ZoneManager: Assigning resources to nodes in zone '{zoneName}'");
        }

        var nodesByType = activeNodes.GroupBy(node => node.GetResourceType());

        foreach (var nodeGroup in nodesByType)
        {
            ResourceType type = nodeGroup.Key;
            
            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.Log($"ZoneManager: Processing {nodeGroup.Count()} nodes of type {type} in zone '{zoneName}'");
            }

            // Find existing resource of this type in our region
            Resource existingResource = regionResources.FirstOrDefault(r => r.Type == type);
            
            // If no existing resource, create one
            if (existingResource == null)
            {
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    Debug.Log($"ZoneManager: No existing resource found for type {type} in zone '{zoneName}'. Creating new one...");
                }
                
                existingResource = await CreateNewResourceAsync(type);
                if (existingResource == null)
                {
                    Debug.LogError($"ZoneManager: Failed to create resource for type {type} in zone '{zoneName}'. Skipping {nodeGroup.Count()} nodes.");
                    continue;
                }
            }

            // Assign the resource to all nodes in this group
            foreach (ResourceNode node in nodeGroup)
            {
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    Debug.Log($"ZoneManager: Assigning resource '{existingResource.ResourceName}' (ID: {existingResource.ResourceSpawnID}) to node '{node.gameObject.name}' in zone '{zoneName}'");
                }
                node.SetResource(existingResource);
            }
        }

        if (Application.isEditor || Debug.isDebugBuild)
        {
            Debug.Log($"ZoneManager: Finished assigning resources to nodes in zone '{zoneName}'");
        }
    }

    public void RegisterNode(ResourceNode node)
    {
        if (node == null)
        {
            Debug.LogWarning($"ZoneManager: Attempted to register null ResourceNode in zone '{zoneName}'");
            return;
        }

        if (!activeNodes.Contains(node))
        {
            activeNodes.Add(node);
            node.SetResourceItemPrefab(resourceItemPrefab);
            node.SetResourceSubType(regionResourceType);
            
            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.Log($"ZoneManager: Registered ResourceNode '{node.gameObject.name}' in zone '{zoneName}'");
            }
        }
    }

    public void UnregisterNode(ResourceNode node)
    {
        if (node == null)
        {
            return;
        }

        if (activeNodes.Contains(node))
        {
            activeNodes.Remove(node);
            
            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.Log($"ZoneManager: Unregistered ResourceNode '{node.gameObject.name}' from zone '{zoneName}'");
            }
        }
    }
    #endregion

    #region Zone Operations (Bridge Pattern Integration)
    /// <summary>
    /// Process resource node interaction from ServerManager
    /// </summary>
    public async Task<bool> ProcessResourceNodeInteraction(int nodeNetworkID, int characterID, int toolDamage, HarvestType interactionType)
    {
        if (!IsServer)
        {
            Debug.LogError("ZoneManager: ProcessResourceNodeInteraction called on client!");
            return false;
        }

        if (!isInitialized)
        {
            Debug.LogWarning($"ZoneManager: Cannot process interaction - zone '{zoneName}' not initialized yet");
            return false;
        }

        try
        {
            // Find the ResourceNode by NetworkObjectId (this will need to be implemented when ResourceNodes become NetworkBehaviours)
            // For now, this is a placeholder for the future integration
            
            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.Log($"ZoneManager: Processing resource interaction in zone '{zoneName}' - NodeID: {nodeNetworkID}, CharID: {characterID}, Damage: {toolDamage}");
            }

            // This method will be expanded in Phase 12c when ResourceNodes become NetworkBehaviours
            // The logic will include:
            // 1. Find the specific ResourceNode by network ID
            // 2. Apply damage and check if resource should be generated
            // 3. Generate resources via ResourceManager
            // 4. Return success/failure and generated resources
            
            return true; // Placeholder return
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneManager: Error processing resource interaction in zone '{zoneName}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get zone status for ServerManager
    /// </summary>
    public ZoneStatus GetZoneStatus()
    {
        return new ZoneStatus
        {
            ZoneName = zoneName,
            IsInitialized = isInitialized,
            ActiveNodeCount = activeNodes.Count,
            RegionResourceCount = regionResources.Count,
            RegionResourceType = regionResourceType
        };
    }
    #endregion

    #region Public Getters
    /// <summary>
    /// Get the market waypoint for this zone (may be null if zone has no waypoint)
    /// </summary>
    public Transform GetMarketWaypoint()
    {
        return marketWaypoint;
    }

    /// <summary>
    /// Check if this zone has a market waypoint
    /// </summary>
    public bool HasMarketWaypoint()
    {
        return marketWaypoint != null;
    }

    public List<Resource> GetRegionResources()
    {
        return new List<Resource>(regionResources); // Return copy for safety
    }

    public List<ResourceNode> GetActiveNodes()
    {
        return new List<ResourceNode>(activeNodes); // Return copy for safety
    }

    public int GetActiveNodeCount()
    {
        return activeNodes.Count;
    }
    #endregion

    #region Unity Editor Support
    #if UNITY_EDITOR
    [ContextMenu("Debug: Force Initialize Zone")]
    private void DebugForceInitialize()
    {
        if (Application.isPlaying && IsServer)
        {
            _ = InitializeZoneAsync();
        }
        else
        {
            Debug.LogWarning("ZoneManager: Debug initialization only available during play mode on server");
        }
    }

    [ContextMenu("Debug: Print Zone Status")]
    private void DebugPrintZoneStatus()
    {
        ZoneStatus status = GetZoneStatus();
        Debug.Log($"=== Zone Status for '{status.ZoneName}' ===");
        Debug.Log($"Initialized: {status.IsInitialized}");
        Debug.Log($"Active Nodes: {status.ActiveNodeCount}");
        Debug.Log($"Region Resources: {status.RegionResourceCount}");
        Debug.Log($"Resource Type: {status.RegionResourceType}");
        Debug.Log($"Is Server: {IsServer}");
        Debug.Log($"Network Spawned: {IsSpawned}");
    }

    [ContextMenu("Debug: List Active Nodes")]
    private void DebugListActiveNodes()
    {
        Debug.Log($"=== Active Nodes in Zone '{zoneName}' ===");
        for (int i = 0; i < activeNodes.Count; i++)
        {
            ResourceNode node = activeNodes[i];
            if (node != null)
            {
                Debug.Log($"Node {i}: {node.gameObject.name} (Type: {node.GetResourceType()})");
            }
            else
            {
                Debug.Log($"Node {i}: NULL");
            }
        }
    }

    [ContextMenu("Debug: Check Market Waypoint")]
    private void DebugCheckMarketWaypoint()
    {
        Debug.Log($"=== Market Waypoint Status for Zone '{zoneName}' ===");
        if (HasMarketWaypoint())
        {
            Debug.Log($"Market Waypoint: Found at position {marketWaypoint.position}");
            Debug.Log($"Waypoint GameObject: {marketWaypoint.gameObject.name}");
        }
        else
        {
            Debug.Log("Market Waypoint: Not found or not set");
        }
    }

    [ContextMenu("Debug: Force Discover Waypoint")]
    private void DebugForceDiscoverWaypoint()
    {
        if (Application.isPlaying)
        {
            DiscoverMarketWaypoint();
        }
        else
        {
            Debug.LogWarning("ZoneManager: Waypoint discovery only available during play mode");
        }
    }
    #endif
    #endregion
}

/// <summary>
/// Zone status data structure for communication with ServerManager
/// </summary>
[Serializable]
public struct ZoneStatus
{
    public string ZoneName;
    public bool IsInitialized;
    public int ActiveNodeCount;
    public int RegionResourceCount;
    public ResourceSubType RegionResourceType;
}