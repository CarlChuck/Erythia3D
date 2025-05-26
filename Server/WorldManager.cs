using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;

/// <summary>
/// Handles runtime spawning and management of ZoneManager NetworkBehaviours.
/// Coordinates with PersistentSceneManager zone loading events and ServerManager.
/// Should be attached to a child object of PersistentSceneManager to ensure persistence.
/// </summary>
public class WorldManager : MonoBehaviour
{
    #region Constants
    private const float zoneManagerSpawnDelay = 0.5f; // Small delay to ensure zone is fully loaded
    #endregion

    #region Private Fields
    [SerializeField] private GameObject zoneManagerPrefab;
    [SerializeField] private bool debugMode = true;
    [SerializeField] private Transform zoneManagerParent; // Optional parent for organization
    
    // Zone-specific configuration mapping
    [SerializeField] private ZoneConfiguration[] zoneConfigurations = new ZoneConfiguration[]
    {
        new ZoneConfiguration 
        { 
            zoneName = "IthoriaSouth", 
            resourceSubType = ResourceSubType.Ithoria 
        }
        // Future zones can be added here:
        // new ZoneConfiguration { zoneName = "Aelystian", resourceSubType = ResourceSubType.Aelystian },
        // new ZoneConfiguration { zoneName = "Qadian", resourceSubType = ResourceSubType.Qadian },
    };
    
    // Active ZoneManager tracking
    private Dictionary<string, ZoneManager> activeZoneManagers = new Dictionary<string, ZoneManager>();
    private Dictionary<string, NetworkObject> zoneManagerNetworkObjects = new Dictionary<string, NetworkObject>();
    
    private static WorldManager instance;
    private bool hasInitialized = false;
    #endregion

    #region Public Properties
    public static WorldManager Instance => instance;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            
            if (debugMode)
            {
                Debug.Log("WorldManager: Initialized singleton instance");
            }
        }
        else
        {
            if (debugMode)
            {
                Debug.Log("WorldManager: Duplicate instance destroyed");
            }
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (instance == this && !hasInitialized)
        {
            InitializeWorldManager();
        }
    }

    private void OnDestroy()
    {
        // Clean up event subscriptions
        if (PersistentSceneManager.Instance != null)
        {
            PersistentSceneManager.OnZoneLoadCompleted -= OnZoneLoadCompleted;
            PersistentSceneManager.OnZoneUnloadCompleted -= OnZoneUnloadCompleted;
        }
        
        // Clean up any active ZoneManagers
        CleanupAllZoneManagers();
    }
    #endregion

    #region Initialization
    private void InitializeWorldManager()
    {
        hasInitialized = true;
        
        // Validate required components
        if (zoneManagerPrefab == null)
        {
            Debug.LogError("WorldManager: ZoneManager prefab is not assigned! Zone management will not work.");
            return;
        }

        // Validate prefab has ZoneManager component
        ZoneManager zoneManagerComponent = zoneManagerPrefab.GetComponent<ZoneManager>();
        if (zoneManagerComponent == null)
        {
            Debug.LogError("WorldManager: Assigned prefab does not have ZoneManager component!");
            return;
        }

        // Validate prefab has NetworkObject component
        NetworkObject networkObjectComponent = zoneManagerPrefab.GetComponent<NetworkObject>();
        if (networkObjectComponent == null)
        {
            Debug.LogError("WorldManager: ZoneManager prefab does not have NetworkObject component!");
            return;
        }

        // Subscribe to PersistentSceneManager events
        if (PersistentSceneManager.Instance != null)
        {
            PersistentSceneManager.OnZoneLoadCompleted += OnZoneLoadCompleted;
            PersistentSceneManager.OnZoneUnloadCompleted += OnZoneUnloadCompleted;
            
            if (debugMode)
            {
                Debug.Log("WorldManager: Subscribed to PersistentSceneManager zone events");
            }
        }
        else
        {
            Debug.LogError("WorldManager: PersistentSceneManager.Instance is null! Cannot subscribe to zone events.");
        }

        if (debugMode)
        {
            Debug.Log($"WorldManager: Initialization complete with {zoneConfigurations.Length} zone configurations");
        }
    }
    #endregion

    #region Zone Event Handlers
    private void OnZoneLoadCompleted(string zoneName)
    {
        if (!IsServer())
        {
            // Only server should spawn ZoneManagers
            if (debugMode)
            {
                Debug.Log($"WorldManager: Ignoring zone load '{zoneName}' - not running as server");
            }
            return;
        }

        if (debugMode)
        {
            Debug.Log($"WorldManager: Zone '{zoneName}' loaded, spawning ZoneManager...");
        }

        // Small delay to ensure zone scene is fully loaded
        StartCoroutine(DelayedZoneManagerSpawn(zoneName));
    }

    private void OnZoneUnloadCompleted(string zoneName)
    {
        if (!IsServer())
        {
            return;
        }

        if (debugMode)
        {
            Debug.Log($"WorldManager: Zone '{zoneName}' unloaded, cleaning up ZoneManager...");
        }

        CleanupZoneManager(zoneName);
    }
    #endregion

    #region ZoneManager Lifecycle
    private System.Collections.IEnumerator DelayedZoneManagerSpawn(string zoneName)
    {
        yield return new WaitForSeconds(zoneManagerSpawnDelay);
        SpawnZoneManager(zoneName);
    }

    private void SpawnZoneManager(string zoneName)
    {
        try
        {
            // Check if ZoneManager already exists for this zone
            if (activeZoneManagers.ContainsKey(zoneName))
            {
                if (debugMode)
                {
                    Debug.LogWarning($"WorldManager: ZoneManager already exists for zone '{zoneName}'");
                }
                return;
            }

            // Get zone configuration
            ZoneConfiguration config = GetZoneConfiguration(zoneName);
            if (config == null)
            {
                Debug.LogError($"WorldManager: No configuration found for zone '{zoneName}'. Cannot spawn ZoneManager.");
                return;
            }

            // Get ServerManager reference
            ServerManager serverManager = ServerManager.Instance;
            if (serverManager == null)
            {
                Debug.LogError($"WorldManager: ServerManager.Instance is null. Cannot spawn ZoneManager for zone '{zoneName}'.");
                return;
            }

            // Determine spawn parent
            Transform spawnParent = zoneManagerParent != null ? zoneManagerParent : transform;

            // Instantiate ZoneManager prefab
            GameObject zoneManagerInstance = Instantiate(zoneManagerPrefab, spawnParent);
            zoneManagerInstance.name = $"ZoneManager_{zoneName}";

            // Get components
            ZoneManager zoneManager = zoneManagerInstance.GetComponent<ZoneManager>();
            NetworkObject networkObject = zoneManagerInstance.GetComponent<NetworkObject>();

            if (zoneManager == null || networkObject == null)
            {
                Debug.LogError($"WorldManager: Failed to get required components from ZoneManager instance for zone '{zoneName}'");
                Destroy(zoneManagerInstance);
                return;
            }

            // Configure server-only visibility
            networkObject.CheckObjectVisibility = (clientId) => false; // Server-only, invisible to all clients

            // Spawn as NetworkObject
            networkObject.Spawn();

            // Initialize ZoneManager with zone-specific data
            zoneManager.InitializeForZone(zoneName, config.resourceSubType, serverManager);

            // Track the spawned ZoneManager
            activeZoneManagers[zoneName] = zoneManager;
            zoneManagerNetworkObjects[zoneName] = networkObject;

            if (debugMode)
            {
                Debug.Log($"WorldManager: Successfully spawned and initialized ZoneManager for zone '{zoneName}' with ResourceSubType '{config.resourceSubType}'");
            }

            // Notify ServerManager that ZoneManager is ready (if needed for future integration)
            NotifyServerManagerZoneReady(zoneName, zoneManager);
        }
        catch (Exception ex)
        {
            Debug.LogError($"WorldManager: Exception while spawning ZoneManager for zone '{zoneName}': {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void CleanupZoneManager(string zoneName)
    {
        try
        {
            // Remove from tracking
            if (activeZoneManagers.ContainsKey(zoneName))
            {
                ZoneManager zoneManager = activeZoneManagers[zoneName];
                activeZoneManagers.Remove(zoneName);

                if (debugMode)
                {
                    Debug.Log($"WorldManager: Removed ZoneManager for zone '{zoneName}' from active tracking");
                }
            }

            // Despawn NetworkObject
            if (zoneManagerNetworkObjects.ContainsKey(zoneName))
            {
                NetworkObject networkObject = zoneManagerNetworkObjects[zoneName];
                zoneManagerNetworkObjects.Remove(zoneName);

                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                    
                    if (debugMode)
                    {
                        Debug.Log($"WorldManager: Despawned ZoneManager NetworkObject for zone '{zoneName}'");
                    }
                }

                // Destroy the GameObject
                if (networkObject != null && networkObject.gameObject != null)
                {
                    Destroy(networkObject.gameObject);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"WorldManager: Exception while cleaning up ZoneManager for zone '{zoneName}': {ex.Message}");
        }
    }

    private void CleanupAllZoneManagers()
    {
        if (debugMode)
        {
            Debug.Log($"WorldManager: Cleaning up all {activeZoneManagers.Count} active ZoneManagers");
        }

        // Create a copy of the keys to avoid modification during iteration
        List<string> zoneNames = new List<string>(activeZoneManagers.Keys);
        
        foreach (string zoneName in zoneNames)
        {
            CleanupZoneManager(zoneName);
        }
    }
    #endregion

    #region Configuration Management
    private ZoneConfiguration GetZoneConfiguration(string zoneName)
    {
        foreach (ZoneConfiguration config in zoneConfigurations)
        {
            if (config.zoneName == zoneName)
            {
                return config;
            }
        }
        return null;
    }

    /// <summary>
    /// Add or update a zone configuration at runtime
    /// </summary>
    public void RegisterZoneConfiguration(string zoneName, ResourceSubType resourceSubType)
    {
        // Check if configuration already exists
        for (int i = 0; i < zoneConfigurations.Length; i++)
        {
            if (zoneConfigurations[i].zoneName == zoneName)
            {
                zoneConfigurations[i].resourceSubType = resourceSubType;
                
                if (debugMode)
                {
                    Debug.Log($"WorldManager: Updated configuration for zone '{zoneName}' to ResourceSubType '{resourceSubType}'");
                }
                return;
            }
        }

        // Add new configuration (expand array)
        ZoneConfiguration newConfig = new ZoneConfiguration
        {
            zoneName = zoneName,
            resourceSubType = resourceSubType
        };

        Array.Resize(ref zoneConfigurations, zoneConfigurations.Length + 1);
        zoneConfigurations[zoneConfigurations.Length - 1] = newConfig;

        if (debugMode)
        {
            Debug.Log($"WorldManager: Added new configuration for zone '{zoneName}' with ResourceSubType '{resourceSubType}'");
        }
    }
    #endregion

    #region Integration Methods
    private void NotifyServerManagerZoneReady(string zoneName, ZoneManager zoneManager)
    {
        // This method can be expanded for future ServerManager integration
        // For now, it's a placeholder for communication between WorldManager and ServerManager
        
        if (debugMode)
        {
            Debug.Log($"WorldManager: Zone '{zoneName}' is ready for server operations");
        }

        // Future: ServerManager.OnZoneManagerReady(zoneName, zoneManager);
    }

    /// <summary>
    /// Get active ZoneManager for a specific zone (for ServerManager access)
    /// </summary>
    public ZoneManager GetZoneManager(string zoneName)
    {
        return activeZoneManagers.ContainsKey(zoneName) ? activeZoneManagers[zoneName] : null;
    }

    /// <summary>
    /// Get all active ZoneManagers
    /// </summary>
    public Dictionary<string, ZoneManager> GetAllZoneManagers()
    {
        return new Dictionary<string, ZoneManager>(activeZoneManagers);
    }
    #endregion

    #region Helper Methods
    private bool IsServer()
    {
        // Check if we're running as server
        if (NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton.IsServer;
        }

        // Fallback: check if running in batch mode (dedicated server)
        return Application.isBatchMode;
    }
    #endregion

    #region Unity Editor Support
    #if UNITY_EDITOR
    [ContextMenu("Debug: Print Active ZoneManagers")]
    private void DebugPrintActiveZoneManagers()
    {
        Debug.Log($"=== Active ZoneManagers ({activeZoneManagers.Count}) ===");
        foreach (var kvp in activeZoneManagers)
        {
            ZoneManager zm = kvp.Value;
            if (zm != null)
            {
                ZoneStatus status = zm.GetZoneStatus();
                Debug.Log($"Zone '{kvp.Key}': Initialized={status.IsInitialized}, Nodes={status.ActiveNodeCount}, Resources={status.RegionResourceCount}");
            }
            else
            {
                Debug.Log($"Zone '{kvp.Key}': NULL ZoneManager");
            }
        }
    }

    [ContextMenu("Debug: Print Zone Configurations")]
    private void DebugPrintZoneConfigurations()
    {
        Debug.Log($"=== Zone Configurations ({zoneConfigurations.Length}) ===");
        for (int i = 0; i < zoneConfigurations.Length; i++)
        {
            ZoneConfiguration config = zoneConfigurations[i];
            Debug.Log($"Config {i}: Zone='{config.zoneName}', ResourceSubType='{config.resourceSubType}'");
        }
    }

    [ContextMenu("Debug: Force Spawn ZoneManager for IthoriaSouth")]
    private void DebugForceSpawnIthoriaSouth()
    {
        if (Application.isPlaying && IsServer())
        {
            SpawnZoneManager("IthoriaSouth");
        }
        else
        {
            Debug.LogWarning("WorldManager: Debug spawn only available during play mode on server");
        }
    }

    [ContextMenu("Debug: Cleanup All ZoneManagers")]
    private void DebugCleanupAllZoneManagers()
    {
        if (Application.isPlaying)
        {
            CleanupAllZoneManagers();
        }
        else
        {
            Debug.LogWarning("WorldManager: Debug cleanup only available during play mode");
        }
    }
    #endif
    #endregion
}

/// <summary>
/// Configuration data for zone-specific settings
/// </summary>
[Serializable]
public class ZoneConfiguration
{
    public string zoneName;
    public ResourceSubType resourceSubType;
}
